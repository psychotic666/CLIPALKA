using System.ComponentModel;
using System.Windows;
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
    private readonly RecordingService _recordingService = new(new ReplayClipExporter());
    private AppSettings _settings = new();
    private GlobalHotkeyService? _hotkeys;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
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

            if (_settings.StartReplayBufferWithApp && !_recordingService.IsReplayBuffering)
            {
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
        SelectDevice(DisplayComboBox, _settings.DisplayDeviceName);
        SelectDevice(OutputDeviceComboBox, _settings.OutputAudioDeviceId);
        SelectDevice(InputDeviceComboBox, _settings.InputAudioDeviceId);
    }

    private void ApplyFormToSettings()
    {
        if (!HotkeyBinding.TryParse(RecordHotkeyTextBox.Text, out _))
        {
            throw new InvalidOperationException("Некорректный хоткей записи. Пример: Ctrl+Shift+R.");
        }

        if (!HotkeyBinding.TryParse(ReplayHotkeyTextBox.Text, out _))
        {
            throw new InvalidOperationException("Некорректный хоткей replay. Пример: Shift+Z.");
        }

        _settings.OutputDirectory = RecordingPathService.EnsureOutputDirectory(OutputPathTextBox.Text);
        _settings.FramesPerSecond = int.TryParse(FpsComboBox.SelectedValue?.ToString(), out var fps) ? fps : 60;
        _settings.DisplayDeviceName = (DisplayComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.OutputAudioDeviceId = (OutputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.InputAudioDeviceId = (InputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.RecordHotkey = RecordHotkeyTextBox.Text.Trim();
        _settings.ReplayHotkey = ReplayHotkeyTextBox.Text.Trim();
        _settings.StartReplayBufferWithApp = StartReplayCheckBox.IsChecked == true;
    }

    private void RegisterHotkeys()
    {
        if (_hotkeys is null)
        {
            return;
        }

        HotkeyBinding.TryParse(_settings.RecordHotkey, out var recordBinding);
        HotkeyBinding.TryParse(_settings.ReplayHotkey, out var replayBinding);
        _hotkeys.Register(RecordHotkeyId, recordBinding, () => _ = ToggleRecordingAsync());
        _hotkeys.Register(ReplayHotkeyId, replayBinding, () => _ = SaveReplayAsync());
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

    private static void SelectDevice(System.Windows.Controls.ComboBox comboBox, string? id)
    {
        var items = comboBox.ItemsSource?.Cast<DeviceOption>().ToList() ?? [];
        comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == id)
                                ?? items.FirstOrDefault(item => item.IsDefault)
                                ?? items.FirstOrDefault();
    }
}
