using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using UserControl = System.Windows.Controls.UserControl;

namespace WinHyperland
{
    public partial class FeaturesSettingsPage : UserControl
    {
        private readonly SettingsService _settings;
        private readonly HyperlandController? _controller;
        private bool _isLoading;

        public FeaturesSettingsPage(SettingsService settings, HyperlandController? controller = null)
        {
            InitializeComponent();
            _settings = settings;
            _controller = controller;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            LoadValues();
            _isLoading = false;
        }

        private void LoadValues()
        {
            ToggleMedia.IsChecked = _settings.MediaIntegrationEnabled;
            ToggleNotifications.IsChecked = _settings.NotificationsEnabled;
            ToggleStartup.IsChecked = _settings.LaunchOnWindowsStartup;
            ToggleWeather.IsChecked = _settings.WeatherEnabled;
            TempUnitCombo.SelectedIndex = _settings.TemperatureUnit == "F" ? 1 : 0;
            NotifDurationInput.Text = _settings.NotificationDuration.ToString();
            CollapseDelayInput.Text = _settings.MediaPausedCollapseDelay.ToString();
            RefreshMediaStatus();
        }

        // ─── System Integration ────────────────────────────

        private void ToggleMedia_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.MediaIntegrationEnabled = ToggleMedia.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleNotifications_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.NotificationsEnabled = ToggleNotifications.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleStartup_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            bool enable = ToggleStartup.IsChecked == true;
            _settings.LaunchOnWindowsStartup = enable;

            try
            {
                // Use registry for startup (WPF desktop app approach)
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                        if (!string.IsNullOrEmpty(exePath))
                            key.SetValue("WinHyperland", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("WinHyperland", false);
                    }
                }
            }
            catch { }

            _settings.NotifyChanged();
        }

        private void ToggleWeather_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.WeatherEnabled = ToggleWeather.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void TempUnitCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (TempUnitCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                _settings.TemperatureUnit = item.Tag?.ToString() ?? "C";
                _settings.NotifyChanged();
            }
        }

        // ─── Timing ────────────────────────────────────────

        private void NotifDurationInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (int.TryParse(NotifDurationInput.Text, out int val))
            {
                val = Math.Clamp(val, 2, 15);
                _settings.NotificationDuration = val;
                NotifDurationInput.Text = val.ToString();
                _settings.NotifyChanged();
            }
            else
            {
                NotifDurationInput.Text = _settings.NotificationDuration.ToString();
            }
        }

        private void CollapseDelayInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (int.TryParse(CollapseDelayInput.Text, out int val))
            {
                val = Math.Clamp(val, 1, 10);
                _settings.MediaPausedCollapseDelay = val;
                CollapseDelayInput.Text = val.ToString();
                _settings.NotifyChanged();
            }
            else
            {
                CollapseDelayInput.Text = _settings.MediaPausedCollapseDelay.ToString();
            }
        }

        // ─── Live Status ───────────────────────────────────

        private void RefreshMediaStatus()
        {
            // Attempt to read current media info from the controller
            // For now, just show the default text
            StatusTrackTitle.Text = "No media playing";
        }

        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            RefreshMediaStatus();
        }

        private void TestNotification_Click(object sender, RoutedEventArgs e)
        {
            if (_controller == null) return;

            var mock = new NotificationInfo(
                AppName: "Win Hyperland",
                Title: "Test notification",
                Body: "Notification pipeline is working",
                Icon: null,
                PackageFamilyName: "");

            _controller.HandleNotification(mock);
        }
    }
}
