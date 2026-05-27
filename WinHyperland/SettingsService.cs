using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace WinHyperisland
{
    // ─── Enums ─────────────────────────────────────────────

    public enum ScreenPosition { TopCenter, TopLeft, TopRight, Custom }

    public enum AnimationPreset { Island, Fluid, Smooth, Snappy, Instant }

    public enum KeylineTintSource { AppAccent, SystemAccent, CustomColor }

    // ─── Spring parameters ─────────────────────────────────

    public record SpringParams(double DampingRatio, double Period, double Amplitude)
    {
        public static SpringParams FromPreset(AnimationPreset preset) => preset switch
        {
            AnimationPreset.Island  => new(0.72, 0.38, 0.30),
            AnimationPreset.Fluid   => new(0.55, 0.55, 0.45),
            AnimationPreset.Smooth  => new(1.00, 0.35, 0.00),
            AnimationPreset.Snappy  => new(0.85, 0.20, 0.10),
            AnimationPreset.Instant => new(1.00, 0.01, 0.00),
            _ => new(0.72, 0.38, 0.30),
        };
    }

    // ─── Settings data model ───────────────────────────────

    public class SettingsData
    {
        // Position
        public ScreenPosition ScreenPosition { get; set; } = ScreenPosition.TopCenter;
        public int CustomX { get; set; } = 0;
        public int PillVerticalOffset { get; set; } = 12;
        public string TargetMonitor { get; set; } = "Primary";

        // Animations
        public AnimationPreset AnimationPreset { get; set; } = AnimationPreset.Island;
        public bool PillMorphAnimation { get; set; } = true;
        public bool ContentCrossfade { get; set; } = true;
        public bool WaveformBars { get; set; } = true;
        public bool NotificationDrop { get; set; } = true;
        public bool ExpandOnMediaChange { get; set; } = true;
        public bool KeylinePulse { get; set; } = true;

        // Appearance
        public bool UseCustomColor { get; set; } = false;
        public string PillColor { get; set; } = "#FF000000";
        public bool KeylineTintEnabled { get; set; } = true;
        public KeylineTintSource KeylineTintSource { get; set; } = KeylineTintSource.SystemAccent;
        public string KeylineTintColor { get; set; } = "#FF0078D7";
        public int KeylineOpacity { get; set; } = 40;
        public int PillOpacityWhenIdle { get; set; } = 100;
        public bool UseMiniPillWhenIdle { get; set; } = false;

        // Features
        public bool MediaIntegrationEnabled { get; set; } = true;
        public bool NotificationsEnabled { get; set; } = true;
        public bool WeatherEnabled { get; set; } = true;
        public string TemperatureUnit { get; set; } = "C"; // "C" or "F"
        public bool LaunchOnWindowsStartup { get; set; } = false;
        public int NotificationDuration { get; set; } = 5;
        public int MediaPausedCollapseDelay { get; set; } = 3;

        // Interactions
        public bool HoverToExpand { get; set; } = true;
        public bool ClickToOpenApp { get; set; } = true;
        public bool ClickOutsideToCollapse { get; set; } = true;

        // Exclusions
        public List<string> ExcludedApps { get; set; } = new();
    }

    // ─── Implementation ────────────────────────────────────

    public sealed class SettingsService
    {
        private static readonly string SettingsFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinHyperisland");
        private static readonly string SettingsFile =
            Path.Combine(SettingsFolder, "settings.json");

        private SettingsData _data = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public event Action? SettingsChanged;

        // Position
        public ScreenPosition ScreenPosition { get => _data.ScreenPosition; set => _data.ScreenPosition = value; }
        public int CustomX { get => _data.CustomX; set => _data.CustomX = value; }
        public int PillVerticalOffset { get => _data.PillVerticalOffset; set => _data.PillVerticalOffset = value; }
        public string TargetMonitor { get => _data.TargetMonitor; set => _data.TargetMonitor = value; }

        // Animations
        public AnimationPreset AnimationPreset { get => _data.AnimationPreset; set => _data.AnimationPreset = value; }
        public bool PillMorphAnimation { get => _data.PillMorphAnimation; set => _data.PillMorphAnimation = value; }
        public bool ContentCrossfade { get => _data.ContentCrossfade; set => _data.ContentCrossfade = value; }
        public bool WaveformBars { get => _data.WaveformBars; set => _data.WaveformBars = value; }
        public bool NotificationDrop { get => _data.NotificationDrop; set => _data.NotificationDrop = value; }
        public bool ExpandOnMediaChange { get => _data.ExpandOnMediaChange; set => _data.ExpandOnMediaChange = value; }
        public bool KeylinePulse { get => _data.KeylinePulse; set => _data.KeylinePulse = value; }

        // Appearance
        public bool UseCustomColor { get => _data.UseCustomColor; set => _data.UseCustomColor = value; }
        public string PillColor { get => _data.PillColor; set => _data.PillColor = value; }
        public bool KeylineTintEnabled { get => _data.KeylineTintEnabled; set => _data.KeylineTintEnabled = value; }
        public KeylineTintSource KeylineTintSource { get => _data.KeylineTintSource; set => _data.KeylineTintSource = value; }
        public string KeylineTintColor { get => _data.KeylineTintColor; set => _data.KeylineTintColor = value; }
        public int KeylineOpacity { get => _data.KeylineOpacity; set => _data.KeylineOpacity = value; }
        public int PillOpacityWhenIdle { get => _data.PillOpacityWhenIdle; set => _data.PillOpacityWhenIdle = value; }
        public bool UseMiniPillWhenIdle { get => _data.UseMiniPillWhenIdle; set => _data.UseMiniPillWhenIdle = value; }

        // Features
        public bool MediaIntegrationEnabled { get => _data.MediaIntegrationEnabled; set => _data.MediaIntegrationEnabled = value; }
        public bool NotificationsEnabled { get => _data.NotificationsEnabled; set => _data.NotificationsEnabled = value; }
        public bool WeatherEnabled { get => _data.WeatherEnabled; set => _data.WeatherEnabled = value; }
        public string TemperatureUnit { get => _data.TemperatureUnit; set => _data.TemperatureUnit = value; }
        public bool LaunchOnWindowsStartup { get => _data.LaunchOnWindowsStartup; set => _data.LaunchOnWindowsStartup = value; }
        public int NotificationDuration { get => _data.NotificationDuration; set => _data.NotificationDuration = value; }
        public int MediaPausedCollapseDelay { get => _data.MediaPausedCollapseDelay; set => _data.MediaPausedCollapseDelay = value; }

        // Interactions
        public bool HoverToExpand { get => _data.HoverToExpand; set => _data.HoverToExpand = value; }
        public bool ClickToOpenApp { get => _data.ClickToOpenApp; set => _data.ClickToOpenApp = value; }
        public bool ClickOutsideToCollapse { get => _data.ClickOutsideToCollapse; set => _data.ClickOutsideToCollapse = value; }

        // Exclusions
        public List<string> ExcludedApps { get => _data.ExcludedApps; set => _data.ExcludedApps = value; }

        public void NotifyChanged()
        {
            Save();
            SettingsChanged?.Invoke();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                string json = JsonSerializer.Serialize(_data, JsonOptions);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var loaded = JsonSerializer.Deserialize<SettingsData>(json, JsonOptions);
                    if (loaded != null)
                        _data = loaded;
                }
            }
            catch { }
        }
    }
}
