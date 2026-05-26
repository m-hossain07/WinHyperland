using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace WinHyperland
{
    public record NotificationInfo(
        string AppName,
        string Title,
        string Body,
        BitmapImage? Icon,
        string PackageFamilyName);

    public sealed class NotificationManager : IDisposable
    {
        public event Action<NotificationInfo>? OnNotificationReceived;

        private readonly Dispatcher _dispatcher;
        private UserNotificationListener? _listener;
        private bool _isEnabled;
        private bool _disposed;
        private DispatcherTimer? _pollTimer;
        private readonly System.Collections.Generic.HashSet<uint> _processedNotifs = new();

        public NotificationManager(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public async Task InitializeAsync()
        {
            try
            {
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", "Init start\n");
                _listener = UserNotificationListener.Current;
                if (_listener is null) 
                {
                    System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", "Listener null\n");
                    return;
                }

                var access = await _listener.RequestAccessAsync();
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", $"Access: {access}\n");
                if (access != UserNotificationListenerAccessStatus.Allowed)
                {
                    _isEnabled = false;
                    return;
                }

                _isEnabled = true;
                
                var currentNotifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", $"Found {currentNotifs.Count} initial notifs\n");
                foreach (var n in currentNotifs)
                {
                    _processedNotifs.Add(n.Id);
                }

                _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _pollTimer.Tick += PollTimer_Tick;
                _pollTimer.Start();
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", "Timer started\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", $"Init err: {ex}\n");
                _isEnabled = false;
            }
        }

        private async void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isEnabled || _listener == null) return;

            try
            {
                var notifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                foreach (var notification in notifs)
                {
                    if (!_processedNotifs.Contains(notification.Id))
                    {
                        System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", $"Found new notif {notification.Id}\n");
                        _processedNotifs.Add(notification.Id);
                        ProcessNotification(notification);
                    }
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_notif.log", $"Poll err: {ex.Message}\n");
            }
        }

        private async void ProcessNotification(UserNotification notification)
        {
            try
            {
                // AppInfo throws NotImplementedException on unpackaged desktop apps
                string appName = "Unknown";
                string packageFamilyName = "";
                Windows.ApplicationModel.AppInfo? appInfo = null;

                try
                {
                    appInfo = notification.AppInfo;
                    if (appInfo is not null)
                    {
                        appName = appInfo.DisplayInfo?.DisplayName ?? "Unknown";
                        packageFamilyName = appInfo.PackageFamilyName ?? "";
                    }
                }
                catch { /* AppInfo not available on unpackaged apps */ }

                var bindings = notification.Notification?.Visual?.Bindings;
                if (bindings is null || bindings.Count == 0) return;

                var texts = bindings[0].GetTextElements();
                string title = texts.Count > 0 ? texts[0].Text ?? "" : "";
                string body = texts.Count > 1 ? texts[1].Text ?? "" : "";

                BitmapImage? icon = null;
                try
                {
                    if (appInfo?.DisplayInfo is not null)
                    {
                        var logoRef = appInfo.DisplayInfo.GetLogo(
                            new Windows.Foundation.Size(32, 32));
                        if (logoRef is not null)
                        {
                            using var stream = await logoRef.OpenReadAsync();
                            var memStream = new MemoryStream();
                            using (var netStream = stream.AsStreamForRead())
                            {
                                await netStream.CopyToAsync(memStream);
                            }
                            memStream.Position = 0;

                            _dispatcher.Invoke(() =>
                            {
                                icon = new BitmapImage();
                                icon.BeginInit();
                                icon.CacheOption = BitmapCacheOption.OnLoad;
                                icon.StreamSource = memStream;
                                icon.EndInit();
                                icon.Freeze();
                            });
                        }
                    }
                }
                catch { icon = null; }

                var info = new NotificationInfo(appName, title, body, icon, packageFamilyName);
                _ = _dispatcher.InvokeAsync(() => OnNotificationReceived?.Invoke(info));
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Tick -= PollTimer_Tick;
            }
        }
    }
}
