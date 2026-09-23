using System.Media;
using System.Windows.Threading;

namespace Clipalka.App.Services;

public enum CaptureNotificationKind
{
    Recording,
    Replay,
    Success,
    Microphone,
    Error
}

public sealed class CaptureNotificationService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private CaptureNotificationWindow? _currentWindow;
    private RecordingIndicatorWindow? _recordingIndicator;

    public CaptureNotificationService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Show(CaptureNotificationKind kind, string title, string message)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => Show(kind, title, message));
            return;
        }

        _currentWindow?.Dismiss();
        PlaySound(kind);

        var window = new CaptureNotificationWindow(kind, title, message);
        _currentWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_currentWindow, window))
            {
                _currentWindow = null;
            }
        };
        window.Show();
    }

    public void StartRecordingIndicator()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(StartRecordingIndicator);
            return;
        }

        _recordingIndicator?.Close();
        _recordingIndicator = new RecordingIndicatorWindow();
        _recordingIndicator.Show();
    }

    public void StopRecordingIndicator()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(StopRecordingIndicator);
            return;
        }

        _recordingIndicator?.Close();
        _recordingIndicator = null;
    }

    public void Dispose()
    {
        if (_dispatcher.CheckAccess())
        {
            _currentWindow?.Dismiss();
            _recordingIndicator?.Close();
        }
        else
        {
            _dispatcher.Invoke(() =>
            {
                _currentWindow?.Dismiss();
                _recordingIndicator?.Close();
            });
        }
        _currentWindow = null;
        _recordingIndicator = null;
    }

    private static void PlaySound(CaptureNotificationKind kind)
    {
        switch (kind)
        {
            case CaptureNotificationKind.Error:
                SystemSounds.Hand.Play();
                break;
            case CaptureNotificationKind.Success:
            case CaptureNotificationKind.Recording:
                SystemSounds.Asterisk.Play();
                break;
        }
    }
}
