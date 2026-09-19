using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media;

namespace Mint
{
    public static class ThemeManager
    {
        public static bool IsDarkThemeActive { get; private set; } = true;

        public static void ApplyTheme(AppTheme theme)
        {
            bool useDark = theme switch
            {
                AppTheme.Dark => true,
                AppTheme.Light => false,
                _ => GetWindowsSystemThemeIsDark()
            };

            IsDarkThemeActive = useDark;

            var res = Application.Current.Resources;

            if (useDark)
            {
                // Fluent Dark Theme Palette (Zinc/Slate)
                res["BgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#09090B"));
                res["CardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                res["InputBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A"));
                res["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
                res["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            }
            else
            {
                // Fluent Light Theme Palette
                res["BgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F4F5"));
                res["CardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                res["InputBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F4F5"));
                res["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4E7"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717A"));
                res["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            }
        }

        private static bool GetWindowsSystemThemeIsDark()
        {
            try
            {
                const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                object? registryValue = key?.GetValue("AppsUseLightTheme");
                if (registryValue is int intValue)
                {
                    return intValue == 0; // 0 = Dark, 1 = Light
                }
            }
            catch { }
            return true;
        }
    }
}