using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WinHyperisland
{
    public sealed class MediaManager : IDisposable
    {
        public record MediaInfo(
            string Title,
            string Artist,
            BitmapImage? AlbumArt,
            bool IsPlaying,
            string SourceAppId);

        public event Action<MediaInfo>? OnMediaPlaying;
        public event Action? OnMediaPaused;
        public event Action? OnMediaStopped;

        private readonly Dispatcher _dispatcher;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private System.Threading.Timer? _stopDebounceTimer;
        private bool _disposed;

        public GlobalSystemMediaTransportControlsSession? CurrentSession => _currentSession;

        public MediaManager(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public async Task InitializeAsync()
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager is null) return;
            _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
            AttachToCurrentSession();
        }

        private void SessionManager_SessionsChanged(
            GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(() => AttachToCurrentSession());
        }

        private void AttachToCurrentSession()
        {
            if (_currentSession is not null)
            {
                _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
            }

            try { _currentSession = _sessionManager?.GetCurrentSession(); }
            catch { _currentSession = null; }

            if (_currentSession is null) { FireMediaStopped(); return; }

            _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged += Session_PlaybackInfoChanged;
            HandlePlaybackInfoChanged();
        }

        private void Session_MediaPropertiesChanged(
            GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(async () => await UpdateMediaPropertiesAsync());
        }

        private void Session_PlaybackInfoChanged(
            GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            _dispatcher.InvokeAsync(() => HandlePlaybackInfoChanged());
        }

        private void HandlePlaybackInfoChanged()
        {
            GlobalSystemMediaTransportControlsSessionPlaybackInfo? info;
            try { info = _currentSession?.GetPlaybackInfo(); }
            catch { FireMediaStopped(); return; }

            if (info is null) { FireMediaStopped(); return; }

            switch (info.PlaybackStatus)
            {
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing:
                    CancelStopDebounce();
                    _ = UpdateMediaPropertiesAsync();
                    break;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused:
                    CancelStopDebounce();
                    OnMediaPaused?.Invoke();
                    break;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped:
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed:
                    StartStopDebounce();
                    break;
            }
        }

        private async Task UpdateMediaPropertiesAsync()
        {
            if (_currentSession is null) return;

            GlobalSystemMediaTransportControlsSessionMediaProperties? props;
            try { props = await _currentSession.TryGetMediaPropertiesAsync(); }
            catch { return; }
            if (props is null) return;

            string title = props.Title ?? string.Empty;
            string artist = props.Artist ?? string.Empty;
            string sourceAppId = _currentSession.SourceAppUserModelId ?? string.Empty;
            BitmapImage? albumArt = null;

            try
            {
                var thumbRef = props.Thumbnail;
                if (thumbRef is not null)
                {
                    using var stream = await thumbRef.OpenReadAsync();
                    var memStream = new MemoryStream();
                    using (var dNetStream = stream.AsStreamForRead())
                    {
                        await dNetStream.CopyToAsync(memStream);
                    }
                    memStream.Position = 0;

                    albumArt = new BitmapImage();
                    albumArt.BeginInit();
                    albumArt.CacheOption = BitmapCacheOption.OnLoad;
                    albumArt.StreamSource = memStream;
                    albumArt.EndInit();
                    albumArt.Freeze();
                }
            }
            catch { albumArt = null; }

            bool isPlaying = true;
            try
            {
                var pb = _currentSession.GetPlaybackInfo();
                isPlaying = pb?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            }
            catch { }

            OnMediaPlaying?.Invoke(new MediaInfo(title, artist, albumArt, isPlaying, sourceAppId));
        }

        private void StartStopDebounce()
        {
            CancelStopDebounce();
            _stopDebounceTimer = new System.Threading.Timer(
                _ => _dispatcher.InvokeAsync(() => FireMediaStopped()),
                null, TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
        }

        private void CancelStopDebounce()
        {
            _stopDebounceTimer?.Dispose();
            _stopDebounceTimer = null;
        }

        private void FireMediaStopped()
        {
            CancelStopDebounce();
            OnMediaStopped?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelStopDebounce();
            if (_currentSession is not null)
            {
                _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
            }
            if (_sessionManager is not null)
                _sessionManager.SessionsChanged -= SessionManager_SessionsChanged;
            _currentSession = null;
            _sessionManager = null;
        }
    }
}
