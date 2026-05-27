using System;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace WinHyperisland
{
    public partial class ExclusionsSettingsPage : UserControl
    {
        private readonly SettingsService _settings;

        public ExclusionsSettingsPage(SettingsService settings)
        {
            InitializeComponent();
            _settings = settings;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void RefreshList()
        {
            ExclusionsList.Children.Clear();

            var apps = _settings.ExcludedApps;
            EmptyState.Visibility = apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (string appId in apps)
            {
                var itemBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 2, 0, 2),
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = appId,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetColumn(label, 0);

                var removeBtn = new Button
                {
                    Content = "Remove",
                    Padding = new Thickness(10, 4, 10, 4),
                    FontSize = 11,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0xAA, 0xAA)),
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0x6B, 0x6B)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0x6B, 0x6B)),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = appId,
                };

                // Style the remove button with rounded corners via template
                var btnTemplate = new ControlTemplate(typeof(Button));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, removeBtn.Background);
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                borderFactory.SetValue(Border.PaddingProperty, removeBtn.Padding);
                borderFactory.SetValue(Border.BorderThicknessProperty, removeBtn.BorderThickness);
                borderFactory.SetValue(Border.BorderBrushProperty, removeBtn.BorderBrush);
                var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                borderFactory.AppendChild(contentPresenter);
                btnTemplate.VisualTree = borderFactory;
                removeBtn.Template = btnTemplate;

                removeBtn.Click += RemoveExclusion_Click;
                Grid.SetColumn(removeBtn, 1);

                grid.Children.Add(label);
                grid.Children.Add(removeBtn);
                itemBorder.Child = grid;
                ExclusionsList.Children.Add(itemBorder);
            }
        }

        private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string appId = btn.Tag as string ?? "";
            if (string.IsNullOrEmpty(appId)) return;

            _settings.ExcludedApps.Remove(appId);
            _settings.NotifyChanged();
            RefreshList();
        }

        private void AddApp_Click(object sender, RoutedEventArgs e)
        {
            // Show a simple input dialog using a child Window
            var dialog = new Window
            {
                Title = "Add Excluded App",
                Width = 420,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1C, 0x1C, 0x1E)),
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var prompt = new TextBlock
            {
                Text = "Enter app package name or ID",
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10),
            };

            var input = new TextBox
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 16),
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(20, 6, 20, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
            };
            cancelBtn.Click += (_, _) => dialog.DialogResult = false;

            var okBtn = new Button
            {
                Content = "Add",
                Padding = new Thickness(20, 6, 20, 6),
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD7)),
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
            };
            okBtn.Click += (_, _) => dialog.DialogResult = true;

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);

            panel.Children.Add(prompt);
            panel.Children.Add(input);
            panel.Children.Add(btnPanel);
            dialog.Content = panel;

            if (dialog.ShowDialog() == true)
            {
                string appId = input.Text.Trim();
                if (!string.IsNullOrEmpty(appId) && !_settings.ExcludedApps.Contains(appId))
                {
                    _settings.ExcludedApps.Add(appId);
                    _settings.NotifyChanged();
                    RefreshList();
                }
            }
        }
    }
}
