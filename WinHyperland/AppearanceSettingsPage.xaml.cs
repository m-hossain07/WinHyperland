using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;

namespace WinHyperisland
{
    public partial class AppearanceSettingsPage : UserControl
    {
        private readonly SettingsService _settings;
        private bool _isLoading;

        public AppearanceSettingsPage(SettingsService settings)
        {
            InitializeComponent();
            _settings = settings;
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
            // Pill Color
            ToggleCustomColor.IsChecked = _settings.UseCustomColor;
            PillColorInput.Text = _settings.PillColor;
            UpdatePillColorPreview();
            PillColorPanel.Visibility = _settings.UseCustomColor ? Visibility.Visible : Visibility.Collapsed;

            // Keyline Tint
            ToggleKeylineTint.IsChecked = _settings.KeylineTintEnabled;
            KeylineSourceCombo.SelectedIndex = (int)_settings.KeylineTintSource;
            KeylineColorInput.Text = _settings.KeylineTintColor;
            UpdateKeylineColorPreview();
            KeylineOpacitySlider.Value = _settings.KeylineOpacity;
            KeylineOpacityLabel.Text = $"{_settings.KeylineOpacity}%";

            UpdateKeylinePanelVisibility();

            // Minimise Mode
            ToggleMiniPill.IsChecked = _settings.UseMiniPillWhenIdle;

            // Idle Opacity
            IdleOpacitySlider.Value = _settings.PillOpacityWhenIdle;
            IdleOpacityLabel.Text = $"{_settings.PillOpacityWhenIdle}%";
        }

        private void UpdatePillColorPreview()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_settings.PillColor);
                PillColorPreview.Background = new SolidColorBrush(color);
            }
            catch
            {
                PillColorPreview.Background = Brushes.Black;
            }
        }

        private void UpdateKeylineColorPreview()
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_settings.KeylineTintColor);
                KeylineColorPreview.Background = new SolidColorBrush(color);
            }
            catch
            {
                KeylineColorPreview.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            }
        }

        private void UpdateKeylinePanelVisibility()
        {
            bool tintEnabled = _settings.KeylineTintEnabled;
            KeylineSourceRow.Visibility = tintEnabled ? Visibility.Visible : Visibility.Collapsed;
            KeylineOpacityRow.Visibility = tintEnabled ? Visibility.Visible : Visibility.Collapsed;

            bool isCustom = _settings.KeylineTintSource == KeylineTintSource.CustomColor;
            KeylineCustomColorPanel.Visibility = tintEnabled && isCustom
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ─── Pill Color ────────────────────────────────────

        private void ToggleCustomColor_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.UseCustomColor = ToggleCustomColor.IsChecked == true;
            PillColorPanel.Visibility = _settings.UseCustomColor ? Visibility.Visible : Visibility.Collapsed;
            _settings.NotifyChanged();
        }

        private void PillColorInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            string text = PillColorInput.Text.Trim();
            try
            {
                ColorConverter.ConvertFromString(text);
                _settings.PillColor = text;
                UpdatePillColorPreview();
                _settings.NotifyChanged();
            }
            catch
            {
                PillColorInput.Text = _settings.PillColor;
            }
        }

        // ─── Keyline Tint ──────────────────────────────────

        private void ToggleKeylineTint_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.KeylineTintEnabled = ToggleKeylineTint.IsChecked == true;
            UpdateKeylinePanelVisibility();
            _settings.NotifyChanged();
        }

        private void KeylineSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (KeylineSourceCombo.SelectedIndex < 0) return;
            var newSource = (KeylineTintSource)KeylineSourceCombo.SelectedIndex;
            if (_settings.KeylineTintSource == newSource) return;
            _settings.KeylineTintSource = newSource;
            UpdateKeylinePanelVisibility();
            _settings.NotifyChanged();
        }

        private void KeylineColorInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            string text = KeylineColorInput.Text.Trim();
            try
            {
                ColorConverter.ConvertFromString(text);
                _settings.KeylineTintColor = text;
                UpdateKeylineColorPreview();
                _settings.NotifyChanged();
            }
            catch
            {
                KeylineColorInput.Text = _settings.KeylineTintColor;
            }
        }

        private void KeylineOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            if (KeylineOpacityLabel == null) return;
            int val = (int)KeylineOpacitySlider.Value;
            _settings.KeylineOpacity = val;
            KeylineOpacityLabel.Text = $"{val}%";
            _settings.NotifyChanged();
        }

        // ─── Idle Opacity ──────────────────────────────────

        private void IdleOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            if (IdleOpacityLabel == null) return;
            int val = (int)IdleOpacitySlider.Value;
            _settings.PillOpacityWhenIdle = val;
            IdleOpacityLabel.Text = $"{val}%";
            _settings.NotifyChanged();
        }

        // ─── Minimise Mode ─────────────────────────────────

        private void ToggleMiniPill_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.UseMiniPillWhenIdle = ToggleMiniPill.IsChecked == true;
            _settings.NotifyChanged();
        }
    }
}
