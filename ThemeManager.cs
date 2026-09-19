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
                // Windows 11 Fluent Dark Palette (Zinc / Dark Slate)
                res["FlyoutBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                res["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A"));
                res["CardHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#323238"));
                res["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202024"));
                res["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
                res["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                res["MenuBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202024"));
            }
            else
            {
                // Windows 11 Fluent Light Palette
                res["FlyoutBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                res["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                res["CardHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                res["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                res["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                res["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                res["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                res["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
                res["MenuBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
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
