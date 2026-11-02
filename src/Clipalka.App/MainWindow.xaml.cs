using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Clipalka.App.Models;
using Clipalka.App.Services;
using Clipalka.Core.Models;
using Clipalka.Core.Services;
using Microsoft.Win32;

namespace Clipalka.App;

public partial class MainWindow : Window
{
    // Render the production visual tree on a Windows runner; fixture values never enter normal startup.
    public void RenderUiSamples(string directory)
    {
        Directory.CreateDirectory(directory);
        DisplayComboBox.ItemsSource = new[] { new DeviceOption("test", "AF24H1") };
        OutputDeviceComboBox.ItemsSource = new[] { new DeviceOption("test", "SteelSeries Sonar — Gaming") };
        InputDeviceComboBox.ItemsSource = new[] { new DeviceOption("test", "SteelSeries Sonar — Microphone") };
        PopulateForm();
        RecordHotkeyHint.Text = _settings.RecordHotkey.Replace("+", " + ");
        ReplayHotkeyHint.Text = _settings.ReplayHotkey.Replace("+", " + ");
        ReplaySourceText.Text = "Rocket League";
        CaptureStateText.Text = "Захват активен";
        CaptureDescriptionText.Text = "Игра запущена и отслеживается";
        CaptureStateDot.Fill = (Brush)FindResource("SuccessBrush");
        CaptureStateText.Foreground = (Brush)FindResource("SuccessBrush");
        ToggleReplayButton.Content = "Остановить replay";
        var thumbnail = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/capture-preview.png"));
        CapturePreviewImage.Source = thumbnail;
        PreviewEmptyPanel.Visibility = Visibility.Collapsed;
        PreviewCaptionText.Text = "Последний сохранённый клип · Rocket League";
        _galleryClips = Enumerable.Range(1, 8).Select(i =>
            new RecentClipItem("", $"Пример клипа {i}", "Сегодня, 21:42", "0:30", thumbnail)).ToList();
        RecentClipsItemsControl.ItemsSource = _galleryClips.Take(4).ToList();
        FilterGallery();
        RecentClipsEmptyText.Visibility = Visibility.Collapsed;
        var root = (FrameworkElement)Content;
        Content = null;
        foreach (var (name, size, page) in new[]
        {
            ("ui-1585", new Size(1585, 992), "home"),
            ("ui-1280", new Size(1280, 800), "home"),
            ("ui-gallery", new Size(1280, 800), "gallery"),
            ("ui-diagnostics", new Size(1280, 800), "diagnostics"),
            ("ui-empty", new Size(1280, 800), "empty")
        })
        {
            ShowPage(page == "gallery" ? AudioPage : page == "diagnostics" ? VideoPage : DashboardPage,
                page == "gallery" ? ClipsNavButton : page == "diagnostics" ? VideoNavButton : DashboardNavButton);
            if (page == "empty")
            {
                CapturePreviewImage.Source = null;
                PreviewEmptyPanel.Visibility = Visibility.Visible;
                PreviewCaptionText.Text = "Предпросмотр появится после сохранения клипа";
                RecentClipsItemsControl.ItemsSource = null;
                RecentClipsEmptyText.Visibility = Visibility.Visible;
            }
            var scale = ApplyWindowScale(size);
            root.Width = size.Width / scale;
            root.Height = size.Height / scale;
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            var hintBottom = RecordHotkeyHint.TransformToAncestor(CaptureActionsGrid)
                .Transform(new Point(0, RecordHotkeyHint.ActualHeight)).Y;
            if ((page is "home" or "empty") && (hintBottom > CaptureActionsGrid.ActualHeight || Math.Abs(ReplayButton.ActualHeight - 84) > 0.1))
                throw new InvalidOperationException("Capture action layout clips its controls.");
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, name + ".png"));
            encoder.Save(file);
        }
        _trayNotifications.Dispose();
        _captureNotifications.Dispose();
    }

    private const int RecordHotkeyId = 1001;
    private const int ReplayHotkeyId = 1002;
    private const int MicrophoneHotkeyId = 1003;
    private const int GalleryHotkeyId = 1004;
    private readonly ISettingsStore _settingsStore;
    private readonly DeviceCatalogService _deviceCatalog = new();
    private readonly CaptureTargetService _captureTargets = new();
    private readonly RecordingService _recordingService;
    private readonly TrayNotificationService _trayNotifications;
    private readonly CaptureNotificationService _captureNotifications;
    private readonly DispatcherTimer _replayTargetTimer;
    private AppSettings _settings = new();
    private GlobalHotkeyService? _hotkeys;
    private bool _isClosing;
    private bool _exitRequested;
    private bool _backgroundHintShown;
    private bool _isRefreshingReplayTarget;
    private bool _isBusy;
    private IReadOnlyList<RecentClipItem> _galleryClips = [];
    private bool _formReady;
    private bool _settingsDirty;

    public MainWindow()
    {
        InitializeComponent();
        GreetingText.Text = DateTime.Now.Hour switch { < 6 => "Доброй ночи", < 12 => "Доброе утро", < 18 => "Добрый день", _ => "Добрый вечер" };
        foreach (var field in new[] { OutputPathTextBox, RecordHotkeyTextBox, ReplayHotkeyTextBox, MicrophoneHotkeyTextBox, GalleryHotkeyTextBox })
            field.TextChanged += SettingsFieldChanged;
        foreach (var field in new[] { DisplayComboBox, FpsComboBox, OutputDeviceComboBox, InputDeviceComboBox })
            field.SelectionChanged += SettingsFieldChanged;
        OutputVolumeSlider.ValueChanged += SettingsFieldChanged;
        MicrophoneVolumeSlider.ValueChanged += SettingsFieldChanged;
        foreach (var field in new[] { AutoGameCheckBox, StartReplayCheckBox, GameOnlyHotkeysCheckBox })
            field.Click += SettingsFieldChanged;
        _recordingService = new RecordingService(new ReplayClipExporter(), _captureTargets);
        _trayNotifications = new TrayNotificationService();
        _captureNotifications = new CaptureNotificationService(Dispatcher);
        _trayNotifications.OpenRequested += () => Dispatcher.BeginInvoke(RestoreFromTray);
        _trayNotifications.ToggleRecordingRequested += () =>
            Dispatcher.BeginInvoke(new Action(() => _ = ToggleRecordingAsync()));
        _trayNotifications.SaveReplayRequested += () =>
            Dispatcher.BeginInvoke(new Action(() => _ = SaveReplayAsync()));
        _trayNotifications.ExitRequested += () => Dispatcher.BeginInvoke(RequestExit);
        _replayTargetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _replayTargetTimer.Tick += ReplayTargetTimer_Tick;
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CLIPALKA",
            "settings.json");
        _settingsStore = new JsonSettingsStore(settingsPath);
        _recordingService.StatusChanged += (_, message) => Dispatcher.Invoke(() =>
        {
            SetStatus(message);
            UpdateUi();
        });
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
            if (SynchronizeSelectedDevicesWithSettings())
            {
                await _settingsStore.SaveAsync(_settings);
            }
            await LoadRecentClipsAsync();
            _hotkeys = new GlobalHotkeyService();
            RegisterHotkeys();

            if (_settings.StartReplayBufferWithApp)
            {
                await _recordingService.StartReplayBufferAsync(_settings);
                _captureNotifications.Show(
                    CaptureNotificationKind.Replay,
                    "Replay готов",
                    $"В фоне сохраняются последние {_settings.ReplaySeconds} секунд");
            }

            UpdateUi();
            SetStatus("Готово. Проверьте выбранные устройства перед первой записью.");
            _replayTargetTimer.Start();
        }
        catch (Exception exception)
        {
            ShowError("Не удалось запустить CLIPALKA", exception);
        }
        finally { UpdateUi(); _replayTargetTimer.Start(); }
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e) => await ToggleRecordingAsync();

    private async Task ToggleRecordingAsync()
    {
        await RunUiActionAsync(async () =>
        {
            var wasRecording = _recordingService.IsRecording;
            var outputPath = await _recordingService.ToggleRecordingAsync(_settings);
            if (wasRecording)
            {
                _captureNotifications.StopRecordingIndicator();
                _captureNotifications.Show(
                    CaptureNotificationKind.Success,
                    "Запись остановлена и сохранена",
                    string.IsNullOrWhiteSpace(outputPath)
                        ? "Видео сохранено в выбранную папку."
                        : Path.GetFileName(outputPath));
                await LoadRecentClipsAsync();
            }
            else
            {
                _captureNotifications.StartRecordingIndicator();
                _captureNotifications.Show(
                    CaptureNotificationKind.Recording,
                    "Запись началась",
                    _recordingService.RecordingCaptureName is { Length: > 0 } name
                        ? $"Источник: {name}"
                        : "CLIPALKA записывает игру и звук");
            }
            UpdateUi();
        });
    }

    private async void ReplayButton_Click(object sender, RoutedEventArgs e) => await SaveReplayAsync();

    private async Task SaveReplayAsync()
    {
        await RunUiActionAsync(async () =>
        {
            ReplayButton.IsEnabled = false;
            SetStatus("Сохраняю последние 30 секунд…");
            _captureNotifications.Show(
                CaptureNotificationKind.Replay,
                "Сохраняем момент…",
                $"Подготавливаем последние {_settings.ReplaySeconds} секунд");
            var outputPath = await _recordingService.SaveReplayAsync(_settings);
            _captureNotifications.Show(
                CaptureNotificationKind.Success,
                "Replay сохранён",
                string.IsNullOrWhiteSpace(outputPath)
                    ? "Последние 30 секунд сохранены."
                    : Path.GetFileName(outputPath));
            await LoadRecentClipsAsync();
            UpdateUi();
        });
    }

    private void MicrophoneButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _recordingService.SetMicrophoneMuted(!_recordingService.IsMicrophoneMuted);
            _captureNotifications.Show(
                CaptureNotificationKind.Microphone,
                _recordingService.IsMicrophoneMuted ? "Микрофон выключен" : "Микрофон включён",
                _recordingService.IsMicrophoneMuted ? "Голос не попадёт в запись" : "Голос снова записывается");
            UpdateUi();
        }
        catch (Exception error) { ShowError("Не удалось переключить микрофон", error); }
    }

    private async void ToggleReplayButton_Click(object sender, RoutedEventArgs e) => await RunUiActionAsync(async () =>
    {
        if (_recordingService.IsReplayBuffering) await _recordingService.StopReplayBufferAsync();
        else await _recordingService.StartReplayBufferAsync(_settings);
    });

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
            if (_recordingService.IsDiagnosing) throw new InvalidOperationException("Сначала завершите проверку записи.");
            if (_recordingService.IsRecording) throw new InvalidOperationException("Остановите запись перед изменением настроек.");
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
            await LoadRecentClipsAsync();
            _settingsDirty = false;
            SetStatus("Настройки сохранены");
        });
    }

    private async void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsButton.IsEnabled = false;
        try
        {
            await RunUiActionAsync(async () =>
            {
                if (_recordingService.IsDiagnosing)
                {
                    await _recordingService.StopDiagnosticsAsync();
                    OpenDiagnosticDirectory();
                    return;
                }
                if (System.Windows.MessageBox.Show(this,
                    "Проверка сохранит экран, отдельный голос и системный звук в папку Diagnostics рядом с клипами. Нужны 3 ГБ свободного места. Ничего не отправляется автоматически.\n\nПереключитесь в игру, играйте и говорите около минуты, затем сохраните replay обычным хоткеем. Через 90 секунд replay остановится. Начать?",
                    "Диагностика записи", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
                ApplyFormToSettings();
                await _recordingService.StartDiagnosticsAsync(_settings);
                _captureNotifications.Show(CaptureNotificationKind.Recording, "Проверка включена", "Экран и отдельный голос сохраняются локально. До 90 секунд.");
            });
        }
        finally { DiagnosticsButton.IsEnabled = true; UpdateUi(); }
    }

    private void OpenDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        try { OpenDiagnosticDirectory(); }
        catch (Exception error) { ShowError("Не удалось открыть диагностику", error); }
    }

    private void OpenDiagnosticDirectory()
    {
        var path = _recordingService.LastDiagnosticDirectory ?? Path.Combine(_settings.OutputDirectory, "Diagnostics");
        if (!Directory.Exists(path)) throw new InvalidOperationException("Сначала запустите проверку записи.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        if (_isClosing)
        {
            return;
        }

        e.Cancel = true;
        _isClosing = true;
        IsEnabled = false;
        SetStatus("Завершаю записи…");
        _replayTargetTimer.Stop();
        _hotkeys?.Dispose();
        try { await _recordingService.DisposeAsync(); }
        catch (Exception error)
        {
            System.Windows.MessageBox.Show(this, "Не удалось корректно завершить одну из записей. Проверьте последний клип.\n" + error.Message,
                "Завершение CLIPALKA", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _captureNotifications.Dispose();
        _trayNotifications.Dispose();
        Closing -= Window_Closing;
        Close();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        SystemCommands.MinimizeWindow(this);

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void DismissErrorButton_Click(object sender, RoutedEventArgs e) =>
        ErrorBanner.Visibility = Visibility.Collapsed;

    private void DashboardNavButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(DashboardPage, DashboardNavButton);

    private async void ClipsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(AudioPage, ClipsNavButton);
        await RunUiActionAsync(LoadRecentClipsAsync);
    }

    private void VideoNavButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(VideoPage, VideoNavButton);

    private void HotkeysNavButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(HotkeysPage, HotkeysNavButton);

    private void ShowPage(FrameworkElement page, Button selectedNavigationButton)
    {
        foreach (var candidate in new[] { DashboardPage, VideoPage, AudioPage, HotkeysPage })
        {
            candidate.Visibility = ReferenceEquals(candidate, page) ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var button in new[] { DashboardNavButton, ClipsNavButton, VideoNavButton, HotkeysNavButton })
        {
            var isSelected = ReferenceEquals(button, selectedNavigationButton);
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? "#34266D" : "#00000000"));
            button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? "#FFFFFF" : "#A4AFC2"));
            button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? "#8B5CF6" : "#00000000"));
            button.BorderThickness = isSelected ? new Thickness(5, 0, 0, 0) : new Thickness(0);
        }
    }

    private void OpenClipsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var outputDirectory = RecordingPathService.EnsureOutputDirectory(_settings.OutputDirectory);
            Process.Start(new ProcessStartInfo(outputDirectory) { UseShellExecute = true });
        }
        catch (Exception error) { ShowError("Не удалось открыть папку", error); }
    }

    private void OpenClip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filePath } || !File.Exists(filePath))
        {
            return;
        }

        try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); }
        catch (Exception error) { ShowError("Не удалось открыть клип", error); }
    }

    private void RevealClip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || !File.Exists(path)) return;
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            start.ArgumentList.Add("/select,");
            start.ArgumentList.Add(path);
            Process.Start(start);
        }
        catch (Exception error) { ShowError("Не удалось открыть папку клипа", error); }
    }

    private async void RefreshClips_Click(object sender, RoutedEventArgs e) => await RunUiActionAsync(LoadRecentClipsAsync);
    private void ClipSearch_TextChanged(object sender, TextChangedEventArgs e) { if (GalleryItemsControl is not null) FilterGallery(); }
    private void FilterGallery()
    {
        var query = ClipSearchTextBox.Text.Trim();
        var clips = _galleryClips.Where(clip => string.IsNullOrEmpty(query) || clip.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        GalleryItemsControl.ItemsSource = clips;
        GalleryEmptyPanel.Visibility = clips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        GalleryCountText.Text = $"Клипов: {clips.Count} · последние 100 записей";
    }

    private async Task LoadRecentClipsAsync()
    {
        var outputDirectory = _settings.OutputDirectory;
        _galleryClips = await Task.Run(() => RecentClipService.Load(outputDirectory, 100));
        RecentClipsItemsControl.ItemsSource = _galleryClips.Take(4).ToList();
        RecentClipsEmptyText.Visibility = _galleryClips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CapturePreviewImage.Source = _galleryClips.FirstOrDefault()?.Thumbnail;
        PreviewEmptyPanel.Visibility = _galleryClips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PreviewCaptionText.Text = _galleryClips.Count == 0 ? "Предпросмотр появится после сохранения клипа" : "Последний сохранённый клип · " + _galleryClips[0].Title;
        FilterGallery();
    }

    private async void ReplayTargetTimer_Tick(object? sender, EventArgs e)
    {
        UpdateUi();
        if (_isBusy || _isRefreshingReplayTarget || _isClosing || !_recordingService.IsReplayBuffering)
        {
            return;
        }

        _isRefreshingReplayTarget = true;
        try
        {
            await _recordingService.RefreshReplayTargetAsync(_settings);
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось обновить источник replay: {exception.Message}");
        }
        finally
        {
            _isRefreshingReplayTarget = false;
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyWindowScale(e.NewSize);
    }

    private double ApplyWindowScale(Size size)
    {
        var scale = Math.Clamp(Math.Min(size.Width / 1585, size.Height / 992), 0.7, 1);
        ShellRoot.LayoutTransform = new ScaleTransform(scale, scale);
        System.Windows.Shell.WindowChrome.GetWindowChrome(this).CaptionHeight = 70 * scale;
        return scale;
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        if (_backgroundHintShown)
        {
            return;
        }

        _backgroundHintShown = true;
        _trayNotifications.ShowInfo(
            "CLIPALKA работает в фоне",
            "Откройте приложение двойным кликом по значку в трее.");
    }

    private void RequestExit()
    {
        _exitRequested = true;
        RestoreFromTray();
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
        _formReady = false;
        OutputPathTextBox.Text = _settings.OutputDirectory;
        FpsComboBox.SelectedValue = _settings.FramesPerSecond.ToString();
        RecordHotkeyTextBox.Text = _settings.RecordHotkey;
        ReplayHotkeyTextBox.Text = _settings.ReplayHotkey;
        MicrophoneHotkeyTextBox.Text = _settings.MicrophoneHotkey;
        GalleryHotkeyTextBox.Text = _settings.GalleryHotkey;
        StartReplayCheckBox.IsChecked = _settings.StartReplayBufferWithApp;
        AutoGameCheckBox.IsChecked = _settings.AutoCaptureGame;
        GameOnlyHotkeysCheckBox.IsChecked = _settings.HotkeysOnlyWhileGameActive;
        SelectDevice(DisplayComboBox, _settings.DisplayDeviceName);
        SelectDevice(OutputDeviceComboBox, _settings.OutputAudioDeviceId);
        SelectDevice(InputDeviceComboBox, _settings.InputAudioDeviceId);
        OutputVolumeSlider.Value = _settings.OutputVolumePercent;
        MicrophoneVolumeSlider.Value = _settings.MicrophoneVolumePercent;
        _formReady = true;
        _settingsDirty = false;
    }

    private void SettingsFieldChanged(object sender, RoutedEventArgs e)
    {
        if (!_formReady) return;
        _settingsDirty = true;
        SaveAllSettingsButton.Content = "Сохранить изменения";
        SaveAllSettingsButton.BorderBrush = (Brush)FindResource("AccentHoverBrush");
    }

    private void ApplyFormToSettings()
    {
        ValidateHotkey(RecordHotkeyTextBox.Text, "записи");
        ValidateHotkey(ReplayHotkeyTextBox.Text, "replay");
        ValidateHotkey(MicrophoneHotkeyTextBox.Text, "микрофона");
        ValidateHotkey(GalleryHotkeyTextBox.Text, "галереи");
        var bindings = new[] { RecordHotkeyTextBox.Text, ReplayHotkeyTextBox.Text, MicrophoneHotkeyTextBox.Text, GalleryHotkeyTextBox.Text }
            .Select(value => { HotkeyBinding.TryParse(value, out var binding); return binding?.ToString(); }).Where(value => value is not null).ToList();
        if (bindings.Distinct(StringComparer.OrdinalIgnoreCase).Count() != bindings.Count)
            throw new InvalidOperationException("У каждого действия должен быть свой хоткей. Escape отключает выбранный хоткей.");

        _settings.OutputDirectory = RecordingPathService.EnsureOutputDirectory(OutputPathTextBox.Text);
        _settings.FramesPerSecond = int.TryParse(FpsComboBox.SelectedValue?.ToString(), out var fps) ? fps : 60;
        _settings.DisplayDeviceName = (DisplayComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.OutputAudioDeviceId = (OutputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.InputAudioDeviceId = (InputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        _settings.OutputVolumePercent = (int)Math.Round(OutputVolumeSlider.Value);
        _settings.MicrophoneVolumePercent = (int)Math.Round(MicrophoneVolumeSlider.Value);
        _settings.RecordHotkey = RecordHotkeyTextBox.Text.Trim();
        _settings.ReplayHotkey = ReplayHotkeyTextBox.Text.Trim();
        _settings.MicrophoneHotkey = MicrophoneHotkeyTextBox.Text.Trim();
        _settings.GalleryHotkey = GalleryHotkeyTextBox.Text.Trim();
        _settings.StartReplayBufferWithApp = StartReplayCheckBox.IsChecked == true;
        _settings.AutoCaptureGame = AutoGameCheckBox.IsChecked == true;
        _settings.HotkeysOnlyWhileGameActive = GameOnlyHotkeysCheckBox.IsChecked == true;
    }

    private bool SynchronizeSelectedDevicesWithSettings()
    {
        var selectedDisplay = (DisplayComboBox.SelectedItem as DeviceOption)?.Id;
        var selectedOutput = (OutputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        var selectedInput = (InputDeviceComboBox.SelectedItem as DeviceOption)?.Id;
        var changed = _settings.DisplayDeviceName != selectedDisplay ||
                      _settings.OutputAudioDeviceId != selectedOutput ||
                      _settings.InputAudioDeviceId != selectedInput;
        _settings.DisplayDeviceName = selectedDisplay;
        _settings.OutputAudioDeviceId = selectedOutput;
        _settings.InputAudioDeviceId = selectedInput;
        return changed;
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
            () => Dispatcher.BeginInvoke(new Action(() => _ = ToggleRecordingAsync())),
            CanExecuteGlobalHotkey);
        _hotkeys.Register(
            ReplayHotkeyId,
            replayBinding,
            () => Dispatcher.BeginInvoke(new Action(() => _ = SaveReplayAsync())),
            CanExecuteGlobalHotkey);
        HotkeyBinding.TryParse(_settings.MicrophoneHotkey, out var microphoneBinding);
        HotkeyBinding.TryParse(_settings.GalleryHotkey, out var galleryBinding);
        _hotkeys.Register(MicrophoneHotkeyId, microphoneBinding,
            () => Dispatcher.BeginInvoke(new Action(() => MicrophoneButton_Click(this, new RoutedEventArgs()))), CanExecuteGlobalHotkey);
        _hotkeys.Register(GalleryHotkeyId, galleryBinding,
            () => Dispatcher.BeginInvoke(new Action(() => { RestoreFromTray(); ClipsNavButton_Click(this, new RoutedEventArgs()); })),
            () => !_isBusy && !IsEditingHotkey());
    }

    private void UpdateUi()
    {
        SaveAllSettingsButton.Content = _settingsDirty ? "Сохранить изменения" : "Сохранить настройки";
        SaveAllSettingsButton.BorderBrush = (Brush)FindResource(_settingsDirty ? "AccentHoverBrush" : "BorderBrush");
        var recording = _recordingService.IsRecording;
        RecordButton.IsEnabled = !_isBusy && !_recordingService.IsDiagnosing;
        SaveAllSettingsButton.IsEnabled = !_isBusy && !recording && !_recordingService.IsDiagnosing;
        ToggleReplayButton.IsEnabled = !_isBusy;
        ToggleReplayButton.Content = _recordingService.IsReplayBuffering ? "Остановить replay" : "Включить replay";
        CaptureQualityText.Text = $"H.264  •  {_settings.FramesPerSecond} FPS  •  Stereo";
        RecordStartGlyph.Visibility = recording ? Visibility.Collapsed : Visibility.Visible;
        RecordStopGlyph.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsButton.Content = _recordingService.IsDiagnosing ? "Остановить проверку" : "Начать проверку (90 секунд)";
        DisplayComboBox.IsEnabled = FpsComboBox.IsEnabled = OutputDeviceComboBox.IsEnabled = InputDeviceComboBox.IsEnabled = !_recordingService.IsDiagnosing;
        RecordButtonTitle.Text = recording ? _recordingService.RecordingElapsed.ToString(@"hh\:mm\:ss") : "Запись";
        RecordButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            _recordingService.IsRecording ? "#402331" : "#151B27"));
        MicrophoneButtonTitle.Text = "Микрофон";
        MicrophoneStateText.Text = _recordingService.IsMicrophoneMuted ? "Выключен" : "Включён";
        MicrophoneStateText.Foreground = _recordingService.IsMicrophoneMuted ? (Brush)FindResource("DangerBrush") : (Brush)FindResource("MutedBrush");
        MicrophoneIcon.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            _recordingService.IsMicrophoneMuted ? "#F04461" : "#F5F7FC"));
        ReplayStatusText.Text = _recordingService.IsReplayBuffering ? "CLIPALKA работает" : "Replay выключен";
        if (recording) ReplayStatusText.Text = "Идёт запись";
        if (_recordingService.IsDiagnosing) ReplayStatusText.Text = "● Диагностика включена";
        ReplayStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            recording ? "#FF506C" : _recordingService.IsReplayBuffering ? "#36D399" : "#64748B"));
        ReplaySourceText.Text = recording ? _recordingService.RecordingCaptureName ?? "Запись"
            : !_recordingService.IsReplayBuffering
            ? "Источник не выбран"
            : _recordingService.ReplayCaptureName ?? "Источник определяется";
        CaptureStateText.Text = recording ? "Идёт запись · " + _recordingService.RecordingCaptureName : _recordingService.IsReplayBuffering ? "Захват активен" : "Ожидание захвата";
        CaptureStateText.Foreground = CaptureStateDot.Fill = (Brush)FindResource(recording ? "DangerBrush" : _recordingService.IsReplayBuffering ? "SuccessBrush" : "SubtleBrush");
        CaptureDescriptionText.Text = recording ? "Нажми квадрат, чтобы остановить и сохранить" : !_recordingService.IsReplayBuffering ? "Replay-буфер выключен" : _recordingService.IsReplayCapturingApplication ? "Игра запущена и отслеживается" : "Записывается выбранный монитор";
        ReplayButton.IsEnabled = _recordingService.IsReplayBuffering && !_isBusy;
        RecordHotkeyHint.Text = string.IsNullOrWhiteSpace(_settings.RecordHotkey) ? "Хоткей выключен" : _settings.RecordHotkey.Replace("+", " + ");
        ReplayHotkeyHint.Text = string.IsNullOrWhiteSpace(_settings.ReplayHotkey) ? "Хоткей выключен" : _settings.ReplayHotkey.Replace("+", " + ");
        MicrophoneHotkeyHint.Text = string.IsNullOrWhiteSpace(_settings.MicrophoneHotkey) ? "Без хоткея" : _settings.MicrophoneHotkey.Replace("+", " + ");
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        if (_isBusy || _isClosing) return;
        _isBusy = true;
        UpdateUi();
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
            _isBusy = false;
            UpdateUi();
        }
    }

    private void SetStatus(string message) => StatusTextBlock.Text = message;

    private void ShowError(string title, Exception exception)
    {
        SetStatus(exception.Message);
        ErrorBannerText.Text = exception.Message;
        ErrorBanner.Visibility = Visibility.Visible;
        _captureNotifications.Show(CaptureNotificationKind.Error, title, exception.Message);
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

        if (key is Key.Escape)
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

        textBox.Text = formattedBinding;
        SetStatus($"Хоткей выбран: {binding}. Нажмите «Сохранить настройки».");
    }

    private bool CanExecuteGlobalHotkey()
    {
        if (_isBusy || IsEditingHotkey())
        {
            return false;
        }

        if (!_settings.HotkeysOnlyWhileGameActive || _captureTargets.IsLikelyGameActive())
        {
            return true;
        }

        SetStatus("Хоткей проигнорирован: активного игрового окна нет");
        return false;
    }

    private bool IsEditingHotkey() => IsActive &&
        (RecordHotkeyTextBox.IsKeyboardFocusWithin || ReplayHotkeyTextBox.IsKeyboardFocusWithin ||
         MicrophoneHotkeyTextBox.IsKeyboardFocusWithin || GalleryHotkeyTextBox.IsKeyboardFocusWithin);

    private static void ValidateHotkey(string value, string purpose)
    {
        if (!HotkeyBinding.TryParse(value, out var binding))
        {
            throw new InvalidOperationException($"Некорректный хоткей {purpose}.");
        }

    }

    private static string NormalizeStoredHotkey(string value)
    {
        if (!HotkeyBinding.TryParse(value, out var binding) || binding is null)
        {
            return value;
        }

        return binding.ToString();
    }

    private static void SelectDevice(System.Windows.Controls.ComboBox comboBox, string? id)
    {
        var items = comboBox.ItemsSource?.Cast<DeviceOption>().ToList() ?? [];
        var selectedId = DeviceSelectionPolicy.SelectAvailableId(
            items.Select(item => new SelectableDevice(item.Id, item.IsDefault)).ToList(),
            id);
        comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == selectedId);
    }
}
