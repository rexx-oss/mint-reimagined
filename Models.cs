using System;
using System.Collections.Generic;

namespace Mint;

public enum AppTheme
{
    System,
    Dark,
    Light
}

public class AppItem
{
    public string AppTitle { get; set; } = string.Empty;
    public string AppLink { get; set; } = string.Empty;
    public string AppGroup { get; set; } = string.Empty;
    public string AppParams { get; set; } = string.Empty;
    public string CustomIconPath { get; set; } = string.Empty;
    // 0 = Auto (Distributed), 1 = Column 1, 2 = Column 2, etc.
    public int Column { get; set; } = 0;
}

public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool StartWithWindows { get; set; } = false;
    public bool UseClassicTrayMenu { get; set; } = false;
    public int TrayColumns { get; set; } = 3; // 2, 3, or 4
    public List<AppItem> Apps { get; set; } = new();
}
