using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clipalka.App.Models;
using Clipalka.App.Services;
using Clipalka.Core.Models;
using Clipalka.Core.Services;
using Microsoft.Win32;

namespace Clipalka.App;

public partial class MainWindow : Window
{
    private const int RecordHotkeyId = 1001;
    private const int ReplayHotkeyId = 1002;
    private readonly ISettingsStore _settingsStore;
    private readonly DeviceCatalogService _deviceCatalog = new();
    private readonly CaptureTargetService _captureTargets = new();
    private readonly RecordingService _recordingService;
    private AppSettings _settings = new();
    private GlobalHotkeyService? _hotkeys;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        _recordingService = new RecordingService(new ReplayClipExporter(), _captureTargets);
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CLIPALKA",
            "settings.json");
        _settingsStore = new JsonSettingsStore(settingsPath);
        _recordingService.StatusChanged += (_, message) => Dispatcher.Invoke(() => SetStatus(message));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = await _settingsStore.LoadAsync();
            var normalizedRecordHotkey = NormalizeStoredHotkey(_settings.RecordHotkey);
            var normalizedReplayHotkey = NormalizeStoredHotkey(_settings.ReplayHotkey);
            if (normalizedRecordHotkey != _settings.RecordHotkey || normalizedReplayHotkey != _settings.ReplayHotkey)
            {
                _settings.RecordHotkey = normalizedRecordHotkey;
                _settings.ReplayHotkey = normalizedReplayHotkey;
                await _settingsStore.SaveAsync(_settings);
            }
            LoadDevices();
            PopulateForm();
            _hotkeys = new GlobalHotkeyService(new WindowInteropHelper(this).Handle);
            RegisterHotkeys();

            if (_settings.StartReplayBufferWithApp)
            {
                await _recordingService.StartReplayBufferAsync(_settings);
            }

            UpdateUi();
            SetStatus("Готово. Проверьте выбранные устройства перед первой записью.");
        }
        catch (Exception exception)
        {
            ShowError("Не удалось запустить CLIPALKA", exception);
        }
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e) => await ToggleRecordingAsync();

    private async Task ToggleRecordingAsync()
    {
        await RunUiActionAsync(async () =>
        {
            ApplyFormToSettings();
            await _recordingService.ToggleRecordingAsync(_settings);
            UpdateUi();
        });
    }

    private async void ReplayButton_Click(object sender, RoutedEventArgs e) => await SaveReplayAsync();

    private async Task SaveReplayAsync()
    {
        await RunUiActionAsync(async () =>
        {
            ApplyFormToSettings();
            ReplayButton.IsEnabled = false;
            SetStatus("Сохраняю последние 30 секунд…");
            await _recordingService.SaveReplayAsync(_settings);
            UpdateUi();
        });
    }

    private void MicrophoneButton_Click(object sender, RoutedEventArgs e)
    {
        _recordingService.SetMicrophoneMuted(!_recordingService.IsMicrophoneMuted);
        UpdateUi();
    }

    private void BrowseOutputPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Куда сохранять записи CLIPALKA",
            InitialDirectory = Directory.Exists(OutputPathTextBox.Text)
                ? OutputPathTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputPathTextBox.Text = dialog.FolderName;
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            ApplyFormToSettings();
            RecordingPathService.EnsureOutputDirectory(_settings.OutputDirectory);
            await _settingsStore.SaveAsync(_settings);
            RegisterHotkeys();

            if (_settings.StartReplayBufferWithApp)
            {
                if (_recordingService.IsReplayBuffering)
                {
                    await _recordingService.StopReplayBufferAsync();
                }
                await _recordingService.StartReplayBufferAsync(_settings);
            }
            else if (!_settings.StartReplayBufferWithApp && _recordingService.IsReplayBuffering)
            {
                await _recordingService.StopReplayBufferAsync();
            }

            UpdateUi();
            SetStatus("Настройки сохранены");
        });
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        e.Cancel = true;
        _isClosing = true;
        IsEnabled = false;
        SetStatus("Завершаю записи…");
        _hotkeys?.Dispose();
        await _recordingService.DisposeAsync();
        Closing -= Window_Closing;
        Close();
    }

    private void LoadDevices()
    {
        DisplayComboBox.ItemsSource = _deviceCatalog.GetDisplays();
        OutputDeviceComboBox.ItemsSource = _deviceCatalog.GetOutputDevices();
        InputDeviceComboBox.ItemsSource = _deviceCatalog.GetInputDevices();
    }

    private void PopulateForm()
    {
        OutputPathTextBox.Text = _settings.OutputDirectory;
        FpsComboBox.SelectedValue = _settings.FramesPerSecond.ToString();
        RecordHotkeyTextBox.Text = _settings.RecordHotkey;
        ReplayHotkeyTextBox.Text = _settings.ReplayHotkey;
        StartReplayCheckBox.IsChecked = _settings.StartReplayBufferWithApp;
        AutoGameCheckBox.IsChecked = _settings.AutoCaptureGame;
        GameOnlyHotkeysCheckBox.IsChecked = _settings.HotkeysOnlyWhileGameActive;
        SelectDevice(DisplayComboBox, _settings.DisplayDeviceName);
        SelectDevice(OutputDeviceComboBox, _settings.OutputAudioDeviceId);
        SelectDevice(InputDeviceComboBox, _settings.InputAudioDeviceId);
    }

    private void ApplyFormToSettings()
    {
        ValidateHotkey(RecordHotkeyTextBox.Text, "записи");
        ValidateHotkey(ReplayHotkeyTextBox.Text, "replay");

        _settings.OutputDirectory = RecordingPathService.EnsureOutputDirectory(OutputPathTextBox.Text);
        _settings.FramesPerSecond = int.TryParse(FpsComboBox.SelectedValue?.ToString(), out var fps) ? fps : 60;
        _settings.DisplayDeviceName = (DisplayComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.OutputAudioDeviceId = (OutputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.InputAudioDeviceId = (InputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.RecordHotkey = RecordHotkeyTextBox.Text.Trim();
        _settings.ReplayHotkey = ReplayHotkeyTextBox.Text.Trim();
        _settings.StartReplayBufferWithApp = StartReplayCheckBox.IsChecked == true;
        _settings.AutoCaptureGame = AutoGameCheckBox.IsChecked == true;
        _settings.HotkeysOnlyWhileGameActive = GameOnlyHotkeysCheckBox.IsChecked == true;
    }

    private void RegisterHotkeys()
    {
        if (_hotkeys is null)
        {
            return;
        }

        HotkeyBinding.TryParse(_settings.RecordHotkey, out var recordBinding);
        HotkeyBinding.TryParse(_settings.ReplayHotkey, out var replayBinding);
        _hotkeys.Register(
            RecordHotkeyId,
            recordBinding,
            () => _ = ToggleRecordingAsync(),
            CanExecuteGlobalHotkey);
        _hotkeys.Register(
            ReplayHotkeyId,
            replayBinding,
            () => _ = SaveReplayAsync(),
            CanExecuteGlobalHotkey);
    }

    private void UpdateUi()
    {
        RecordButtonTitle.Text = _recordingService.IsRecording ? "■  Остановить запись" : "●  Записывать экран";
        RecordButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            _recordingService.IsRecording ? "#A82D32" : "#E5484D"));
        MicrophoneButtonTitle.Text = _recordingService.IsMicrophoneMuted
            ? "🔇  Микрофон выключен"
            : "🎙  Микрофон включён";
        ReplayStatusText.Text = _recordingService.IsReplayBuffering ? "Replay активен" : "Replay выключен";
        ReplayStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            _recordingService.IsReplayBuffering ? "#36D399" : "#64748B"));
        ReplayButton.IsEnabled = _recordingService.IsReplayBuffering;
        RecordHotkeyHint.Text = string.IsNullOrWhiteSpace(_settings.RecordHotkey) ? "Хоткей выключен" : _settings.RecordHotkey.Replace("+", " + ");
        ReplayHotkeyHint.Text = string.IsNullOrWhiteSpace(_settings.ReplayHotkey) ? "Хоткей выключен" : _settings.ReplayHotkey.Replace("+", " + ");
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ShowError("Операция не выполнена", exception);
        }
        finally
        {
            UpdateUi();
        }
    }

    private void SetStatus(string message) => StatusTextBlock.Text = message;

    private void ShowError(string title, Exception exception)
    {
        SetStatus(exception.Message);
        MessageBox.Show(this, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (sender is not TextBox textBox)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            SetStatus("Удерживайте модификаторы и нажмите основную клавишу");
            return;
        }

        if (key is Key.Back or Key.Delete or Key.Escape)
        {
            textBox.Clear();
            SetStatus("Хоткей отключён. Сохраните настройки, чтобы применить.");
            return;
        }

        var modifiers = HotkeyModifiers.NoRepeat;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Windows;

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            SetStatus("Эта клавиша не поддерживается для глобального хоткея");
            return;
        }

        var binding = new HotkeyBinding(modifiers, virtualKey);
        var formattedBinding = binding.ToString();
        if (!HotkeyBinding.TryParse(formattedBinding, out _))
        {
            SetStatus("Эта клавиша пока не поддерживается. Используйте букву, цифру, F1–F24, Space, Tab или Enter.");
            return;
        }

        if (!binding.IsTypingSafe)
        {
            SetStatus("Добавьте Ctrl, Alt или Win — Shift+буква срабатывает при обычном наборе текста");
            return;
        }

        textBox.Text = formattedBinding;
        SetStatus($"Хоткей выбран: {binding}. Нажмите «Сохранить настройки».");
    }

    private bool CanExecuteGlobalHotkey()
    {
        if (!_settings.HotkeysOnlyWhileGameActive || _captureTargets.IsLikelyGameActive())
        {
            return true;
        }

        SetStatus("Хоткей проигнорирован: активного игрового окна нет");
        return false;
    }

    private static void ValidateHotkey(string value, string purpose)
    {
        if (!HotkeyBinding.TryParse(value, out var binding))
        {
            throw new InvalidOperationException($"Некорректный хоткей {purpose}.");
        }

        if (binding is not null && !binding.IsTypingSafe)
        {
            throw new InvalidOperationException(
                $"Хоткей {purpose} может срабатывать при печати. Используйте Ctrl, Alt или Win.");
        }
    }

    private static string NormalizeStoredHotkey(string value)
    {
        if (!HotkeyBinding.TryParse(value, out var binding) || binding is null)
        {
            return value;
        }

        return binding.WithTypingProtection().ToString();
    }

    private static void SelectDevice(System.Windows.Controls.ComboBox comboBox, string? id)
    {
        var items = comboBox.ItemsSource?.Cast<DeviceOption>().ToList() ?? [];
        comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == id)
                                ?? items.FirstOrDefault(item => item.IsDefault)
                                ?? items.FirstOrDefault();
    }
}
