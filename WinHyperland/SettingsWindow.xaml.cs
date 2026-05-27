using System;
using System.Windows;
using System.Windows.Controls;

namespace WinHyperisland
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settings;
        private readonly HyperislandController? _controller;

        // Lazy-created pages
        private PositionSettingsPage? _positionPage;
        private AnimationsSettingsPage? _animationsPage;
        private AppearanceSettingsPage? _appearancePage;
        private FeaturesSettingsPage? _featuresPage;
        private InteractionsSettingsPage? _interactionsPage;
        private ExclusionsSettingsPage? _exclusionsPage;

        public SettingsWindow(SettingsService settings, HyperislandController? controller = null)
        {
            InitializeComponent();
            _settings = settings;
            _controller = controller;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Select first item
            NavList.SelectedIndex = 0;
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (NavList.SelectedItem is not ListBoxItem item)
                {
                    System.IO.File.AppendAllText("d:\\Antigravity\\debug_settings.log", "Selection changed: item is null\n");
                    return;
                }
                string tag = item.Tag as string ?? "";
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_settings.log", $"Selection changed: tag={tag}\n");

                object page = tag switch
                {
                    "Position"     => _positionPage ??= new PositionSettingsPage(_settings),
                    "Animations"   => _animationsPage ??= new AnimationsSettingsPage(_settings),
                    "Appearance"   => _appearancePage ??= new AppearanceSettingsPage(_settings),
                    "Features"     => _featuresPage ??= new FeaturesSettingsPage(_settings, _controller),
                    "Interactions" => _interactionsPage ??= new InteractionsSettingsPage(_settings),
                    "Exclusions"   => _exclusionsPage ??= new ExclusionsSettingsPage(_settings),
                    _ => null,
                };

                ContentArea.Content = page;
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_settings.log", $"Set ContentArea.Content to page of type {page?.GetType().Name ?? "null"}\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_settings.log", $"Error in SelectionChanged: {ex}\n");
            }
        }
    }
}
