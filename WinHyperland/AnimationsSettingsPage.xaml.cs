using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using UserControl = System.Windows.Controls.UserControl;
using RadioButton = System.Windows.Controls.RadioButton;

namespace WinHyperisland
{
    public partial class AnimationsSettingsPage : UserControl
    {
        private readonly SettingsService _settings;
        private bool _isLoading;

        public AnimationsSettingsPage(SettingsService settings)
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
            // Select correct radio
            switch (_settings.AnimationPreset)
            {
                case AnimationPreset.Island:  RadioIsland.IsChecked = true;  break;
                case AnimationPreset.Fluid:   RadioFluid.IsChecked = true;   break;
                case AnimationPreset.Smooth:  RadioSmooth.IsChecked = true;  break;
                case AnimationPreset.Snappy:  RadioSnappy.IsChecked = true;  break;
                case AnimationPreset.Instant: RadioInstant.IsChecked = true; break;
            }

            // Toggles
            TogglePillMorph.IsChecked = _settings.PillMorphAnimation;
            ToggleCrossfade.IsChecked = _settings.ContentCrossfade;
            ToggleWaveform.IsChecked = _settings.WaveformBars;
            ToggleNotifDrop.IsChecked = _settings.NotificationDrop;
            ToggleExpandMedia.IsChecked = _settings.ExpandOnMediaChange;
            ToggleKeylinePulse.IsChecked = _settings.KeylinePulse;

            UpdateInstantMode();
        }

        private void UpdateInstantMode()
        {
            bool isInstant = _settings.AnimationPreset == AnimationPreset.Instant;
            EffectsPanel.IsEnabled = !isInstant;
            EffectsPanel.Opacity = isInstant ? 0.4 : 1.0;
            InstantModeNote.Visibility = isInstant ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Preset_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (sender is not RadioButton radio) return;

            string tag = radio.Tag as string ?? "";
            _settings.AnimationPreset = tag switch
            {
                "Island"  => AnimationPreset.Island,
                "Fluid"   => AnimationPreset.Fluid,
                "Smooth"  => AnimationPreset.Smooth,
                "Snappy"  => AnimationPreset.Snappy,
                "Instant" => AnimationPreset.Instant,
                _ => AnimationPreset.Island,
            };

            UpdateInstantMode();
            RunPreviewAnimation();
            _settings.NotifyChanged();
        }

        private void RunPreviewAnimation()
        {
            var spring = SpringParams.FromPreset(_settings.AnimationPreset);

            if (_settings.AnimationPreset == AnimationPreset.Instant)
            {
                // Instant: just snap sizes
                PreviewPill.Width = 200;
                PreviewPill.Height = 44;

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    PreviewPill.Width = 100;
                    PreviewPill.Height = 28;
                };
                timer.Start();
                return;
            }

            // Build easing from spring params
            var ease = new BackEase
            {
                Amplitude = spring.Amplitude,
                EasingMode = EasingMode.EaseOut
            };
            var duration = TimeSpan.FromSeconds(spring.Period);

            // Phase 1: expand 100×28 → 200×44
            var expandW = new DoubleAnimation(100, 200, duration) { EasingFunction = ease };
            var expandH = new DoubleAnimation(28, 44, duration) { EasingFunction = ease };

            // Phase 2: contract 200×44 → 100×28, delayed
            var contractW = new DoubleAnimation(200, 100, duration)
            {
                EasingFunction = ease,
                BeginTime = duration + TimeSpan.FromMilliseconds(200)
            };
            var contractH = new DoubleAnimation(44, 28, duration)
            {
                EasingFunction = ease,
                BeginTime = duration + TimeSpan.FromMilliseconds(200)
            };

            var sb = new Storyboard();
            Storyboard.SetTarget(expandW, PreviewPill);
            Storyboard.SetTargetProperty(expandW, new PropertyPath(WidthProperty));
            Storyboard.SetTarget(expandH, PreviewPill);
            Storyboard.SetTargetProperty(expandH, new PropertyPath(HeightProperty));
            Storyboard.SetTarget(contractW, PreviewPill);
            Storyboard.SetTargetProperty(contractW, new PropertyPath(WidthProperty));
            Storyboard.SetTarget(contractH, PreviewPill);
            Storyboard.SetTargetProperty(contractH, new PropertyPath(HeightProperty));

            sb.Children.Add(expandW);
            sb.Children.Add(expandH);
            sb.Children.Add(contractW);
            sb.Children.Add(contractH);
            sb.Begin();
        }

        // ─── Toggle event handlers ─────────────────────────

        private void TogglePillMorph_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.PillMorphAnimation = TogglePillMorph.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleCrossfade_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.ContentCrossfade = ToggleCrossfade.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleWaveform_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.WaveformBars = ToggleWaveform.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleNotifDrop_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.NotificationDrop = ToggleNotifDrop.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleExpandMedia_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.ExpandOnMediaChange = ToggleExpandMedia.IsChecked == true;
            _settings.NotifyChanged();
        }

        private void ToggleKeylinePulse_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settings.KeylinePulse = ToggleKeylinePulse.IsChecked == true;
            _settings.NotifyChanged();
        }
    }
}
