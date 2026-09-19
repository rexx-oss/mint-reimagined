using System;
using System.Collections.Generic;

namespace Mint
{
    public enum AppTheme
    {
        System,
        Dark,
        Light
    }

    public class AppItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string AppTitle { get; set; } = string.Empty;
        public string AppLink { get; set; } = string.Empty;
        public string AppGroup { get; set; } = string.Empty;
        public string AppParams { get; set; } = string.Empty;
        public string CustomIconPath { get; set; } = string.Empty;
    }

    public class AppSettings
    {
        public AppTheme Theme { get; set; } = AppTheme.System;
        public bool StartWithWindows { get; set; } = false;
        public List<string> Groups { get; set; } = new();
        public List<AppItem> Apps { get; set; } = new();
    }
}