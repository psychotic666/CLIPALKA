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
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowAppearance.Apply(this);
    }

    // Render the production visual tree on a Windows runner; fixture values never enter normal startup.
    public void RenderUiSamples(string directory)
    {
        Directory.CreateDirectory(directory);
        GreetingText.Text = "Лови момент.";
        _settings.FramesPerSecond = 120;
        _settings.RecordHotkey = "Shift+R";
        _settings.ReplayHotkey = "Shift+Z";
        DisplayComboBox.ItemsSource = new[] { new DeviceOption("test", "AF24H1") };
        OutputDeviceComboBox.ItemsSource = new[] { new DeviceOption("test", "SteelSeries Sonar — Gaming") };
        InputDeviceComboBox.ItemsSource = new[] { new DeviceOption("test", "SteelSeries Sonar — Microphone") };
        PopulateForm();
        RecordHotkeyHint.Text = _settings.RecordHotkey.Replace("+", " + ");
        ReplayHotkeyHint.Text = _settings.ReplayHotkey.Replace("+", " + ");
        ReplaySourceText.Text = "Rocket League";
        CaptureStateText.Text = "Буфер готов · 00:30";
        CaptureDescriptionText.Text = "Игра запущена и отслеживается";
        CaptureStateDot.Fill = (Brush)FindResource("SuccessBrush");
        CaptureStateText.Foreground = (Brush)FindResource("SuccessBrush");
        ReplayStatusText.Text = "Replay активен";
        ReplayStatusDot.Fill = (Brush)FindResource("SuccessBrush");
        ToggleReplayButton.Content = "Остановить replay";
        CaptureQualityText.Text = "120 FPS";
        AudioSummaryText.Text = "Аудио: Sonar";
        MicrophoneHotkeyHint.Text = "Shift + X";
        var thumbnail = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/studio-demo.png"));
        CapturePreviewBrush.ImageSource = thumbnail;
        PreviewEmptyPanel.Visibility = Visibility.Collapsed;
        PreviewCaptionText.Text = "Rocket League";
        LatestClipMetadataText.Text = "Сегодня, 21:47 · 00:30";
        var titles = new[] { "Решающий гол", "Эффектное спасение", "Удачный пас" };
        _galleryClips = Enumerable.Range(0, 9).Select(i =>
            new RecentClipItem("", titles[i % 3], "Сегодня, " + new TimeOnly(21, 47).AddMinutes(-i * 7).ToString("HH:mm"), "0:30", thumbnail)).ToList();
        OpenLatestClipButton.IsEnabled = true;
        LatestClipDurationText.Text = "00:30";
        RecentClipsItemsControl.ItemsSource = _galleryClips.Take(3).ToList();
        FilterGallery();
        RecentClipsEmptyText.Visibility = Visibility.Collapsed;
        var root = (FrameworkElement)Content;
        Content = null;
        foreach (var (name, size, page) in new[]
        {
            ("ui-1585", new Size(1585, 992), "home"),
            ("ui-1280", new Size(1280, 800), "home"),
            ("ui-compact", new Size(1180, 760), "home"),
            ("ui-narrow", new Size(1024, 768), "home"),
            ("ui-150dpi", new Size(1280, 800), "home"),
            ("ui-gallery", new Size(1280, 800), "gallery"),
            ("ui-diagnostics", new Size(1280, 800), "diagnostics"),
            ("ui-startup", new Size(1280, 800), "startup"),
            ("ui-audio", new Size(1280, 800), "audio"),
            ("ui-recording", new Size(1280, 800), "recording"),
            ("ui-muted", new Size(1280, 800), "muted"),
            ("ui-error", new Size(1280, 800), "error"),
            ("ui-empty", new Size(1280, 800), "empty"),
            ("ui-replay-off", new Size(1280, 800), "replay-off"),
            ("ui-controller", new Size(1280, 800), "controller")
        })
        {
            ShowPage(page == "controller" ? HotkeysPage : page == "audio" ? SoundPage : page == "gallery" ? AudioPage : page is "diagnostics" or "startup" ? VideoPage : DashboardPage,
                page == "controller" ? HotkeysNavButton : page == "gallery" ? ClipsNavButton : page is "diagnostics" or "startup" ? VideoNavButton : DashboardNavButton);
            ErrorBanner.Visibility = Visibility.Collapsed;
            RecordStartGlyph.Visibility = Visibility.Visible;
            RecordStopGlyph.Visibility = Visibility.Collapsed;
            RecordButtonTitle.Text = "Начать запись";
            RecordHotkeyHint.Text = "Shift + R";
            RecordHotkeyHint.FontSize = 13;
            GreetingText.Text = "Лови момент.";
            RecordButton.BorderBrush = (Brush)FindResource("BorderBrush");
            RecordButton.Background = (Brush)FindResource("StudioSurfaceBrush");
            MicrophoneButtonTitle.Text = "Микрофон";
            MicrophoneIcon.Stroke = (Brush)FindResource("SuccessBrush");
            MicrophoneLevelMeter.Level = .09;
            MicrophoneLevelMeter.IsMuted = false;
            MicrophoneLevelMeter.IsAvailable = true;
            CaptureStateText.Text = "Буфер готов · 00:30";
            CaptureStateText.Foreground = CaptureStateDot.Fill = (Brush)FindResource("SuccessBrush");
            LatestClipLabel.Visibility = Visibility.Visible;
            CapturePreviewBrush.ImageSource = thumbnail;
            PreviewEmptyPanel.Visibility = Visibility.Collapsed;
            PreviewCaptionText.Text = "Rocket League";
            LatestClipMetadataText.Text = "Сегодня, 21:47 · 00:30";
            LatestClipDurationText.Text = "00:30";
            OpenLatestClipButton.IsEnabled = true;
            RecentClipsItemsControl.ItemsSource = _galleryClips.Take(3).ToList();
            RecentClipsEmptyText.Visibility = Visibility.Collapsed;
            ReplayButton.IsEnabled = true;
            ToggleReplayButton.Content = "Остановить replay";
            ReplayStatusText.Text = "Replay активен";
            ReplayStatusDot.Fill = (Brush)FindResource("SuccessBrush");
            if (page == "replay-off")
            {
                CaptureStateText.Text = "Буфер выключен";
                CaptureStateText.Foreground = CaptureStateDot.Fill = (Brush)FindResource("SubtleBrush");
                ReplayStatusText.Text = "Replay выключен";
                ReplayStatusDot.Fill = (Brush)FindResource("SubtleBrush");
                ToggleReplayButton.Content = "Включить replay";
                ReplayButton.IsEnabled = false;
            }
            if (page == "recording")
            {
                RecordStartGlyph.Visibility = Visibility.Collapsed;
                RecordStopGlyph.Visibility = Visibility.Visible;
                RecordButtonTitle.Text = "Остановить";
                RecordHotkeyHint.Text = "02:14";
                RecordHotkeyHint.FontSize = 20;
                GreetingText.Text = "Запись идёт.";
                RecordButton.BorderBrush = (Brush)FindResource("DangerBrush");
                RecordButton.Background = (Brush)FindResource("DangerSurfaceBrush");
                ReplayStatusText.Text = "Запись · Rocket League";
                ReplayStatusDot.Fill = (Brush)FindResource("DangerBrush");
            }
            if (page == "muted")
            {
                MicrophoneButtonTitle.Text = "Выключен";
                MicrophoneIcon.Stroke = (Brush)FindResource("MutedBrush");
                MicrophoneLevelMeter.Level = 0;
                MicrophoneLevelMeter.IsMuted = true;
            }
            if (page == "error")
            {
                ErrorBanner.Visibility = Visibility.Visible;
                ErrorBannerTitle.Text = "Микрофон недоступен";
                ErrorBannerText.Text = "Проверь подключение устройства и выбери доступный микрофон в разделе «Аудио».";
            }
            if (page == "controller")
            {
                ControllerComboBox.ItemsSource = new[] { new ControllerDevice("test", "Wireless Controller (DualSense)"), new ControllerDevice("test-flydigi", "Flydigi (XInput)") };
                ControllerComboBox.SelectedIndex = 0;
                ControllerBindingText.Text = "Wireless Controller · Create + R1";
                ControllerStatusText.Text = "Сочетание сохранено · нажми один раз";
                ControllerInputText.Text = "Кнопки отпущены";
            }
            if (page == "empty")
            {
                CapturePreviewBrush.ImageSource = null;
                PreviewEmptyPanel.Visibility = Visibility.Visible;
                PreviewCaptionText.Text = "Предпросмотр появится после сохранения клипа";
                RecentClipsItemsControl.ItemsSource = null;
                RecentClipsEmptyText.Visibility = Visibility.Visible;
                OpenLatestClipButton.IsEnabled = false;
                LatestClipLabel.Visibility = Visibility.Collapsed;
                LatestClipMetadataText.Text = "";
                LatestClipDurationText.Text = "—";
                PreviewCaptionText.Text = "";
            }
            var scale = ApplyWindowScale(size);
            root.Width = size.Width / scale;
            root.Height = size.Height / scale;
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            if (page == "startup")
            {
                VideoPage.ScrollToVerticalOffset(360);
                root.UpdateLayout();
            }
            var hintBottom = RecordHotkeyHint.TransformToAncestor(CaptureActionsGrid)
                .Transform(new Point(0, RecordHotkeyHint.ActualHeight)).Y;
            if ((page is "home" or "empty" or "recording" or "error") && (hintBottom > CaptureActionsGrid.ActualHeight || Math.Abs(ReplayButton.ActualHeight - CaptureActionsGrid.Height) > 0.1))
                throw new InvalidOperationException("Capture action layout clips its controls.");
            if (page is "home" or "empty" or "recording" or "error")
            {
                var hintBaselines = new[] { RecordHotkeyHint, ReplayHotkeyHint, MicrophoneHotkeyHint }
                    .Select(hint => hint.TransformToAncestor(CaptureActionsGrid).Transform(new Point(0, hint.ActualHeight)).Y).ToArray();
                if ((size.Width < 1400 && page != "recording" && hintBaselines.Max() - hintBaselines.Min() > 2) || hintBaselines.Max() > CaptureActionsGrid.ActualHeight)
                    throw new InvalidOperationException($"Action shortcuts must share a visible baseline at every window size: {name} [{string.Join(", ", hintBaselines)}]");
            }
            if (name == "ui-1585")
            {
                var clipsBottom = RecentClipsItemsControl.TransformToAncestor(DashboardPage)
                    .Transform(new Point(0, RecentClipsItemsControl.ActualHeight)).Y;
                if (clipsBottom > DashboardPage.ActualHeight - 6
                    || DashboardPage.ExtentHeight > DashboardPage.ViewportHeight + 1)
                    throw new InvalidOperationException("Studio capture and recent clips must fit the default viewport.");
            }
            var dpi = name == "ui-150dpi" ? 144d : 96d;
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(size.Width * dpi / 96), (int)(size.Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, name + ".png"));
            encoder.Save(file);
        }
        foreach (var (name, kind, title, message) in new[]
        {
            ("toast-replay", CaptureNotificationKind.Replay, "Replay сохранён", "Rocket League · последние 30 секунд"),
            ("toast-recording", CaptureNotificationKind.Recording, "Запись началась", "Rocket League · нажми хоткей, чтобы остановить"),
            ("toast-success", CaptureNotificationKind.Success, "Запись сохранена", "Твой момент уже в папке с клипами"),
            ("toast-error", CaptureNotificationKind.Error, "Не удалось сохранить клип", "Проверь свободное место в папке записей")
        })
        {
            var toast = new CaptureNotificationWindow(kind, title, message);
            RenderDetachedWindow(toast, name, directory);
            toast.Close();
        }
        var indicator = new RecordingIndicatorWindow();
        RenderDetachedWindow(indicator, "recording-indicator", directory);
        indicator.Close();
        VerifyKeyboardAndDevicePopup(root, directory);
        TrayMenu.RenderSamples(directory);
        _trayNotifications.Dispose();
        _captureNotifications.Dispose();
    }

    private void VerifyKeyboardAndDevicePopup(FrameworkElement root, string directory)
    {
        // Host only the detached visual tree: normal app startup/capture is never run.
        var workArea = SystemParameters.WorkArea;
        var size = new Size(Math.Min(1280, workArea.Width - 24), Math.Min(800, workArea.Height - 24));
        ApplyWindowScale(size);
        root.Width = size.Width;
        root.Height = size.Height;
        ErrorBanner.Visibility = Visibility.Collapsed;
        ShowPage(DashboardPage, DashboardNavButton);
        var host = new Window { Content = root, Width = size.Width, Height = size.Height,
            Left = workArea.Left + 12, Top = workArea.Top + 12,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        try
        {
            host.Show();
            host.Activate();
            host.UpdateLayout();
            Keyboard.Focus(ReplayButton);
            ReplayButton.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            if (!RecordButton.IsKeyboardFocused)
                throw new InvalidOperationException("Tab must reach recording from the replay action.");
            host.UpdateLayout();
            SaveVisual(root, "ui-keyboard", directory);

            // Hover must cover the whole control, including its content padding.
            foreach (var button in new[] { DashboardNavButton, ReplayButton })
            {
                button.ApplyTemplate();
                var hover = (Border)button.Template.FindName("Hover", button);
                if (hover.ActualWidth < button.ActualWidth - 4 || hover.ActualHeight < button.ActualHeight - 4)
                    throw new InvalidOperationException("Hover must fill the rounded button rather than its padded content rectangle.");
            }
            ShowPage(AudioPage, ClipsNavButton);
            host.UpdateLayout();
            SaveVisual(root, "ui-gallery-interaction", directory);

            ShowPage(HotkeysPage, HotkeysNavButton);
            ControllerComboBox.Focus();
            ControllerComboBox.IsDropDownOpen = true;
            host.UpdateLayout();
            var popup = (System.Windows.Controls.Primitives.Popup)ControllerComboBox.Template.FindName("PART_Popup", ControllerComboBox);
            var popupVisual = (FrameworkElement)popup.Child;
            popupVisual.UpdateLayout();
            if (ControllerComboBox.ItemContainerGenerator.ContainerFromIndex(0) is not ComboBoxItem { IsSelected: true })
                throw new InvalidOperationException("Device popup must expose its selected item.");
            SaveVisual(popupVisual, "device-popup", directory);
            ControllerComboBox.IsDropDownOpen = false;
            File.WriteAllText(Path.Combine(directory, "interaction-verification.txt"),
                "PASS: Tab reaches recording from replay; navigation and action hover layers fill their rounded controls; selected controller is exposed by real ComboBoxItem; native popup rendered.");
        }
        finally
        {
            ControllerComboBox.IsDropDownOpen = false;
            host.Content = null;
            host.Close();
        }
    }

    private static void SaveVisual(FrameworkElement visual, string name, string directory)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(file);
    }

    private static void RenderDetachedWindow(Window window, string name, string directory)
    {
        var visual = (FrameworkElement)window.Content;
        window.Content = null;
        var size = new Size(window.Width, window.Height);
        visual.Width = size.Width;
        visual.Height = size.Height;
        visual.Measure(size);
        visual.Arrange(new Rect(size));
        visual.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(file);
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
    private readonly MicrophoneLevelService _microphoneLevels = new();
    private readonly DispatcherTimer _microphoneLevelTimer;
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
    private ControllerHotkeyService? _controllerHotkeys;
    private ControllerBinding? _pendingControllerBinding;
    private long _controllerPermissionCheckedAt;
    private bool _controllerGameActive;

    public MainWindow()
    {
        InitializeComponent();
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 24);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);
        GreetingText.Text = "Лови момент.";
        ShowPage(DashboardPage, DashboardNavButton);
        foreach (var field in new[] { OutputPathTextBox, RecordHotkeyTextBox, ReplayHotkeyTextBox, MicrophoneHotkeyTextBox, GalleryHotkeyTextBox })
            field.TextChanged += SettingsFieldChanged;
        foreach (var field in new[] { DisplayComboBox, FpsComboBox, OutputDeviceComboBox, InputDeviceComboBox })
            field.SelectionChanged += SettingsFieldChanged;
        OutputVolumeSlider.ValueChanged += SettingsFieldChanged;
        MicrophoneVolumeSlider.ValueChanged += SettingsFieldChanged;
        foreach (var field in new[] { AutoGameCheckBox, StartReplayCheckBox, StartWithWindowsCheckBox, GameOnlyHotkeysCheckBox, ControllerGameOnlyCheckBox })
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
        _microphoneLevelTimer = new DispatcherTimer(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(50) };
        _microphoneLevelTimer.Tick += (_, _) => UpdateMicrophoneLevel();
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
            try { _hotkeys = new GlobalHotkeyService(); }
            catch (Exception error) { ShowError("Клавиатурные хоткеи недоступны", error); }
            RegisterHotkeys();
            InitializeControllerHotkeys();

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
        finally { UpdateUi(); _replayTargetTimer.Start(); _microphoneLevelTimer.Start(); }
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
            if (_controllerHotkeys?.IsLearning == true) throw new InvalidOperationException("Сначала отпусти кнопки или отмени назначение контроллера.");
            if (_recordingService.IsRecording) throw new InvalidOperationException("Остановите запись перед изменением настроек.");
            var previous = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(_settings))!;
            ApplyFormToSettings();
            RecordingPathService.EnsureOutputDirectory(_settings.OutputDirectory);
            new WindowsStartupService().SetEnabled(StartWithWindowsCheckBox.IsChecked == true);
            await _settingsStore.SaveAsync(_settings);
            _controllerHotkeys?.Configure(_settings.ReplayControllerBinding);
            RegisterHotkeys();

            if (_recordingService.IsReplayBuffering)
            {
                if (CaptureSettingsPolicy.RequiresReplayRestart(previous, _settings))
                {
                    await _recordingService.StopReplayBufferAsync();
                    await _recordingService.StartReplayBufferAsync(_settings);
                }
                else await _recordingService.UpdateAudioLevelsAsync(_settings);
            }
            else if (_settings.StartReplayBufferWithApp)
            {
                await _recordingService.StartReplayBufferAsync(_settings);
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
        _microphoneLevelTimer.Stop();
        _microphoneLevels.Dispose();
        _hotkeys?.Dispose();
        _controllerHotkeys?.Dispose();
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
        ShowPage(VideoPage, HotkeysNavButton);

    private void AudioSettingsNavButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(SoundPage, HotkeysNavButton);

    private void ControlsNavButton_Click(object sender, RoutedEventArgs e) =>
        ShowPage(HotkeysPage, HotkeysNavButton);

    private void ShowPage(FrameworkElement page, Button selectedNavigationButton)
    {
        var settings = page == VideoPage || page == SoundPage || page == HotkeysPage;
        SettingsShell.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
        foreach (var candidate in new[] { DashboardPage, VideoPage, AudioPage, SoundPage, HotkeysPage })
        {
            candidate.Visibility = ReferenceEquals(candidate, page) ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var button in new[] { DashboardNavButton, ClipsNavButton, HotkeysNavButton })
        {
            var isSelected = button == HotkeysNavButton ? settings : button == DashboardNavButton ? page == DashboardPage : page == AudioPage;
            button.Tag = isSelected ? "Selected" : null;
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, isSelected ? "Выбрано" : "");
        }
        foreach (var (button, view) in new[] { (VideoNavButton, (FrameworkElement)VideoPage), (AudioSettingsNavButton, SoundPage), (ControlsNavButton, HotkeysPage) })
            button.Tag = ReferenceEquals(page, view) ? "Selected" : null;
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
        RecentClipsItemsControl.ItemsSource = _galleryClips.Take(3).ToList();
        RecentClipsEmptyText.Visibility = _galleryClips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CapturePreviewBrush.ImageSource = _galleryClips.FirstOrDefault()?.Thumbnail;
        PreviewEmptyPanel.Visibility = _galleryClips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LatestClipLabel.Visibility = _galleryClips.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        PreviewCaptionText.Text = _galleryClips.FirstOrDefault()?.Title ?? "";
        LatestClipMetadataText.Text = _galleryClips.Count == 0 ? "" : _galleryClips[0].SavedAt;
        OpenLatestClipButton.Tag = _galleryClips.FirstOrDefault()?.FilePath;
        OpenLatestClipButton.IsEnabled = _galleryClips.Count > 0;
        LatestClipDurationText.Text = _galleryClips.FirstOrDefault()?.Duration ?? "—";
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
        // Adapt the desktop layout without shrinking the visual tree.
        ShellRoot.LayoutTransform = Transform.Identity;
        var compact = size.Width < 1400;
        NavigationStrip.Width = Math.Max(340, Math.Min(520, size.Width - 410));
        GreetingText.FontSize = compact ? 36 : 44;
        GreetingText.LineHeight = double.NaN;
        DashboardHeadingGrid.Height = compact ? 52 : 68;
        CapturePreviewBorder.Height = Math.Clamp(size.Height - (compact ? 574 : 600), 160, 430);
        CaptureActionsGrid.Height = compact ? 72 : 60;
        var actionFontSize = size.Width < 1100 ? 16 : 19;
        RecordButtonTitle.FontSize = ReplayButtonTitle.FontSize = actionFontSize;
        MicrophoneButtonTitle.FontSize = actionFontSize;
        ReplayHotkeyHint.FontSize = MicrophoneHotkeyHint.FontSize = 13;
        MicrophoneLevelMeter.Width = size.Width < 1100 ? 64 : 84;
        foreach (var (layout, chip) in new[] { (ReplayActionLayout, ReplayShortcutChip), (RecordActionLayout, RecordShortcutChip) })
        {
            layout.RowDefinitions[1].Height = new GridLength(compact ? 26 : 0);
            Grid.SetRow(chip, compact ? 1 : 0);
            Grid.SetColumn(chip, compact ? 0 : 1);
            chip.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            chip.Margin = compact ? new Thickness(0, 2, 0, 0) : new Thickness(12, 0, 0, 0);
            chip.BorderThickness = compact ? new Thickness(0) : new Thickness(1);
            chip.Padding = compact ? new Thickness(0) : new Thickness(8, 4, 8, 4);
        }
        MicrophoneActionLayout.RowDefinitions[1].Height = new GridLength(compact ? 26 : 0);
        MicrophoneHotkeyHint.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Shell.WindowChrome.GetWindowChrome(this).CaptionHeight = 48;
        return 1;
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
        _pendingControllerBinding = _settings.ReplayControllerBinding;
        UpdateControllerBindingText();
        StartReplayCheckBox.IsChecked = _settings.StartReplayBufferWithApp;
        StartWithWindowsCheckBox.IsChecked = new WindowsStartupService().IsEnabled();
        AutoGameCheckBox.IsChecked = _settings.AutoCaptureGame;
        GameOnlyHotkeysCheckBox.IsChecked = _settings.HotkeysOnlyWhileGameActive;
        ControllerGameOnlyCheckBox.IsChecked = _settings.ControllerHotkeysOnlyWhileGameActive;
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
        _settings.ReplayControllerBinding = _pendingControllerBinding;
        _settings.StartReplayBufferWithApp = StartReplayCheckBox.IsChecked == true;
        _settings.AutoCaptureGame = AutoGameCheckBox.IsChecked == true;
        _settings.HotkeysOnlyWhileGameActive = GameOnlyHotkeysCheckBox.IsChecked == true;
        _settings.ControllerHotkeysOnlyWhileGameActive = ControllerGameOnlyCheckBox.IsChecked == true;
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
        _trayNotifications.UpdateState(recording, _recordingService.IsReplayBuffering, _isBusy || _recordingService.IsDiagnosing,
            _settings.RecordHotkey, _settings.ReplayHotkey);
        RecordButton.IsEnabled = !_isBusy && !_recordingService.IsDiagnosing;
        SaveAllSettingsButton.IsEnabled = !_isBusy && !recording && !_recordingService.IsDiagnosing && _controllerHotkeys?.IsLearning != true;
        LearnControllerButton.Content = _controllerHotkeys?.IsLearning == true ? "Отменить назначение" : "Назначить сочетание";
        ControllerComboBox.IsEnabled = _controllerHotkeys?.IsLearning != true;
        ToggleReplayButton.IsEnabled = !_isBusy;
        ToggleReplayButton.Content = _recordingService.IsReplayBuffering ? "Остановить replay" : "Включить replay";
        CaptureQualityText.Text = $"{_settings.FramesPerSecond} FPS";
        SourceIdentityIcon.Data = (Geometry)FindResource(_recordingService.IsReplayCapturingApplication ? "GameIcon" : "CaptureIcon");
        RecordStartGlyph.Visibility = recording ? Visibility.Collapsed : Visibility.Visible;
        RecordStopGlyph.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsButton.Content = _recordingService.IsDiagnosing ? "Остановить проверку" : "Начать проверку (90 секунд)";
        DisplayComboBox.IsEnabled = FpsComboBox.IsEnabled = OutputDeviceComboBox.IsEnabled = InputDeviceComboBox.IsEnabled = !_recordingService.IsDiagnosing;
        RecordButtonTitle.Text = recording ? "Остановить" : "Начать запись";
        GreetingText.Text = recording ? "Запись идёт." : "Лови момент.";
        RecordButton.BorderBrush = (Brush)FindResource(recording ? "DangerBrush" : "BorderBrush");
        RecordButton.Background = (Brush)FindResource(recording ? "DangerSurfaceBrush" : "StudioSurfaceBrush");
        MicrophoneButtonTitle.Text = _recordingService.IsMicrophoneMuted ? "Выключен" : "Микрофон";
        MicrophoneButton.ToolTip = _recordingService.IsMicrophoneMuted ? "Включить микрофон" : "Выключить микрофон";
        MicrophoneIcon.Stroke = (Brush)FindResource(_recordingService.IsMicrophoneMuted ? "MutedBrush" : "SuccessBrush");
        MicrophoneLevelMeter.IsMuted = _recordingService.IsMicrophoneMuted;
        if (_recordingService.IsMicrophoneMuted) MicrophoneLevelMeter.Level = 0;
        System.Windows.Automation.AutomationProperties.SetItemStatus(MicrophoneButton, _recordingService.IsMicrophoneMuted ? "Микрофон выключен" : "Микрофон включён");
        System.Windows.Automation.AutomationProperties.SetName(RecordButton, recording ? "Остановить и сохранить запись" : "Начать запись");
        AudioSummaryText.Text = OutputDeviceComboBox.SelectedItem is DeviceOption output ? "Аудио: " + output.DisplayName : "Аудио: системное устройство";
        ReplayStatusText.Text = _recordingService.IsReplayBuffering ? "Replay активен" : "Replay выключен";
        if (recording) ReplayStatusText.Text = "Идёт запись";
        if (_recordingService.IsDiagnosing) ReplayStatusText.Text = "● Диагностика включена";
        ReplayStatusDot.Fill = (Brush)FindResource(recording ? "DangerBrush" : _recordingService.IsReplayBuffering ? "SuccessBrush" : "SubtleBrush");
        ReplaySourceText.Text = recording ? _recordingService.RecordingCaptureName ?? "Запись"
            : !_recordingService.IsReplayBuffering
            ? "Источник не выбран"
            : _recordingService.ReplayCaptureName ?? "Источник определяется";
        CaptureStateText.Text = _recordingService.IsReplayBuffering ? "Буфер активен" : "Буфер выключен";
        CaptureStateText.Foreground = CaptureStateDot.Fill = (Brush)FindResource(_recordingService.IsReplayBuffering ? "SuccessBrush" : "SubtleBrush");
        CaptureDescriptionText.Text = recording ? "Нажми квадрат, чтобы остановить и сохранить" : !_recordingService.IsReplayBuffering ? "Replay-буфер выключен" : _recordingService.IsReplayCapturingApplication ? "Игра запущена и отслеживается" : "Записывается выбранный монитор";
        ReplayButton.IsEnabled = _recordingService.IsReplayBuffering && !_isBusy;
        RecordHotkeyHint.Text = recording
            ? _recordingService.RecordingElapsed.ToString(_recordingService.RecordingElapsed.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss")
            : string.IsNullOrWhiteSpace(_settings.RecordHotkey) ? "Хоткей выключен" : _settings.RecordHotkey.Replace("+", " + ");
        RecordHotkeyHint.FontSize = recording ? 20 : 13;
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

    private void UpdateMicrophoneLevel()
    {
        if (_isClosing || !IsVisible || WindowState == WindowState.Minimized || DashboardPage.Visibility != Visibility.Visible)
        {
            _microphoneLevels.Suspend();
            MicrophoneLevelMeter.Level = 0;
            return;
        }
        var level = _microphoneLevels.Read(_settings.InputAudioDeviceId, _settings.MicrophoneVolumePercent, _recordingService.IsMicrophoneMuted);
        MicrophoneLevelMeter.Level = level.Peak;
        MicrophoneLevelMeter.IsAvailable = level.Available;
        MicrophoneLevelMeter.IsMuted = _recordingService.IsMicrophoneMuted;
        MicrophoneButton.ToolTip = level.Description + "\n" + (_recordingService.IsMicrophoneMuted ? "Нажми, чтобы включить" : "Нажми, чтобы выключить");
    }

    private void SetStatus(string message) => StatusTextBlock.Text = message;

    private void ShowError(string title, Exception exception)
    {
        SetStatus(exception.Message);
        ErrorBannerTitle.Text = title;
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

    private void InitializeControllerHotkeys()
    {
        // Controller failures must not prevent screen/audio capture from starting.
        try
        {
            _controllerHotkeys = new ControllerHotkeyService(Dispatcher, CanExecuteControllerHotkey,
                () => Dispatcher.BeginInvoke(new Action(() => _ = SaveReplayAsync())), GetControllerBlockReason);
            _controllerHotkeys.InputChanged += text => ControllerInputText.Text = text;
            _controllerHotkeys.TriggerRejected += reason =>
                _captureNotifications.Show(CaptureNotificationKind.Replay, "Клип не сохранён", reason);
            _controllerHotkeys.DevicesChanged += devices =>
            {
                var selected = ControllerComboBox.SelectedValue as string ?? _pendingControllerBinding?.DeviceId;
                ControllerComboBox.ItemsSource = devices;
                ControllerComboBox.SelectedValue = selected;
                if (ControllerComboBox.SelectedIndex < 0 && devices.Count > 0) ControllerComboBox.SelectedIndex = 0;
            };
            _controllerHotkeys.StatusChanged += text =>
            {
                ControllerStatusText.Text = text;
                LearnControllerButton.Content = _controllerHotkeys.IsLearning ? "Отменить назначение" : "Назначить сочетание";
                ControllerComboBox.IsEnabled = !_controllerHotkeys.IsLearning;
                SaveAllSettingsButton.IsEnabled = !_controllerHotkeys.IsLearning && !_isBusy && !_recordingService.IsRecording && !_recordingService.IsDiagnosing;
            };
            _controllerHotkeys.BindingCaptured += binding =>
            {
                _pendingControllerBinding = binding;
                UpdateControllerBindingText();
                SettingsFieldChanged(this, new RoutedEventArgs());
            };
            _controllerHotkeys.Configure(_settings.ReplayControllerBinding);
            _controllerHotkeys.RefreshDevices();
        }
        catch (Exception error)
        {
            _controllerHotkeys?.Dispose();
            _controllerHotkeys = null;
            ControllerStatusText.Text = "Контроллер недоступен: " + error.Message;
        }
    }

    private bool CanExecuteControllerHotkey() => GetControllerBlockReason() is null;

    private string? GetControllerBlockReason()
    {
        if (_isClosing) return "приложение закрывается";
        if (_isBusy) return "дождись завершения текущей операции";
        if (!_recordingService.IsReplayBuffering) return "replay выключен — сначала включи буфер";
        if (IsEditingHotkey()) return "закончи редактирование клавиатурного хоткея";
        if (!_settings.ControllerHotkeysOnlyWhileGameActive) return null;
        var now = Stopwatch.GetTimestamp();
        if (now - _controllerPermissionCheckedAt >= Stopwatch.Frequency / 5)
        {
            _controllerPermissionCheckedAt = now;
            _controllerGameActive = _captureTargets.IsLikelyGameActive();
        }
        return _controllerGameActive ? null : "игра не активна; ограничение геймпада включено в настройках";
    }

    private void LearnControllerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controllerHotkeys is null) InitializeControllerHotkeys();
        if (_controllerHotkeys is null) return;
        if (_controllerHotkeys.IsLearning) { _controllerHotkeys.CancelLearning(); UpdateUi(); return; }
        try
        {
            if (ControllerComboBox.SelectedValue is not string id) throw new InvalidOperationException("Подключи контроллер и выбери его в списке.");
            _controllerHotkeys.StartLearning(id);
        }
        catch (Exception error) { ShowError("Не удалось назначить кнопки", error); }
    }

    private void RefreshControllers_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_controllerHotkeys is null) InitializeControllerHotkeys();
            else _controllerHotkeys.RefreshDevices();
        }
        catch (Exception error) { ShowError("Ошибка обнаружения контроллера", error); }
    }

    private void OpenWindowsControllers_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("control.exe", "joy.cpl") { UseShellExecute = true }); }
        catch (Exception error) { ShowError("Не удалось открыть контроллеры Windows", error); }
    }

    private void CopyControllerDetails_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText((_controllerHotkeys?.DetectionDetails ?? "Input service unavailable") + "\n" +
                string.Join("\n", ControllerComboBox.Items.Cast<ControllerDevice>().Select(device => device.Name)));
            SetStatus("Сведения об обнаружении контроллеров скопированы");
        }
        catch (Exception error) { ShowError("Не удалось скопировать сведения", error); }
    }

    private void ClearControllerButton_Click(object sender, RoutedEventArgs e)
    {
        _controllerHotkeys?.CancelLearning();
        _pendingControllerBinding = null;
        UpdateControllerBindingText();
        SettingsFieldChanged(this, new RoutedEventArgs());
    }

    private void UpdateControllerBindingText() => ControllerBindingText.Text = _pendingControllerBinding is null
        ? "Не назначено" : _pendingControllerBinding.DeviceName + " · " + _pendingControllerBinding.Label;

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
