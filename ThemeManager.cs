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
                res["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F0F12"));
                res["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181C"));
                res["CardHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#26262C"));
                res["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222228"));
                res["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E36"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F4F6"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E98"));
                res["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
            }
            else
            {
                res["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F7"));
                res["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                res["CardHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBECEF"));
                res["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F1F4"));
                res["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCDDE2"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141416"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B6B76"));
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
