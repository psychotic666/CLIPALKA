using System.Diagnostics;
using System.Windows.Threading;
using Windows.Gaming.Input;
using Clipalka.Core.Models;
using Clipalka.Core.Services;

namespace Clipalka.App.Services;

public sealed record ControllerDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

// RawGameController covers HID/DirectInput and controllers exposed through XInput.
// Device-reported indices are learned rather than guessed from a vendor-specific layout.
public sealed class ControllerHotkeyService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ControllerChordMatcher _matcher = new();
    private readonly Func<bool> _canTrigger;
    private readonly Action _saveReplay;
    private List<DeviceState> _devices = [];
    private TimeSpan _lastScan = TimeSpan.FromSeconds(-3);
    private TimeSpan _learnUntil;
    private string? _learningDevice;
    private bool _learningNeutral;
    private int[] _bestChord = [];
    private ControllerBinding? _binding;
    private string? _lastStatus;
    private bool _disposed;

    public event Action<IReadOnlyList<ControllerDevice>>? DevicesChanged;
    public event Action<ControllerBinding>? BindingCaptured;
    public event Action<string>? StatusChanged;
    public bool IsLearning => _learningDevice is not null;

    public ControllerHotkeyService(Dispatcher dispatcher, Func<bool> canTrigger, Action saveReplay)
    {
        _canTrigger = canTrigger;
        _saveReplay = saveReplay;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background, Tick, dispatcher);
    }

    public void Configure(ControllerBinding? binding)
    {
        _binding = binding is { DeviceId.Length: > 0, Inputs.Length: > 0 } && binding.Inputs.All(input => input >= 0)
            ? new ControllerBinding { DeviceId = binding.DeviceId, DeviceName = binding.DeviceName,
                Label = binding.Label, Inputs = binding.Inputs.Distinct().Order().ToArray() } : null;
        _matcher.Reset();
    }

    public void StartLearning(string deviceId)
    {
        if (!_devices.Any(device => device.Id == deviceId))
            throw new InvalidOperationException("Подключите контроллер и выберите его в списке.");
        _learningDevice = deviceId;
        _learningNeutral = false;
        _bestChord = [];
        _learnUntil = _clock.Elapsed + TimeSpan.FromSeconds(15);
        _matcher.Reset();
        PublishStatus("Отпусти кнопки, затем нажми сочетание и отпусти его. До 15 секунд.");
    }

    public void CancelLearning()
    {
        _learningDevice = null;
        _bestChord = [];
        _matcher.Reset();
        _lastStatus = null;
    }

    public void RefreshDevices()
    {
        var connected = RawGameController.RawGameControllers.ToArray();
        var devices = new List<DeviceState>();
        foreach (var controller in connected)
        {
            try
            {
                var id = controller.NonRoamableId;
                if (string.IsNullOrWhiteSpace(id) || controller.ButtonCount == 0) continue;
                devices.Add(_devices.FirstOrDefault(device => device.Id == id) ?? new DeviceState(controller, id));
            }
            catch (Exception) { /* An unplugged device can disappear during enumeration. */ }
        }
        if (!_devices.Select(device => device.Id).SequenceEqual(devices.Select(device => device.Id)))
        {
            _devices = devices;
            _matcher.Reset();
            DevicesChanged?.Invoke(devices.Select(device => new ControllerDevice(device.Id, device.Name)).ToArray());
        }
        else _devices = devices;
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (_disposed) return;
        try
        {
            var now = _clock.Elapsed;
            if (now - _lastScan >= TimeSpan.FromSeconds(2))
            {
                _lastScan = now;
                RefreshDevices();
            }
            if (IsLearning && now >= _learnUntil)
            {
                CancelLearning();
                PublishStatus("Назначение отменено: кнопки не выбраны. Можно попробовать снова.");
                return;
            }
            var id = _learningDevice ?? _binding?.DeviceId;
            var device = _devices.FirstOrDefault(candidate => candidate.Id == id);
            if (device is null)
            {
                _matcher.Reset();
                if (IsLearning) CancelLearning();
                PublishStatus(_devices.Count == 0 ? "Контроллер не найден. Подключи DualSense или Flydigi." :
                    _binding is null ? "Выбери контроллер и назначь сочетание." : "Назначенный контроллер отключён. После смены режима назначь сочетание снова.");
                return;
            }
            var pressed = device.ReadPressed();
            if (IsLearning)
            {
                if (pressed.Length == 0)
                {
                    if (_bestChord.Length > 0)
                    {
                        var binding = new ControllerBinding
                        {
                            DeviceId = device.Id, DeviceName = device.Name, Inputs = _bestChord,
                            Label = string.Join(" + ", _bestChord.Select(device.Label))
                        };
                        CancelLearning();
                        BindingCaptured?.Invoke(binding);
                        PublishStatus("Сочетание выбрано. Нажми «Сохранить настройки».");
                    }
                    else _learningNeutral = true;
                }
                else if (_learningNeutral && pressed.Length >= _bestChord.Length)
                {
                    _bestChord = pressed;
                    PublishStatus(string.Join(" + ", pressed.Select(device.Label)) + " — отпусти кнопки");
                }
                return;
            }
            PublishStatus("Подключён: " + device.Name + " · удержание сочетания 0,4 с");
            if (_binding is not null && _matcher.Update(_binding.Inputs, pressed, now, _canTrigger()))
                _saveReplay();
        }
        catch (Exception)
        {
            _matcher.Reset();
            if (IsLearning) CancelLearning();
            // Force fresh WinRT objects on the next scan after a Bluetooth disconnect.
            _devices.Clear();
            DevicesChanged?.Invoke([]);
            PublishStatus("Не удалось прочитать контроллер. Проверь подключение и режим устройства.");
        }
    }

    private void PublishStatus(string text)
    {
        if (_lastStatus == text) return;
        _lastStatus = text;
        StatusChanged?.Invoke(text);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Tick;
        _devices.Clear();
        CancelLearning();
    }

    private sealed class DeviceState
    {
        public RawGameController Controller { get; }
        public string Id { get; }
        public string Name { get; }
        private readonly bool[] _buttons;
        private readonly GameControllerSwitchPosition[] _switches;
        private readonly double[] _axes;

        public DeviceState(RawGameController controller, string id)
        {
            Controller = controller; Id = id;
            Name = string.IsNullOrWhiteSpace(controller.DisplayName) ? "Gamepad" : controller.DisplayName;
            _buttons = new bool[controller.ButtonCount];
            _switches = new GameControllerSwitchPosition[controller.SwitchCount];
            _axes = new double[controller.AxisCount];
        }

        public int[] ReadPressed()
        {
            Controller.GetCurrentReading(_buttons, _switches, _axes);
            var inputs = new List<int>();
            for (var i = 0; i < _buttons.Length; i++) if (_buttons[i]) inputs.Add(i);
            for (var i = 0; i < _switches.Length; i++)
                if (_switches[i] != GameControllerSwitchPosition.Center)
                    inputs.Add(_buttons.Length + i * 8 + (int)_switches[i] - 1);
            return inputs.ToArray();
        }

        public string Label(int input)
        {
            if (input >= _buttons.Length)
            {
                var direction = (GameControllerSwitchPosition)((input - _buttons.Length) % 8 + 1);
                return "D-pad " + direction;
            }
            try
            {
                var label = Controller.GetButtonLabel(input).ToString();
                if (label != "None") return label.Replace("PlayStation", "").Replace("Xbox", "");
            }
            catch (Exception) { /* Some generic HID drivers provide no labels. */ }
            return $"Button {input + 1}";
        }
    }
}
