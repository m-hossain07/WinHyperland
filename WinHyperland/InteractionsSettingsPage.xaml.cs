using System;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace WinHyperisland
{
    public partial class InteractionsSettingsPage : UserControl
    {
        private readonly SettingsService _settings;
        private bool _isLoading;

        public InteractionsSettingsPage(SettingsService settings)
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
            ToggleHoverExpand.IsChecked = _settings.HoverToExpand;
            ToggleClickOpen.IsChecked = _settings.ClickToOpenApp;
            ToggleClickOutside.IsChecked = _settings.ClickOutsideToCollapse;
            UpdateHoldNote();
        }

        private void UpdateHoldNote()
        {
            HoldNote.Visibility = _settings.HoverToExpand
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ToggleHoverExpand_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.HoverToExpand = ToggleHoverExpand.IsChecked == true;
            UpdateHoldNote();
            _settings.NotifyChanged();
        }

        private void ToggleClickOpen_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.ClickToOpenApp = ToggleClickOpen.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleClickOutside_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.ClickOutsideToCollapse = ToggleClickOutside.IsChecked == true;
            _settings.NotifyChanged();
        }
    }
}
