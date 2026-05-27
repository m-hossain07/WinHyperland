using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WinHyperisland
{
    public partial class App : System.Windows.Application
    {
        private MediaManager? _mediaManager;
        private NotificationManager? _notifManager;
        private WeatherManager? _weatherManager;
        private HyperislandController? _controller;
        private static SettingsService? _settingsService;

        private static SettingsWindow? _settingsWindow;
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var dispatcher = Dispatcher.CurrentDispatcher;

            // Initialize settings
            _settingsService = new SettingsService();
            _settingsService.Load();

            _mediaManager = new MediaManager(dispatcher);
            _notifManager = new NotificationManager(dispatcher);
            _weatherManager = new WeatherManager(dispatcher, _settingsService);

            await _mediaManager.InitializeAsync();
            await _notifManager.InitializeAsync();

            var window = new MainWindow(_settingsService);
            _controller = new HyperislandController(window, _mediaManager, _settingsService);
            window.Controller = _controller;

            // Wire notification events
            _notifManager.OnNotificationReceived += (info) =>
            {
                _controller.HandleNotification(info);
            };

            // Wire weather manager
            _controller.SetWeatherManager(_weatherManager);
            if (_settingsService.WeatherEnabled)
            {
                _ = _weatherManager.InitializeAsync();
            }

            window.Show();

            // Create system tray icon
            SetupTrayIcon();

            OpenSettings();
        }

        private void SetupTrayIcon()
        {
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();

            var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
            settingsItem.Click += (_, _) => Dispatcher.Invoke(() => OpenSettings());

            var separatorItem = new System.Windows.Forms.ToolStripSeparator();

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
                Shutdown();
            });

            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(separatorItem);
            contextMenu.Items.Add(exitItem);

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "Win Hyperisland",
                ContextMenuStrip = contextMenu,
                Visible = true,
            };

            // Load icon from file or use a default
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (File.Exists(iconPath))
                {
                    _trayIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _trayIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _trayIcon.Icon = SystemIcons.Application;
            }

            // Double-click tray icon opens settings
            _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => OpenSettings());
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            base.OnExit(e);
        }

        public static void OpenSettings()
        {
            if (_settingsService == null) return;

            if (_settingsWindow == null)
            {
                var app = (App)Current;
                _settingsWindow = new SettingsWindow(_settingsService, app._controller);
                _settingsWindow.Closed += (_, _) =>
                {
                    _settingsWindow = null;
                };
                _settingsWindow.Show();
            }
            _settingsWindow.Activate();
        }
    }
}
