using System;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace WinHyperisland
{
    public partial class PositionSettingsPage : UserControl
    {
        private readonly SettingsService _settings;

        private bool _isLoading;

        public PositionSettingsPage(SettingsService settings)
        {
            InitializeComponent();
            _settings = settings;
            _isLoading = true;
            LoadValues();
            _isLoading = false;
        }

        private void LoadValues()
        {
            PositionCombo.SelectedIndex = (int)_settings.ScreenPosition;
            CustomXInput.Text = _settings.CustomX.ToString();
            VerticalOffsetInput.Text = _settings.PillVerticalOffset.ToString();
            PopulateMonitors();
        }

        private void PopulateMonitors()
        {
            MonitorCombo.Items.Clear();
            MonitorCombo.Items.Add(new ComboBoxItem { Content = "Active Monitor (Cursor)", Tag = "Active" });
            MonitorCombo.Items.Add(new ComboBoxItem { Content = "Primary Monitor", Tag = "Primary" });

            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    var s = screens[i];
                    string name = $"Monitor {i + 1} ({s.Bounds.Width}x{s.Bounds.Height})";
                    MonitorCombo.Items.Add(new ComboBoxItem { Content = name, Tag = s.DeviceName });
                }
            }
            catch { }

            string currentTarget = _settings.TargetMonitor;
            int selectIndex = 0; // Default to Active
            for (int i = 0; i < MonitorCombo.Items.Count; i++)
            {
                if (MonitorCombo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == currentTarget)
                {
                    selectIndex = i;
                    break;
                }
            }
            MonitorCombo.SelectedIndex = selectIndex;
        }

        private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (MonitorCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string newTarget = item.Tag.ToString() ?? "Primary";
                if (_settings.TargetMonitor == newTarget) return;
                _settings.TargetMonitor = newTarget;
                _settings.NotifyChanged();
            }
        }



        private void PositionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (PositionCombo.SelectedIndex < 0) return;
            var newPos = (ScreenPosition)PositionCombo.SelectedIndex;
            if (_settings.ScreenPosition == newPos) return;
            _settings.ScreenPosition = newPos;
            _settings.NotifyChanged();
        }

        private void CustomXInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(CustomXInput.Text, out int val))
            {
                // Clamp: get primary screen width
                int maxX = (int)SystemParameters.PrimaryScreenWidth - 440;
                val = Math.Clamp(val, 0, Math.Max(0, maxX));
                _settings.CustomX = val;
                CustomXInput.Text = val.ToString();
                _settings.NotifyChanged();
            }
            else
            {
                CustomXInput.Text = _settings.CustomX.ToString();
            }
        }

        private void VerticalOffsetInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(VerticalOffsetInput.Text, out int val))
            {
                val = Math.Clamp(val, 0, 60);
                _settings.PillVerticalOffset = val;
                VerticalOffsetInput.Text = val.ToString();
                _settings.NotifyChanged();
            }
            else
            {
                VerticalOffsetInput.Text = _settings.PillVerticalOffset.ToString();
            }
        }

        private void CustomXInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CustomXInput_LostFocus(sender, e);
            }
        }

        private void VerticalOffsetInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                VerticalOffsetInput_LostFocus(sender, e);
            }
        }
    }
}
