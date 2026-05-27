using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WinHyperisland
{
    public class WeatherInfo
    {
        public string City { get; set; } = "";
        public double Temperature { get; set; }
        public double FeelsLike { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public int WeatherCode { get; set; }
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "\xE9CA"; // Segoe Fluent: Globe
        public double HighTemp { get; set; }
        public double LowTemp { get; set; }
        public bool IsDay { get; set; } = true;
        public DateTime LastUpdated { get; set; }
    }

    public sealed class WeatherManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly HttpClient _httpClient = new();
        private readonly DispatcherTimer _refreshTimer;
        private readonly SettingsService _settings;

        private double _latitude;
        private double _longitude;
        private bool _locationResolved;

        public WeatherInfo? CurrentWeather { get; private set; }
        public event Action<WeatherInfo>? OnWeatherUpdated;
        public event Action? OnWeatherError;

        public WeatherManager(Dispatcher dispatcher, SettingsService settings)
        {
            _dispatcher = dispatcher;
            _settings = settings;
            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMinutes(15)
            };
            _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        }

        public async Task InitializeAsync()
        {
            try
            {
                await ResolveLocationAsync();
                await RefreshAsync();
                _refreshTimer.Start();
            }
            catch
            {
                _dispatcher.BeginInvoke(() => OnWeatherError?.Invoke());
            }
        }

        public async Task RefreshAsync()
        {
            if (!_locationResolved) return;

            try
            {
                // Get temperature unit from settings
                string unit = _settings.TemperatureUnit == "F" ? "fahrenheit" : "celsius";
                
                // Open-Meteo: completely free, no API key
                string url = $"https://api.open-meteo.com/v1/forecast?" +
                    $"latitude={_latitude}&longitude={_longitude}" +
                    $"&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,is_day" +
                    $"&daily=temperature_2m_max,temperature_2m_min" +
                    $"&temperature_unit={unit}" +
                    $"&timezone=auto&forecast_days=1";

                string json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var current = root.GetProperty("current");

                int weatherCode = current.GetProperty("weather_code").GetInt32();
                bool isDay = current.GetProperty("is_day").GetInt32() == 1;

                var daily = root.GetProperty("daily");
                double highTemp = daily.GetProperty("temperature_2m_max")[0].GetDouble();
                double lowTemp = daily.GetProperty("temperature_2m_min")[0].GetDouble();

                var info = new WeatherInfo
                {
                    City = _cityName,
                    Temperature = Math.Round(current.GetProperty("temperature_2m").GetDouble()),
                    FeelsLike = Math.Round(current.GetProperty("apparent_temperature").GetDouble()),
                    Humidity = current.GetProperty("relative_humidity_2m").GetInt32(),
                    WindSpeed = Math.Round(current.GetProperty("wind_speed_10m").GetDouble()),
                    WeatherCode = weatherCode,
                    IsDay = isDay,
                    HighTemp = Math.Round(highTemp),
                    LowTemp = Math.Round(lowTemp),
                    Description = GetWeatherDescription(weatherCode),
                    Icon = GetWeatherIcon(weatherCode, isDay),
                    LastUpdated = DateTime.Now
                };

                _dispatcher.BeginInvoke(() =>
                {
                    CurrentWeather = info;
                    OnWeatherUpdated?.Invoke(info);
                });
            }
            catch
            {
                _dispatcher.BeginInvoke(() => OnWeatherError?.Invoke());
            }
        }

        private string _cityName = "Unknown";

        private async Task ResolveLocationAsync()
        {
            try
            {
                // Free IP-based geolocation (no API key) - using ip-api.com which has higher rate limits
                string geoJson = await _httpClient.GetStringAsync("http://ip-api.com/json/");
                using var doc = JsonDocument.Parse(geoJson);
                var root = doc.RootElement;

                if (root.GetProperty("status").GetString() == "success")
                {
                    _latitude = root.GetProperty("lat").GetDouble();
                    _longitude = root.GetProperty("lon").GetDouble();
                    _cityName = root.GetProperty("city").GetString() ?? "Unknown";
                    _locationResolved = true;
                }
                else
                {
                    throw new Exception("IP Geolocation failed");
                }
            }
            catch
            {
                // Fallback: default to London if rate limited or offline
                _latitude = 51.5074;
                _longitude = -0.1278;
                _cityName = "London";
                _locationResolved = true;
            }
        }

        public static string GetWeatherDescription(int code) => code switch
        {
            0 => "Clear Sky",
            1 => "Mainly Clear",
            2 => "Partly Cloudy",
            3 => "Overcast",
            45 or 48 => "Foggy",
            51 or 53 or 55 => "Drizzle",
            56 or 57 => "Freezing Drizzle",
            61 or 63 or 65 => "Rain",
            66 or 67 => "Freezing Rain",
            71 or 73 or 75 => "Snowfall",
            77 => "Snow Grains",
            80 or 81 or 82 => "Rain Showers",
            85 or 86 => "Snow Showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm with Hail",
            _ => "Unknown"
        };

        public static string GetWeatherIcon(int code, bool isDay) => code switch
        {
            // Segoe Fluent Icons / MDL2 Assets
            0 => isDay ? "\xE706" : "\xE708",       // Sun / Moon  (E706=Brightness, E708=ClearNight)
            1 => isDay ? "\xE706" : "\xE708",       // Mainly clear
            2 => "\xE9CA",                            // Partly Cloudy (Globe as fallback)
            3 => "\xE753",                            // Overcast (Cloud)
            45 or 48 => "\xE818",                     // Fog
            51 or 53 or 55 => "\xE9C4",               // Drizzle
            61 or 63 or 65 => "\xE9C4",               // Rain
            56 or 57 or 66 or 67 => "\xE9C4",         // Freezing rain
            71 or 73 or 75 or 77 => "\xE9C8",         // Snow
            80 or 81 or 82 => "\xE9C4",               // Rain showers
            85 or 86 => "\xE9C8",                     // Snow showers
            95 or 96 or 99 => "\xE9C6",               // Thunderstorm
            _ => "\xE753"                             // Default cloud
        };

        public void Stop()
        {
            _refreshTimer.Stop();
        }
    }
}
