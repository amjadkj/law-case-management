using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace LawCaseManagement.Wpf
{
    public class ThemeManager : INotifyPropertyChanged
    {
        private static ThemeManager? _instance;
        public static ThemeManager Instance => _instance ??= new ThemeManager();

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged(nameof(IsDarkMode));
                    OnPropertyChanged(nameof(ThemeIcon));
                    OnPropertyChanged(nameof(ThemeTooltip));
                    ApplyTheme(_isDarkMode);
                    SaveThemePreference(_isDarkMode);
                }
            }
        }

        public string ThemeIcon => IsDarkMode ? "🌙" : "☀️";
        public string ThemeTooltip => IsDarkMode ? "Switch to Light Mode" : "Switch to Dark Mode";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public void Initialize()
        {
            bool savedDark = LoadThemePreference();
            _isDarkMode = savedDark;
            ApplyTheme(savedDark);
        }

        public void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
        }

        private void ApplyTheme(bool isDark)
        {
            if (Application.Current == null) return;

            string targetUri = isDark ? "Styles/DarkTheme.xaml" : "Styles/LightTheme.xaml";
            var appResources = Application.Current.Resources;

            // Find existing theme dictionary
            var existing = appResources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("LightTheme.xaml") || d.Source.OriginalString.Contains("DarkTheme.xaml")));

            var newDict = new ResourceDictionary { Source = new Uri(targetUri, UriKind.Relative) };

            if (existing != null)
            {
                int index = appResources.MergedDictionaries.IndexOf(existing);
                appResources.MergedDictionaries[index] = newDict;
            }
            else
            {
                appResources.MergedDictionaries.Insert(0, newDict);
            }
        }

        private string GetConfigFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "LawCaseManagement");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "theme_settings.json");
        }

        private bool LoadThemePreference()
        {
            try
            {
                string path = GetConfigFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("IsDarkMode", out var prop))
                    {
                        return prop.GetBoolean();
                    }
                }
            }
            catch
            {
                // Fallback to light mode on any read error
            }
            return false;
        }

        private void SaveThemePreference(bool isDark)
        {
            try
            {
                string path = GetConfigFilePath();
                string json = JsonSerializer.Serialize(new { IsDarkMode = isDark });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Best effort save
            }
        }
    }
}
