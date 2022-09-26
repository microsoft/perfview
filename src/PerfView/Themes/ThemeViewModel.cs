using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Utilities;

namespace PerfView
{
    public enum Theme
    {
        Light,
        Dark,
        System
    }

    public class ThemeViewModel : INotifyPropertyChanged
    {
        private ConfigData _userConfigData;
        public Theme CurrentTheme { get; private set; }

        public bool IsLightTheme
        {
            get => CurrentTheme == Theme.Light;
            set => SetTheme(Theme.Light);
        }

        public bool IsDarkTheme
        {
            get => CurrentTheme == Theme.Dark;
            set => SetTheme(Theme.Dark);
        }

        public bool IsSystemTheme
        {
            get => CurrentTheme == Theme.System;
            set => SetTheme(Theme.System);
        }

        public class SetThemeCommand : RoutedCommand
        {
            public SetThemeCommand(Theme theme)
            {
                Theme = theme;
            }

            public Theme Theme { get; }
        }

        public static SetThemeCommand SetLightThemeCommand = new SetThemeCommand(Theme.Light);

        public static SetThemeCommand SetDarkThemeCommand = new SetThemeCommand(Theme.Dark);

        public static SetThemeCommand SetSystemThemeCommand = new SetThemeCommand(Theme.System);

        public event PropertyChangedEventHandler PropertyChanged;

        public ThemeViewModel(ConfigData userConfigData)
        {
            _userConfigData = userConfigData;

            if (!Enum.TryParse(userConfigData["Theme"], out Theme theme))
            {
                theme = Theme.Light;
            }

            InitTheme(theme);
        }

        public void InitTheme(Theme theme)
        {
            CurrentTheme = theme;

            if (theme == Theme.Light)
                ApplyResources("Themes/LightTheme.xaml");
            else// if (newTheme == Theme.Dark)
                ApplyResources("Themes/DarkTheme.xaml");

            void ApplyResources(string src)
            {
                var dict = new ResourceDictionary() { Source = new Uri(src, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries[0] = dict;
            }
        }

        public void SetTheme(Theme newTheme)
        {
            if (newTheme == CurrentTheme)
                return;

            Theme oldTheme = CurrentTheme;

            _userConfigData["Theme"] = newTheme.ToString();
            CurrentTheme = newTheme;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"Is{newTheme}Theme"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"Is{oldTheme}Theme"));
        }
    }
}