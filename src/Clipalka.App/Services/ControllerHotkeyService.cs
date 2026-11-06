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

public sealed class ControllerHotkeyService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Func<TimeSpan> _now;
    private readonly Func<IReadOnlyList<IControllerInput>>? _enumerateOverride;
    private readonly ControllerChordMatcher _matcher = new();
    private readonly ControllerChordMatcher _physicalMatcher = new();
    private readonly Func<bool> _canTrigger;
    private readonly Func<string?>? _blockReason;
    private readonly Action _saveReplay;
    private readonly Queue<string> _trace = new();
    private List<IControllerInput> _devices = [];
    private RawControllerInputs? _rawInputs;
    private string _detectionSummary = "Обнаружение ещё не запускалось.";
    public string DetectionDetails => _detectionSummary + "\n" + string.Join("\n", _trace);
    private TimeSpan _lastScan = TimeSpan.FromSeconds(-3);
    private TimeSpan _learnUntil, _feedbackUntil;
    private string? _learningDevice;
    private bool _learningNeutral;
    private int[] _bestChord = [];
    private ControllerBinding? _binding;
    private string? _lastStatus, _lastInputs;
    private bool _disposed;

    public event Action<IReadOnlyList<ControllerDevice>>? DevicesChanged;
    public event Action<ControllerBinding>? BindingCaptured;
    public event Action<string>? StatusChanged;
    public event Action<string>? InputChanged;
    public event Action<string>? TriggerRejected;
    public bool IsLearning => _learningDevice is not null;

    public ControllerHotkeyService(Dispatcher dispatcher, Func<bool> canTrigger, Action saveReplay,
        Func<string?>? blockReason = null)
        : this(dispatcher, canTrigger, saveReplay, null, null, true, blockReason) { }

    internal ControllerHotkeyService(Dispatcher dispatcher, Func<bool> canTrigger, Action saveReplay,
        Func<IReadOnlyList<IControllerInput>>? enumerate, Func<TimeSpan>? now, bool startTimer,
        Func<string?>? blockReason = null)
    {
        _canTrigger = canTrigger;
        _saveReplay = saveReplay;
        _blockReason = blockReason;
        _enumerateOverride = enumerate;
        _now = now ?? (() => _clock.Elapsed);
        _timer = new DispatcherTimer(DispatcherPriority.Input, dispatcher) { Interval = TimeSpan.FromMilliseconds(20) };
        _timer.Tick += Tick;
        if (startTimer) _timer.Start();
    }

    public void Configure(ControllerBinding? binding)
    {
        _binding = binding is { DeviceId.Length: > 0, Inputs.Length: > 0 } && binding.Inputs.All(input => input >= 0)
            ? new ControllerBinding { DeviceId = binding.DeviceId, DeviceName = binding.DeviceName,
                Label = binding.Label, Inputs = binding.Inputs.Distinct().Order().ToArray() } : null;
        ResetMatchers();
        Trace(_binding is null ? "Binding disabled" : "Binding configured: " + _binding.DeviceId + " / " + _binding.Label);
    }

    public void StartLearning(string deviceId)
    {
        if (!_devices.Any(device => device.Id == deviceId))
            throw new InvalidOperationException("Подключите контроллер и выберите его в списке.");
        _learningDevice = deviceId;
        _learningNeutral = false;
        _bestChord = [];
        _learnUntil = _now() + TimeSpan.FromSeconds(30);
        _feedbackUntil = TimeSpan.Zero;
        ResetMatchers();
        Trace("Learning started: " + deviceId);
        PublishStatus("Отпусти кнопки, затем нажми сочетание и отпусти его. До 30 секунд.");
    }

    public void CancelLearning()
    {
        _learningDevice = null;
        _bestChord = [];
        ResetMatchers();
        _lastStatus = null;
    }

    public void RefreshDevices()
    {
        if (_enumerateOverride is not null) { ApplyDevices(_enumerateOverride().ToList()); return; }
        var devices = new List<IControllerInput>();
        var errors = new List<string>();
        try
        {
            _rawInputs ??= new RawControllerInputs();
            devices.AddRange(_rawInputs.Enumerate());
        }
        catch (Exception error) { errors.Add("Raw Input: " + error.Message); }
        RawGameController[] connected = [];
        try { connected = RawGameController.RawGameControllers.ToArray(); }
        catch (Exception error) { errors.Add("Windows.Gaming.Input: " + error.Message); }
        foreach (var controller in connected)
        {
            try
            {
                var id = controller.NonRoamableId;
                if (string.IsNullOrWhiteSpace(id)) id = $"wgi:{controller.HardwareVendorId:X4}:{controller.HardwareProductId:X4}:{controller.DisplayName}";
                if (controller.ButtonCount == 0) continue;
                devices.Add(_devices.FirstOrDefault(device => device.Id == id) ?? new DeviceState(controller, id));
            }
            catch (Exception error) { errors.Add("WGI device: " + error.Message); }
        }
        try { devices.AddRange(NativeControllerInputs.Enumerate()); }
        catch (Exception error) { errors.Add("Desktop input: " + error.Message); }
        _detectionSummary = $"WGI: {connected.Length}; доступные источники: {devices.Count}. " + string.Join("; ", errors);
        ApplyDevices(devices);
    }

    private void ApplyDevices(List<IControllerInput> devices)
    {
        var bindingId = _binding?.DeviceId;
        var availabilityChanged = _devices.Any(device => device.Id == bindingId) != devices.Any(device => device.Id == bindingId);
        var listChanged = !_devices.Select(device => device.Id).SequenceEqual(devices.Select(device => device.Id));
        _devices = devices;
        if (availabilityChanged) ResetMatchers();
        if (listChanged)
        {
            Trace("Devices: " + string.Join(", ", devices.Select(device => device.Id)));
            DevicesChanged?.Invoke(devices.Select(device => new ControllerDevice(device.Id, device.Name)).ToArray());
        }
    }

    private void Tick(object? sender, EventArgs e) => Poll();

    internal void Poll()
    {
        if (_disposed) return;
        try
        {
            var now = _now();
            if (now - _lastScan >= TimeSpan.FromSeconds(2)) { _lastScan = now; RefreshDevices(); }
            if (IsLearning && now >= _learnUntil)
            {
                CancelLearning();
                _feedbackUntil = now + TimeSpan.FromSeconds(4);
                Trace("Learning timed out without a captured chord");
                PublishStatus("Назначение отменено: кнопки не выбраны. Можно попробовать снова.");
                return;
            }
            var id = _learningDevice ?? _binding?.DeviceId;
            var device = _devices.FirstOrDefault(candidate => candidate.Id == id);
            if (device is null)
            {
                ResetMatchers();
                PublishInputs("Нет данных назначенного контроллера");
                if (IsLearning) CancelLearning();
                PublishStatus(_devices.Count == 0 ? "Контроллер не найден. Подключи DualSense или Flydigi." :
                    _binding is null ? "Выбери контроллер и назначь сочетание." : "Назначенный контроллер отключён. Проверь выбранное устройство.");
                return;
            }
            var pressed = device.ReadPressed();
            if (!device.HasReading)
            {
                ResetMatchers();
                PublishInputs("Ожидание первого отчёта: нажми и отпусти кнопку геймпада");
                return;
            }
            PublishInputs(pressed.Length == 0 ? "Кнопки отпущены" : string.Join(" + ", pressed.Select(device.Label)));
            if (IsLearning)
            {
                if (pressed.Length == 0)
                {
                    if (_bestChord.Length > 0)
                    {
                        var binding = new ControllerBinding { DeviceId = device.Id, DeviceName = device.Name, Inputs = _bestChord,
                            Label = string.Join(" + ", _bestChord.Select(device.Label)) };
                        CancelLearning();
                        _feedbackUntil = now + TimeSpan.FromSeconds(4);
                        Trace("Binding captured: " + binding.Label);
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
            if (_binding is null) return;
            var permitted = _canTrigger();
            var physicallyPressed = _physicalMatcher.Update(_binding.Inputs, pressed, now, true);
            var accepted = _matcher.Update(_binding.Inputs, pressed, now, permitted);
            if (accepted)
            {
                Trace("Replay requested");
                _feedbackUntil = now + TimeSpan.FromSeconds(3);
                PublishStatus("Сочетание распознано — сохраняем replay.");
                _saveReplay();
            }
            else if (physicallyPressed && !permitted)
            {
                var reason = _blockReason?.Invoke() ?? "сохранение временно недоступно";
                Trace("Replay rejected: " + reason);
                _feedbackUntil = now + TimeSpan.FromSeconds(3);
                PublishStatus("Сочетание распознано, но " + reason);
                TriggerRejected?.Invoke(reason);
            }
            else if (now >= _feedbackUntil)
                PublishStatus("Подключён: " + device.Name + " · сочетание готово");
        }
        catch (Exception error)
        {
            Trace(error.GetType().Name + ": " + error.Message);
            ResetMatchers();
            if (IsLearning) CancelLearning();
            _devices.Clear();
            DevicesChanged?.Invoke([]);
            PublishInputs("Не удалось прочитать кнопки");
            PublishStatus("Не удалось прочитать контроллер. Проверь подключение и режим устройства.");
        }
    }

    private void ResetMatchers() { _matcher.Reset(); _physicalMatcher.Reset(); }
    private void PublishInputs(string text)
    {
        if (_lastInputs == text) return;
        _lastInputs = text;
        Trace("Inputs: " + text);
        InputChanged?.Invoke(text);
    }
    private void Trace(string text)
    {
        _trace.Enqueue($"{_now().TotalSeconds:F2}s {text}");
        while (_trace.Count > 100) _trace.Dequeue();
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
        _rawInputs?.Dispose();
        _devices.Clear();
        CancelLearning();
    }

    private sealed class DeviceState : IControllerInput
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
