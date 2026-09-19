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
                // Modern Charcoal Dark (Muted, Zero harsh contrast)
                res["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#101014"));
                res["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181D"));
                res["CardHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#24242C"));
                res["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E25"));
                res["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C2C35"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDEDF0"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B8B98"));
                res["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            }
            else
            {
                // Eye-Friendly Soft Light (Warm Slate, No blinding pure #FFFFFF)
                res["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBECEF"));
                res["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F5F7"));
                res["CardHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                res["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EAEF"));
                res["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D2D5DC"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2229"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606773"));
                res["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            }
        }

        private static bool GetWindowsSystemThemeIsDark()
        {
            try
            {
                const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                object? val = key?.GetValue("AppsUseLightTheme");
                if (val is int intVal) return intVal == 0;
            }
            catch { }
            return true;
        }
    }
}
