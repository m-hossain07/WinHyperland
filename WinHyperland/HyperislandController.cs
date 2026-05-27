using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Rectangle = System.Windows.Shapes.Rectangle;
using Windows.UI.ViewManagement;

namespace WinHyperisland
{
    public enum HyperislandState { Idle, Compact, Expanded, Notification }

    public sealed class HyperislandController
    {
        private readonly MainWindow _window;
        private readonly SettingsService _settings;
        private HyperislandState _currentState = HyperislandState.Idle;
        public HyperislandState CurrentState => _currentState;

        private bool _mediaActive;
        private MediaManager.MediaInfo? _lastMediaInfo;
        private NotificationInfo? _lastNotifInfo;

        private readonly DispatcherTimer _notifTimer;
        private readonly DispatcherTimer _holdTimer;
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _cursorTracker;
        private readonly DispatcherTimer _pausedCollapseTimer;
        private readonly MediaManager _mediaManager;
        private WeatherManager? _weatherManager;
        private readonly IslandAppManager _appManager = new();
        private bool _isDraggingSlider;
        private bool _cursorOverPill;

        // Waveform storyboards
        private Storyboard? _wave1Sb, _wave2Sb, _wave3Sb;
        private Storyboard? _expWave1Sb, _expWave2Sb, _expWave3Sb, _expWave4Sb, _expWave5Sb;

        public SettingsService Settings => _settings;
        public bool MediaActive => _mediaActive;
        public IslandAppManager AppManager => _appManager;

        public HyperislandController(MainWindow window, MediaManager mediaManager, SettingsService settings)
        {
            _window = window;
            _mediaManager = mediaManager;
            _settings = settings;

            // Subscribe to settings change
            _settings.SettingsChanged += OnSettingsChanged;

            // Notification auto-dismiss timer
            _notifTimer = new DispatcherTimer();
            _notifTimer.Tick += (_, _) =>
            {
                _notifTimer.Stop();
                if (_mediaActive && _settings.MediaIntegrationEnabled)
                    TransitionTo(HyperislandState.Compact);
                else
                    TransitionTo(HyperislandState.Idle);
            };

            // Long-press timer (500ms hold → Expanded)
            _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _holdTimer.Tick += (_, _) =>
            {
                _holdTimer.Stop();
                if (_currentState == HyperislandState.Compact)
                    TransitionTo(HyperislandState.Expanded);
            };

            // Progress tracking timer
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _progressTimer.Tick += ProgressTimer_Tick;

            // Cursor tracking timer — replaces unreliable MouseEnter/MouseLeave
            // on transparent click-through windows
            _cursorTracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            _cursorTracker.Tick += CursorTracker_Tick;
            _cursorTracker.Start();

            // Paused collapse timer
            _pausedCollapseTimer = new DispatcherTimer();
            _pausedCollapseTimer.Tick += (s, e) =>
            {
                _pausedCollapseTimer.Stop();
                if (!_mediaActive || _currentState == HyperislandState.Notification) return;

                // Check playback state again
                try
                {
                    var pbInfo = _mediaManager.CurrentSession?.GetPlaybackInfo();
                    if (pbInfo?.PlaybackStatus != Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        _mediaActive = false;
                        _appManager.Deactivate(IslandApp.Media);
                        _progressTimer.Stop();
                        if (_appManager.IsActive(IslandApp.Weather))
                            TransitionTo(HyperislandState.Compact);
                        else
                            TransitionTo(HyperislandState.Idle);
                    }
                }
                catch { }
            };

            SetupPointerEvents();
            SetupTransportButtons(mediaManager);

            // Wire media events
            mediaManager.OnMediaPlaying += OnMediaPlaying;
            mediaManager.OnMediaPaused += OnMediaPaused;
            mediaManager.OnMediaStopped += () =>
            {
                _mediaActive = false;
                _appManager.Deactivate(IslandApp.Media);
                _progressTimer.Stop();
                StopWaveform();
                if (_appManager.IsActive(IslandApp.Weather))
                    TransitionTo(HyperislandState.Compact);
                else
                    TransitionTo(HyperislandState.Idle);
            };

            BuildWaveformAnimations();

            // Wire app switching
            _appManager.OnActiveAppChanged += OnActiveAppChanged;

            // Apply initial settings immediately
            _window.Dispatcher.BeginInvoke(new Action(() => {
                _window.ApplyPositionSettings(_settings);
                ApplyAppearanceSettings();
            }));
        }

        // ─── Weather integration ────────────────────────────

        public void SetWeatherManager(WeatherManager weatherManager)
        {
            _weatherManager = weatherManager;
            _weatherManager.OnWeatherUpdated += OnWeatherUpdated;

            if (_settings.WeatherEnabled)
            {
                _appManager.Activate(IslandApp.Weather);
            }
        }

        private void OnWeatherUpdated(WeatherInfo info)
        {
            if (!_settings.WeatherEnabled) return;

            double temp = info.Temperature;
            string unit = "C";
            if (_settings.TemperatureUnit == "F")
            {
                temp = Math.Round(info.Temperature * 9.0 / 5.0 + 32);
                unit = "F";
            }

            // Update compact weather UI
            _window.CompactWeatherIcon.Text = info.Icon;
            _window.CompactWeatherTemp.Text = $"{temp}°{unit}";

            // Update expanded weather UI
            _window.ExpandedWeatherIcon.Text = info.Icon;
            _window.ExpandedWeatherTemp.Text = $"{temp}°";
            _window.ExpandedWeatherCity.Text = info.City;
            _window.ExpandedWeatherDesc.Text = info.Description;

            double highTemp = _settings.TemperatureUnit == "F"
                ? Math.Round(info.HighTemp * 9.0 / 5.0 + 32) : info.HighTemp;
            double lowTemp = _settings.TemperatureUnit == "F"
                ? Math.Round(info.LowTemp * 9.0 / 5.0 + 32) : info.LowTemp;
            _window.ExpandedWeatherHighLow.Text = $"H:{highTemp}° L:{lowTemp}°";

            double feelsLike = _settings.TemperatureUnit == "F"
                ? Math.Round(info.FeelsLike * 9.0 / 5.0 + 32) : info.FeelsLike;
            _window.ExpandedWeatherFeelsLike.Text = $"{feelsLike}°";
            _window.ExpandedWeatherHumidity.Text = $"{info.Humidity}%";
            _window.ExpandedWeatherWind.Text = $"{info.WindSpeed} km/h";

            // Tint the icon color based on weather
            _window.CompactWeatherIcon.Foreground = GetWeatherBrush(info.WeatherCode, info.IsDay);
            _window.ExpandedWeatherIcon.Foreground = GetWeatherBrush(info.WeatherCode, info.IsDay);

            // If currently idle and weather is only active app, show weather compact
            if (_currentState == HyperislandState.Idle && !_mediaActive && _settings.WeatherEnabled)
            {
                TransitionTo(HyperislandState.Compact);
            }

            UpdateDotIndicators();
        }

        private SolidColorBrush GetWeatherBrush(int code, bool isDay)
        {
            return code switch
            {
                0 or 1 when isDay => new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 163, 23)),   // Golden sun
                0 or 1 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 190, 220)),              // Moonlight
                2 or 3 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 175, 200)),              // Cloudy
                45 or 48 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(140, 150, 170)),            // Fog
                >= 51 and <= 67 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 160, 230)),     // Rain
                >= 71 and <= 77 or 85 or 86 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 220, 240)), // Snow
                >= 80 and <= 82 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 160, 230)),     // Rain showers
                95 or 96 or 99 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 130, 255)),      // Thunder
                _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200))                     // Default
            };
        }

        // ─── Multi-app switching ────────────────────────────

        private void OnActiveAppChanged(IslandApp app)
        {
            _window.Dispatcher.BeginInvoke(() =>
            {
                UpdateViewsForCurrentApp();
                UpdateDotIndicators();
            });
        }

        public void CycleApp()
        {
            _appManager.CycleNext();
        }

        private void UpdateViewsForCurrentApp()
        {
            if (_currentState == HyperislandState.Notification) return;

            var app = _appManager.CurrentApp;

            if (_currentState == HyperislandState.Compact)
            {
                CrossfadeView(_window.CompactView, app == IslandApp.Media);
                CrossfadeView(_window.WeatherCompactView, app == IslandApp.Weather);
            }
            else if (_currentState == HyperislandState.Expanded)
            {
                CrossfadeView(_window.ExpandedView, app == IslandApp.Media);
                CrossfadeView(_window.WeatherExpandedView, app == IslandApp.Weather);
            }
        }

        private void UpdateDotIndicators()
        {
            bool showDots = _appManager.HasMultipleApps &&
                            (_currentState == HyperislandState.Compact || _currentState == HyperislandState.Expanded);

            _window.AppSwitchDots.Visibility = showDots ? Visibility.Visible : Visibility.Collapsed;

            if (showDots)
            {
                var current = _appManager.CurrentApp;
                bool mediaActive = _appManager.IsActive(IslandApp.Media);
                bool weatherActive = _appManager.IsActive(IslandApp.Weather);

                _window.Dot_Media.Visibility = mediaActive ? Visibility.Visible : Visibility.Collapsed;
                _window.Dot_Weather.Visibility = weatherActive ? Visibility.Visible : Visibility.Collapsed;

                _window.Dot_Media.Fill = current == IslandApp.Media
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
                _window.Dot_Weather.Fill = current == IslandApp.Weather
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
            }
        }

        // ─── Cursor tracking (replaces MouseEnter/MouseLeave) ──

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_LBUTTON = 0x01;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private void CursorTracker_Tick(object? sender, EventArgs e)
        {
            if (!GetCursorPos(out POINT pt)) return;

            var pillRect = _window.GetPillScreenRect();
            bool isOver = pillRect.Contains(new System.Windows.Point(pt.X, pt.Y));

            if (isOver && !_cursorOverPill)
            {
                // Cursor entered pill
                _cursorOverPill = true;
                if ((_currentState == HyperislandState.Compact || _currentState == HyperislandState.Idle) && _settings.HoverToExpand)
                    TransitionTo(HyperislandState.Expanded);
            }
            else if (!isOver)
            {
                if (_cursorOverPill)
                {
                    // Cursor left pill
                    _cursorOverPill = false;
                    if (_currentState == HyperislandState.Expanded && !_isDraggingSlider && _settings.HoverToExpand)
                    {
                        if (_mediaActive)
                            TransitionTo(HyperislandState.Compact);
                        else if (_appManager.IsActive(IslandApp.Weather))
                            TransitionTo(HyperislandState.Compact);
                        else
                            TransitionTo(HyperislandState.Idle);
                    }
                }
                else if (_currentState == HyperislandState.Expanded)
                {
                    // If we're expanded, check if user clicked outside
                    // GetAsyncKeyState most significant bit is set if key is down, least significant if pressed since last call
                    if ((GetAsyncKeyState(VK_LBUTTON) & 0x8001) != 0)
                    {
                        if (_settings.ClickOutsideToCollapse)
                        {
                            TransitionTo(_mediaActive ? HyperislandState.Compact : HyperislandState.Idle);
                        }
                    }
                }
            }
        }

        // ─── Pointer events ────────────────────────────────

        private void SetupPointerEvents()
        {
            var pill = _window.HyperislandPill;

            // Mouse down handling: double-click to toggle mini pill when idle, long-press on Compact
            pill.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2 && _currentState == HyperislandState.Idle)
                {
                    _settings.UseMiniPillWhenIdle = !_settings.UseMiniPillWhenIdle;
                    _settings.NotifyChanged();
                    e.Handled = true;
                }
                else if ((_currentState == HyperislandState.Compact || _currentState == HyperislandState.Idle) && !_settings.HoverToExpand)
                {
                    _holdTimer.Start();
                }
            };

            // Unified mouse up handling on the pill container
            pill.PreviewMouseLeftButtonUp += async (s, e) =>
            {
                _holdTimer.Stop();

                // ─── Prevent App Launch on Transport Button Click ───
                if (e.OriginalSource is DependencyObject hit)
                {
                    var button = FindVisualAncestor<System.Windows.Controls.Button>(hit);
                    if (button != null)
                    {
                        // User clicked a transport control (Next/Prev/Play), do not launch app!
                        return;
                    }
                }

                if (_currentState == HyperislandState.Compact || _currentState == HyperislandState.Idle)
                {
                    if (_currentState == HyperislandState.Idle)
                    {
                        // Always expand on click when Idle, even if HoverToExpand is enabled
                        TransitionTo(HyperislandState.Expanded);
                    }
                    else if (_appManager.HasMultipleApps)
                    {
                        // If multiple apps active, single click cycles between them
                        CycleApp();
                    }
                    else if (_settings.HoverToExpand)
                    {
                        if (_settings.ClickToOpenApp && _lastMediaInfo?.SourceAppId is not null)
                        {
                            try
                            {
                                await Windows.System.Launcher.LaunchUriAsync(
                                    new Uri($"shell:AppsFolder\\{_lastMediaInfo.SourceAppId}"));
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // If hover expand is false and we're Compact, click to expand immediately
                        TransitionTo(HyperislandState.Expanded);
                    }
                }
                else if (_currentState == HyperislandState.Expanded)
                {
                    if (_appManager.HasMultipleApps)
                    {
                        // Cycle between apps in expanded view too
                        CycleApp();
                    }
                    else if (_settings.ClickToOpenApp && _mediaActive && _lastMediaInfo?.SourceAppId is not null)
                    {
                        try
                        {
                            await Windows.System.Launcher.LaunchUriAsync(
                                new Uri($"shell:AppsFolder\\{_lastMediaInfo.SourceAppId}"));
                        }
                        catch { }
                    }
                }
                else if (_currentState == HyperislandState.Notification)
                {
                    // Click to dismiss notification — return to previous app state
                    _notifTimer.Stop();
                    if (_mediaActive || _appManager.IsActive(IslandApp.Weather))
                        TransitionTo(HyperislandState.Compact);
                    else
                        TransitionTo(HyperislandState.Idle);
                }
            };

            // Slider interaction (scrubbing)
            _window.ExpandedProgress.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                new System.Windows.Input.MouseButtonEventHandler(Slider_DragStarted), true);
            _window.ExpandedProgress.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,
                new System.Windows.Input.MouseButtonEventHandler(Slider_DragCompleted), true);
        }

        private static T? FindVisualAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void Slider_DragStarted(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingSlider = true;
        }

        private async void Slider_DragCompleted(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var session = _mediaManager.CurrentSession;
                if (session != null)
                {
                    var timeline = session.GetTimelineProperties();
                    if (timeline != null)
                    {
                        var total = timeline.EndTime - timeline.StartTime;
                        double pct = _window.ExpandedProgress.Value;
                        var newPos = timeline.StartTime + TimeSpan.FromSeconds(total.TotalSeconds * (pct / 100.0));
                        await session.TryChangePlaybackPositionAsync((long)newPos.Ticks);
                    }
                }
            }
            catch { }
            _isDraggingSlider = false;
        }

        private void SetupTransportButtons(MediaManager mediaManager)
        {
            _window.OnPlayPauseClicked += async () =>
            {
                try
                {
                    if (mediaManager.CurrentSession is not null)
                        await mediaManager.CurrentSession.TryTogglePlayPauseAsync();
                }
                catch { }
            };

            _window.OnPreviousClicked += async () =>
            {
                try
                {
                    if (mediaManager.CurrentSession is not null)
                        await mediaManager.CurrentSession.TrySkipPreviousAsync();
                }
                catch { }
            };

            _window.OnNextClicked += async () =>
            {
                try
                {
                    if (mediaManager.CurrentSession is not null)
                        await mediaManager.CurrentSession.TrySkipNextAsync();
                }
                catch { }
            };
        }

        // ─── Media callbacks ───────────────────────────────

        private void OnMediaPlaying(MediaManager.MediaInfo info)
        {
            if (!_settings.MediaIntegrationEnabled) return;
            if (_settings.ExcludedApps.Contains(info.SourceAppId ?? "", StringComparer.OrdinalIgnoreCase))
                return;

            _lastMediaInfo = info;
            _mediaActive = true;
            _appManager.Activate(IslandApp.Media);
            _appManager.SwitchTo(IslandApp.Media);
            _pausedCollapseTimer.Stop();
            UpdateMediaUI(info);
            _window.PlayPauseButton.Content = "\xE769"; // Pause icon

            if (_currentState == HyperislandState.Idle || _currentState == HyperislandState.Compact)
            {
                if (_settings.ExpandOnMediaChange)
                {
                    TransitionTo(HyperislandState.Expanded);
                    var tempTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    tempTimer.Tick += (s, e) =>
                    {
                        tempTimer.Stop();
                        if (_currentState == HyperislandState.Expanded && !_cursorOverPill && !_isDraggingSlider)
                        {
                            TransitionTo(HyperislandState.Compact);
                        }
                    };
                    tempTimer.Start();
                }
                else
                {
                    TransitionTo(HyperislandState.Compact);
                }
            }

            StartWaveform();
            ApplyKeylineTint();
            _progressTimer.Start();
        }

        private void OnMediaPaused()
        {
            if (!_settings.MediaIntegrationEnabled) return;

            _window.PlayPauseButton.Content = "\xE768"; // Play icon
            StopWaveform();
            _progressTimer.Stop();

            _pausedCollapseTimer.Interval = TimeSpan.FromSeconds(_settings.MediaPausedCollapseDelay);
            _pausedCollapseTimer.Stop();
            _pausedCollapseTimer.Start();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (!_mediaActive || _window.Visibility != Visibility.Visible || _currentState != HyperislandState.Expanded || _isDraggingSlider)
                return;

            UpdateTimelineProgress();
        }

        private void UpdateTimelineProgress()
        {
            try
            {
                var session = _mediaManager.CurrentSession;
                if (session == null) return;

                var timeline = session.GetTimelineProperties();
                if (timeline == null) return;

                var pbInfo = session.GetPlaybackInfo();
                bool isPlaying = pbInfo?.PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var start = timeline.StartTime;
                var end = timeline.EndTime;
                var total = end - start;

                if (total.TotalSeconds <= 0)
                {
                    _window.ExpandedProgress.Value = 0;
                    _window.ExpandedElapsed.Text = "0:00";
                    _window.ExpandedTotal.Text = "0:00";
                    return;
                }

                var position = timeline.Position;
                if (isPlaying)
                {
                    var elapsedSinceUpdate = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                    position = timeline.Position + elapsedSinceUpdate;
                    if (position > end) position = end;
                    if (position < start) position = start;
                }

                double pct = (position - start).TotalSeconds / total.TotalSeconds * 100;
                _window.ExpandedProgress.Value = pct;

                _window.ExpandedElapsed.Text = FormatTimeSpan(position - start);
                _window.ExpandedTotal.Text = FormatTimeSpan(total);
            }
            catch { }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
            {
                return ts.ToString(@"h\:mm\:ss");
            }
            return ts.ToString(@"m\:ss");
        }

        private void UpdateMediaUI(MediaManager.MediaInfo? info)
        {
            if (info is null)
            {
                _window.CompactAlbumArt.Source = null;
                _window.ExpandedAlbumArt.Source = null;
                _window.ExpandedTitleText.Text = "";
                _window.ExpandedArtistText.Text = "";
                return;
            }

            _window.CompactAlbumArt.Source = info.AlbumArt;
            _window.ExpandedAlbumArt.Source = info.AlbumArt;
            _window.ExpandedTitleText.Text = info.Title;
            _window.ExpandedArtistText.Text = info.Artist;
        }

        // ─── Notification ──────────────────────────────────

        public void HandleNotification(NotificationInfo info)
        {
            if (!_settings.NotificationsEnabled) return;
            if (_settings.ExcludedApps.Contains(info.PackageFamilyName ?? "", StringComparer.OrdinalIgnoreCase) ||
                _settings.ExcludedApps.Contains(info.AppName ?? "", StringComparer.OrdinalIgnoreCase))
                return;

            _lastNotifInfo = info;

            // Update notification UI
            _window.NotificationIcon.Source = info.Icon;
            _window.NotificationAppName.Text = info.AppName;
            _window.NotificationBody.Text = string.IsNullOrEmpty(info.Title)
                ? info.Body
                : $"{info.Title} {info.Body}";

            // Dual activity logic
            if (_mediaActive)
            {
                TransitionTo(HyperislandState.Notification);
                _notifTimer.Interval = TimeSpan.FromSeconds(Math.Min(3, _settings.NotificationDuration));
                _notifTimer.Stop();
                _notifTimer.Start();
            }
            else
            {
                TransitionTo(HyperislandState.Notification);
                _notifTimer.Interval = TimeSpan.FromSeconds(_settings.NotificationDuration);
                _notifTimer.Stop();
                _notifTimer.Start();
            }
        }

        // ─── State transitions ─────────────────────────────

        public void TransitionTo(HyperislandState newState)
        {
            if (_currentState == newState) return;
            var oldState = _currentState;
            _currentState = newState;
            var (w, h, cr) = GetStateDims(newState);

            var (ease, dur) = GetAnimationParams();

            var widthAnim = new DoubleAnimation(w, dur);
            var heightAnim = new DoubleAnimation(h, dur);
            if (ease != null)
            {
                widthAnim.EasingFunction = ease;
                heightAnim.EasingFunction = ease;
            }

            _window.HyperislandPill.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            _window.HyperislandPill.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);

            // Corner radius
            AnimateCornerRadius(_window.HyperislandPill, cr, (int)dur.TotalMilliseconds);
            AnimateCornerRadius(_window.KeylineBorder, cr, (int)dur.TotalMilliseconds);

            // Determine which views to show based on active app
            var currentApp = _appManager.CurrentApp;

            CrossfadeView(_window.IdleView, newState == HyperislandState.Idle);

            // Compact views: show the right one based on active app
            if (newState == HyperislandState.Compact)
            {
                CrossfadeView(_window.CompactView, currentApp == IslandApp.Media || (!_appManager.IsActive(IslandApp.Weather) && _mediaActive));
                CrossfadeView(_window.WeatherCompactView, currentApp == IslandApp.Weather || (!_mediaActive && _appManager.IsActive(IslandApp.Weather)));
            }
            else
            {
                if (newState != HyperislandState.Compact)
                {
                    CrossfadeView(_window.CompactView, false);
                    CrossfadeView(_window.WeatherCompactView, false);
                }
            }

            // Expanded views: show the right one based on active app
            if (newState == HyperislandState.Expanded)
            {
                CrossfadeView(_window.ExpandedView, currentApp == IslandApp.Media || (!_appManager.IsActive(IslandApp.Weather) && _mediaActive));
                CrossfadeView(_window.WeatherExpandedView, currentApp == IslandApp.Weather || (!_mediaActive && _appManager.IsActive(IslandApp.Weather)));
            }
            else
            {
                if (newState != HyperislandState.Expanded)
                {
                    CrossfadeView(_window.ExpandedView, false);
                    CrossfadeView(_window.WeatherExpandedView, false);
                }
            }

            CrossfadeView(_window.NotificationView, newState == HyperislandState.Notification);

            // Dot indicators
            UpdateDotIndicators();

            // Keyline and Appearance Settings
            ApplyAppearanceSettings();

            if (newState == HyperislandState.Expanded && currentApp == IslandApp.Media)
            {
                UpdateTimelineProgress();
            }

            // Notification timer management
            if (newState != HyperislandState.Notification)
                _notifTimer.Stop();

            // Ensure window is visible for non-Idle states
            if (newState != HyperislandState.Idle)
                _window.Visibility = Visibility.Visible;
        }

        private void AnimateCornerRadius(Border border, double targetRadius, int durationMs)
        {
            var currentRadius = border.CornerRadius.TopLeft;
            if (durationMs <= 1)
            {
                border.CornerRadius = new CornerRadius(targetRadius);
                return;
            }

            var startTime = DateTime.UtcNow;
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var startVal = currentRadius;
            var endVal = targetRadius;

            void OnRendering(object? sender, EventArgs e)
            {
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed >= duration)
                {
                    border.CornerRadius = new CornerRadius(endVal);
                    CompositionTarget.Rendering -= OnRendering;
                    return;
                }
                double t = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                double val = startVal + (endVal - startVal) * t;
                border.CornerRadius = new CornerRadius(val);
            }

            CompositionTarget.Rendering += OnRendering;
        }

        private void CrossfadeView(UIElement view, bool show)
        {
            if (show)
            {
                view.Visibility = Visibility.Visible;
                if (_settings.ContentCrossfade)
                {
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(150)
                    };
                    view.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }
                else
                {
                    view.Opacity = 1;
                    view.BeginAnimation(UIElement.OpacityProperty, null);
                }
            }
            else
            {
                if (view.Visibility == Visibility.Visible && view.Opacity > 0)
                {
                    if (_settings.ContentCrossfade)
                    {
                        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
                        fadeOut.Completed += (_, _) => view.Visibility = Visibility.Collapsed;
                        view.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    }
                    else
                    {
                        view.Opacity = 0;
                        view.Visibility = Visibility.Collapsed;
                        view.BeginAnimation(UIElement.OpacityProperty, null);
                    }
                }
            }
        }

        // ─── Appearance and helper functions ───────────────

        public void ApplyAppearanceSettings()
        {
            try
            {
                // Pill Background Color
                var pillBrush = _window.HyperislandPill.Background as SolidColorBrush;
                if (pillBrush == null || pillBrush.IsFrozen)
                {
                    pillBrush = new SolidColorBrush(Colors.Black);
                    _window.HyperislandPill.Background = pillBrush;
                }

                if (_settings.UseCustomColor)
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.PillColor);
                    pillBrush.Color = color;
                }
                else
                {
                    pillBrush.Color = Colors.Black;
                }

                // Idle opacity
                if (_currentState == HyperislandState.Idle)
                {
                    _window.HyperislandPill.Opacity = _settings.PillOpacityWhenIdle / 100.0;
                }
                else
                {
                    _window.HyperislandPill.Opacity = 1.0;
                }

                // Keyline Tint
                if (!_settings.KeylineTintEnabled || _currentState == HyperislandState.Idle)
                {
                    AnimateKeylineToTransparent();
                }
                else
                {
                    ApplyKeylineTint();
                }

                // Waveform visible
                if (_window.WaveformContainer != null)
                {
                    _window.WaveformContainer.Visibility = _settings.WaveformBars ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private (double W, double H, double CR) GetStateDims(HyperislandState state)
        {
            var currentApp = _appManager.CurrentApp;
            return state switch
            {
                HyperislandState.Idle => _settings.UseMiniPillWhenIdle ? (80, 16, 8) : (126, 37, 18.5),
                HyperislandState.Compact => (280, 37, 18.5),
                HyperislandState.Expanded => currentApp == IslandApp.Weather ? (370, 190, 20) : (370, 160, 20),
                HyperislandState.Notification => (400, 64, 20),
                _ => (126, 37, 18.5)
            };
        }

        private (IEasingFunction? Ease, TimeSpan Duration) GetAnimationParams()
        {
            if (!_settings.PillMorphAnimation)
            {
                return (null, TimeSpan.FromMilliseconds(1));
            }

            return _settings.AnimationPreset switch
            {
                AnimationPreset.Island => (new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }, TimeSpan.FromMilliseconds(380)),
                AnimationPreset.Fluid => (new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut }, TimeSpan.FromMilliseconds(550)),
                AnimationPreset.Smooth => (new CubicEase { EasingMode = EasingMode.EaseOut }, TimeSpan.FromMilliseconds(350)),
                AnimationPreset.Snappy => (new BackEase { Amplitude = 0.1, EasingMode = EasingMode.EaseOut }, TimeSpan.FromMilliseconds(200)),
                AnimationPreset.Instant => (null, TimeSpan.FromMilliseconds(1)),
                _ => (new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }, TimeSpan.FromMilliseconds(380))
            };
        }

        private void OnSettingsChanged()
        {
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                _window.ApplyPositionSettings(_settings);
                ApplyAppearanceSettings();

                // Weather toggle
                if (_settings.WeatherEnabled)
                {
                    _appManager.Activate(IslandApp.Weather);
                    _ = _weatherManager?.RefreshAsync();
                }
                else
                {
                    _appManager.Deactivate(IslandApp.Weather);
                    if (!_mediaActive)
                        TransitionTo(HyperislandState.Idle);
                }

                // Re-animate to current state's dimensions in case they changed, and add a small pulse 
                // to demonstrate the new animation speed/bounce to the user.
                var (w, h, cr) = GetStateDims(_currentState);
                var (ease, dur) = GetAnimationParams();

                // If dimensions are zero, don't pulse
                if (w > 0 && h > 0)
                {
                    var widthAnim = new DoubleAnimation(w + 20, w, dur);
                    var heightAnim = new DoubleAnimation(h + 6, h, dur);
                    if (ease != null)
                    {
                        widthAnim.EasingFunction = ease;
                        heightAnim.EasingFunction = ease;
                    }

                    _window.HyperislandPill.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
                    _window.HyperislandPill.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);
                    AnimateCornerRadius(_window.HyperislandPill, cr, (int)dur.TotalMilliseconds);
                    AnimateCornerRadius(_window.KeylineBorder, cr, (int)dur.TotalMilliseconds);
                }

                UpdateDotIndicators();
            }));
        }

        // ─── Keyline tint ──────────────────────────────────

        private void ApplyKeylineTint()
        {
            if (!_settings.KeylineTintEnabled) return;

            try
            {
                System.Windows.Media.Color accentColor;
                if (_settings.KeylineTintSource == KeylineTintSource.CustomColor)
                {
                    accentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.KeylineTintColor);
                }
                else
                {
                    var uiSettings = new UISettings();
                    var accent = uiSettings.GetColorValue(UIColorType.Accent);
                    accentColor = System.Windows.Media.Color.FromArgb(255, accent.R, accent.G, accent.B);
                }

                double opacityFactor = _settings.KeylineOpacity / 100.0;
                
                // Dynamic neon intensity based on state
                bool isExpanded = _currentState == HyperislandState.Expanded;
                double borderMultiplier = isExpanded ? 0.90 : 0.40;
                double glowMultiplier = isExpanded ? 1.00 : 0.60;
                double targetThickness = isExpanded ? 2.0 : 1.0;
                double targetBlur = isExpanded ? 18.0 : 8.0;

                var borderCol = System.Windows.Media.Color.FromArgb(
                    (byte)(255 * opacityFactor * borderMultiplier),
                    accentColor.R,
                    accentColor.G,
                    accentColor.B);

                var glowCol = System.Windows.Media.Color.FromArgb(
                    (byte)(255 * opacityFactor * glowMultiplier),
                    accentColor.R,
                    accentColor.G,
                    accentColor.B);

                var colorAnim = new ColorAnimation(borderCol, TimeSpan.FromMilliseconds(300));
                var glowAnim = new ColorAnimation(glowCol, TimeSpan.FromMilliseconds(300));

                var brush = _window.KeylineBorder.BorderBrush as SolidColorBrush;
                if (brush is null || brush.IsFrozen)
                {
                    brush = new SolidColorBrush(Colors.Transparent);
                    _window.KeylineBorder.BorderBrush = brush;
                }
                brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);

                // Animate BorderThickness for neon edge
                var thicknessAnim = new ThicknessAnimation(
                    new Thickness(targetThickness),
                    TimeSpan.FromMilliseconds(300));
                _window.KeylineBorder.BeginAnimation(Border.BorderThicknessProperty, thicknessAnim);

                if (_window.KeylineBorder.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
                {
                    glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, glowAnim);

                    // Animate BlurRadius for neon edge
                    var blurAnim = new DoubleAnimation(targetBlur, TimeSpan.FromMilliseconds(300));
                    glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, blurAnim);

                    if (_settings.KeylinePulse)
                    {
                        double minPulse = (isExpanded ? 0.60 : 0.40) * opacityFactor;
                        double maxPulse = (isExpanded ? 1.00 : 0.80) * opacityFactor;
                        var pulseAnim = new DoubleAnimation(minPulse, maxPulse, TimeSpan.FromSeconds(2.0))
                        {
                            AutoReverse = true,
                            RepeatBehavior = RepeatBehavior.Forever
                        };
                        glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, pulseAnim);
                    }
                    else
                    {
                        glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
                        glow.Opacity = glowMultiplier * opacityFactor;
                    }
                }
            }
            catch { }
        }

        private void AnimateKeylineToTransparent()
        {
            try
            {
                var colorAnim = new ColorAnimation(Colors.Transparent,
                    TimeSpan.FromMilliseconds(300));
                var brush = _window.KeylineBorder.BorderBrush as SolidColorBrush;
                if (brush is null || brush.IsFrozen)
                {
                    brush = new SolidColorBrush(Colors.Transparent);
                    _window.KeylineBorder.BorderBrush = brush;
                }
                brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);

                if (_window.KeylineBorder.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
                {
                    glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, colorAnim);
                    glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
                }

                // Restore default thickness/blur
                var thicknessAnim = new ThicknessAnimation(new Thickness(1), TimeSpan.FromMilliseconds(300));
                _window.KeylineBorder.BeginAnimation(Border.BorderThicknessProperty, thicknessAnim);
                
                if (_window.KeylineBorder.Effect is System.Windows.Media.Effects.DropShadowEffect glow2)
                {
                    var blurAnim = new DoubleAnimation(8, TimeSpan.FromMilliseconds(300));
                    glow2.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, blurAnim);
                }
            }
            catch { }
        }

        // ─── Waveform animation ────────────────────────────

        private void BuildWaveformAnimations()
        {
            _wave1Sb = BuildBarStoryboard(_window.WaveBar1,
                new double[] { 4, 14, 6, 14, 4 }, 900);
            _wave2Sb = BuildBarStoryboard(_window.WaveBar2,
                new double[] { 8, 4, 16, 4, 8 }, 700);
            _wave3Sb = BuildBarStoryboard(_window.WaveBar3,
                new double[] { 12, 6, 12, 18, 8 }, 1100);

            _expWave1Sb = BuildBarStoryboard(_window.ExpWaveBar1,
                new double[] { 4, 14, 6, 14, 4 }, 950);
            _expWave2Sb = BuildBarStoryboard(_window.ExpWaveBar2,
                new double[] { 8, 4, 18, 4, 8 }, 750);
            _expWave3Sb = BuildBarStoryboard(_window.ExpWaveBar3,
                new double[] { 14, 6, 14, 22, 10 }, 1150);
            _expWave4Sb = BuildBarStoryboard(_window.ExpWaveBar4,
                new double[] { 8, 16, 6, 16, 8 }, 850);
            _expWave5Sb = BuildBarStoryboard(_window.ExpWaveBar5,
                new double[] { 4, 10, 8, 10, 4 }, 1050);
        }

        private Storyboard BuildBarStoryboard(Rectangle bar, double[] heights, int durationMs)
        {
            var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var anim = new DoubleAnimationUsingKeyFrames();
            int count = heights.Length;
            for (int i = 0; i < count; i++)
            {
                double fraction = (double)i / (count - 1);
                var kf = new LinearDoubleKeyFrame(
                    heights[i],
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * fraction)));
                anim.KeyFrames.Add(kf);
            }
            Storyboard.SetTarget(anim, bar);
            Storyboard.SetTargetProperty(anim,
                new PropertyPath(FrameworkElement.HeightProperty));
            sb.Children.Add(anim);
            return sb;
        }

        private void StartWaveform()
        {
            try
            {
                _wave1Sb?.Begin();
                _wave2Sb?.Begin();
                _wave3Sb?.Begin();

                _expWave1Sb?.Begin();
                _expWave2Sb?.Begin();
                _expWave3Sb?.Begin();
                _expWave4Sb?.Begin();
                _expWave5Sb?.Begin();
            }
            catch { }
        }

        private void StopWaveform()
        {
            try
            {
                _wave1Sb?.Stop();
                _wave2Sb?.Stop();
                _wave3Sb?.Stop();
                _window.WaveBar1.Height = 4;
                _window.WaveBar2.Height = 4;
                _window.WaveBar3.Height = 4;

                _expWave1Sb?.Stop();
                _expWave2Sb?.Stop();
                _expWave3Sb?.Stop();
                _expWave4Sb?.Stop();
                _expWave5Sb?.Stop();
                _window.ExpWaveBar1.Height = 4;
                _window.ExpWaveBar2.Height = 4;
                _window.ExpWaveBar3.Height = 4;
                _window.ExpWaveBar4.Height = 4;
                _window.ExpWaveBar5.Height = 4;
            }
            catch { }
        }
    }
}
