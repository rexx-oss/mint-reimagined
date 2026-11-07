using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace Mint;

public class AppDisplayItem
{
    public AppItem App { get; set; } = new();
    public ImageSource? Icon { get; set; }

    public string Subtitle
    {
        get
        {
            string colText = App.Column > 0 ? $"Col {App.Column}" : "Auto";
            string grpText = !string.IsNullOrWhiteSpace(App.AppGroup) ? $"{App.AppGroup} • " : "";
            return $"{grpText}[{colText}]  •  {App.AppLink}";
        }
    }
}

public class InsertionAdorner : Adorner
{
    public bool IsAfter { get; set; }
    private readonly System.Windows.Media.Pen _pen;
    private readonly System.Windows.Media.Brush _brush;

    public InsertionAdorner(UIElement adornedElement, bool isAfter, System.Windows.Media.Brush brush) : base(adornedElement)
    {
        IsAfter = isAfter;
        _brush = brush;
        _pen = new System.Windows.Media.Pen(brush, 2)
        {
            DashStyle = DashStyles.Dash
        };
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double y = IsAfter ? AdornedElement.RenderSize.Height : 0;
        double width = AdornedElement.RenderSize.Width;

        dc.DrawLine(_pen, new Point(4, y), new Point(width - 4, y));
        dc.DrawEllipse(_brush, null, new Point(4, y), 3, 3);
        dc.DrawEllipse(_brush, null, new Point(width - 4, y), 3, 3);
    }
}

public partial class MainWindow : Window
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps.json");
    private AppSettings _settings = new();
    private readonly ObservableCollection<AppDisplayItem> _displayList = new();
    private readonly WinForms.NotifyIcon _notifyIcon;
    private TrayLauncherWindow? _trayLauncher;
    private AppItem? _editingApp;
    private string _activeGroupFilter = "All";

    // Drag & Drop State
    private Point _dragStartPoint;
    private AppDisplayItem? _draggedItem;
    private AppItem? _draggedBoardApp;
    private InsertionAdorner? _currentAdorner;
    private ListBoxItem? _currentAdornedItem;

    // Controlled Dynamic Auto-Scroll
    private DispatcherTimer? _autoScrollTimer;
    private double _autoScrollDelta = 0;
    private ScrollViewer? _listScrollViewer;

    public MainWindow()
    {
        InitializeComponent();
        LstApps.ItemsSource = _displayList;

        LoadConfig();
        RestoreWindowState();

        var appIcon = LoadMintIcon();
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = appIcon,
            Text = "Mint Launcher",
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => ShowAndRestore();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Right && !_settings.UseClassicTrayMenu)
            {
                OpenModernTrayFlyout();
            }
        };

        _trayLauncher = new TrayLauncherWindow(this);

        InitAutoScroll();
        ThemeManager.ApplyTheme(_settings.Theme);
        SyncPreferences();
        BuildGroupChips();
        RefreshList();
        BuildTrayContextMenu();

        // Open last active view
        SwitchToTab(_settings.ActiveViewTab);

        TrimMemory();
    }

    private void RestoreWindowState()
    {
        if (_settings.IsMaximized)
        {
            this.WindowState = WindowState.Maximized;
        }
        else if (_settings.WindowWidth >= MinWidth && _settings.WindowHeight >= MinHeight)
        {
            this.Width = _settings.WindowWidth;
            this.Height = _settings.WindowHeight;
            this.WindowState = WindowState.Normal;
        }
        else
        {
            var workArea = SystemParameters.WorkArea;
            this.Width = Math.Clamp(workArea.Width * 0.65, 880, 1200);
            this.Height = Math.Clamp(workArea.Height * 0.70, 560, 800);
        }

        this.ClearValue(MaxWidthProperty);
        this.ClearValue(MaxHeightProperty);
    }

    private void SaveCurrentWindowState()
    {
        if (this.WindowState == WindowState.Maximized)
        {
            _settings.IsMaximized = true;
        }
        else if (this.WindowState == WindowState.Normal)
        {
            _settings.IsMaximized = false;
            _settings.WindowWidth = this.ActualWidth;
            _settings.WindowHeight = this.ActualHeight;
        }
        SaveConfig();
    }

    public void ShowAndRestore()
    {
        Show();
        if (_settings.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            if (_settings.WindowWidth >= MinWidth && _settings.WindowHeight >= MinHeight)
            {
                Width = _settings.WindowWidth;
                Height = _settings.WindowHeight;
            }
        }
        Activate();
        TxtSearch.Focus();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        SaveCurrentWindowState();
        Hide();
        TrimMemory();
    }

    public void ExitApplication()
    {
        SaveCurrentWindowState();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void OpenModernTrayFlyout()
    {
        _trayLauncher ??= new TrayLauncherWindow(this);
        _trayLauncher.Open(_settings);
    }

    public static void TrimMemory()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            using var proc = Process.GetCurrentProcess();
            EmptyWorkingSet(proc.Handle);
        }
        catch { }
    }

    private System.Drawing.Icon LoadMintIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mint.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = BitmapFrame.Create(new Uri(iconPath));
                return new System.Drawing.Icon(iconPath);
            }

            if (Environment.ProcessPath != null)
            {
                var extracted = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                if (extracted != null)
                {
                    this.Icon = Imaging.CreateBitmapSourceFromHIcon(extracted.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    return extracted;
                }
            }
        }
        catch { }

        return System.Drawing.SystemIcons.Application;
    }

    private void InitAutoScroll()
    {
        _autoScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        _autoScrollTimer.Tick += (s, e) =>
        {
            if (_listScrollViewer == null)
                _listScrollViewer = FindVisualChild<ScrollViewer>(LstApps);

            if (_listScrollViewer != null && _autoScrollDelta != 0)
            {
                _listScrollViewer.ScrollToVerticalOffset(_listScrollViewer.VerticalOffset + _autoScrollDelta);
            }
        };
    }

    public void BuildTrayContextMenu()
    {
        if (!_settings.UseClassicTrayMenu)
        {
            _notifyIcon.ContextMenuStrip = null;
            return;
        }

        var menu = new WinForms.ContextMenuStrip
        {
            Renderer = new ModernTrayRenderer(),
            ShowImageMargin = true,
            DropShadowEnabled = true
        };

        menu.HandleCreated += (s, e) =>
        {
            int roundPreference = 2;
            DwmSetWindowAttribute(menu.Handle, 33, ref roundPreference, sizeof(int));
        };

        bool isDark = ThemeManager.IsDarkThemeActive;
        var font = new System.Drawing.Font("Segoe UI Semibold", 9.5f);
        var foreColor = isDark ? System.Drawing.Color.FromArgb(237, 237, 240) : System.Drawing.Color.FromArgb(30, 34, 41);

        var groups = _settings.Apps
            .Where(a => !string.IsNullOrWhiteSpace(a.AppGroup))
            .GroupBy(a => a.AppGroup)
            .OrderBy(g => g.Key);

        foreach (var grp in groups)
        {
            var groupItem = new WinForms.ToolStripMenuItem(grp.Key)
            {
                Font = font,
                ForeColor = foreColor,
                Padding = new WinForms.Padding(6, 4, 6, 4)
            };

            foreach (var app in grp)
            {
                var icon = IconHelper.GetGdiIcon(app.AppLink, app.CustomIconPath, app.AppTitle);
                var item = new WinForms.ToolStripMenuItem(app.AppTitle, icon)
                {
                    Font = font,
                    ForeColor = foreColor,
                    Padding = new WinForms.Padding(6, 4, 6, 4)
                };
                item.Click += (s, e) => LaunchApp(app);
                groupItem.DropDownItems.Add(item);
            }

            menu.Items.Add(groupItem);
        }

        if (groups.Any()) menu.Items.Add(new WinForms.ToolStripSeparator());

        var ungrouped = _settings.Apps.Where(a => string.IsNullOrWhiteSpace(a.AppGroup)).ToList();
        foreach (var app in ungrouped)
        {
            var icon = IconHelper.GetGdiIcon(app.AppLink, app.CustomIconPath, app.AppTitle);
            var item = new WinForms.ToolStripMenuItem(app.AppTitle, icon)
            {
                Font = font,
                ForeColor = foreColor,
                Padding = new WinForms.Padding(6, 4, 6, 4)
            };
            item.Click += (s, e) => LaunchApp(app);
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0) menu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem("Exit", null)
        {
            Font = font,
            ForeColor = foreColor,
            Padding = new WinForms.Padding(6, 4, 6, 4)
        };
        exitItem.Click += (s, e) => ExitApplication();
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath)) ?? new AppSettings();
            }
        }
        catch { _settings = new AppSettings(); }

        if (_settings.Apps.Count == 0)
        {
            _settings.Apps = new List<AppItem>
            {
                new AppItem { AppTitle = "Notepad", AppLink = "notepad.exe", AppGroup = "Tools", Column = 1 },
                new AppItem { AppTitle = "Calculator", AppLink = "calc.exe", AppGroup = "Tools", Column = 2 },
                new AppItem { AppTitle = "Command Prompt", AppLink = "cmd.exe", AppGroup = "System", Column = 3 }
            };
            SaveConfig();
        }

        ApplyAutoStart(_settings.StartWithWindows);
        ChkAutoStart.IsChecked = _settings.StartWithWindows;
    }

    private void SaveConfig()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
            IconHelper.CleanupOrphanedIcons(_settings.Apps.Select(a => a.AppTitle));
        }
        catch { }
    }

    private void SyncPreferences()
    {
        RbSystem.IsChecked = _settings.Theme == AppTheme.System;
        RbDark.IsChecked = _settings.Theme == AppTheme.Dark;
        RbLight.IsChecked = _settings.Theme == AppTheme.Light;

        RbTrayModern.IsChecked = !_settings.UseClassicTrayMenu;
        RbTrayClassic.IsChecked = _settings.UseClassicTrayMenu;

        TxtColCountDisplay.Text = Math.Max(1, _settings.TrayColumns).ToString();
    }

    // --- TAB SWITCHING (Classic vs Column Board) ---
    private void BtnTabClassic_Click(object sender, RoutedEventArgs e) => SwitchToTab(0);
    private void BtnTabColumns_Click(object sender, RoutedEventArgs e) => SwitchToTab(1);

    private void SwitchToTab(int tabIndex)
    {
        _settings.ActiveViewTab = tabIndex;
        SaveConfig();

        if (tabIndex == 0)
        {
            ViewClassic.Visibility = Visibility.Visible;
            ViewColumns.Visibility = Visibility.Collapsed;
            BtnTabClassic.Background = (Brush)Application.Current.Resources["AccentColor"];
            BtnTabClassic.Foreground = Brushes.White;
            BtnTabColumns.Background = Brushes.Transparent;
            BtnTabColumns.Foreground = (Brush)Application.Current.Resources["TextSecondary"];
            RefreshList(TxtSearch.Text);
        }
        else
        {
            ViewClassic.Visibility = Visibility.Collapsed;
            ViewColumns.Visibility = Visibility.Visible;
            BtnTabColumns.Background = (Brush)Application.Current.Resources["AccentColor"];
            BtnTabColumns.Foreground = Brushes.White;
            BtnTabClassic.Background = Brushes.Transparent;
            BtnTabClassic.Foreground = (Brush)Application.Current.Resources["TextSecondary"];
            BuildMultiColumnBoard();
        }
    }

    // --- VISUAL MULTI-COLUMN BOARD LOGIC ---
    private void BuildMultiColumnBoard()
    {
        PanelIconDock.Children.Clear();
        PanelBoardColumns.Children.Clear();

        int totalCols = Math.Max(1, _settings.TrayColumns);
        TxtColCountDisplay.Text = totalCols.ToString();

        // 1. Populate Available Apps Icon Dock (Compact Icons)
        foreach (var app in _settings.Apps)
        {
            var iconBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["InputBg"],
                BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                BorderThickness = new Thickness(1),
                Margin = new Thickness(3),
                Cursor = Cursors.Hand,
                ToolTip = $"{app.AppTitle}\nColumn: {(app.Column > 0 ? app.Column.ToString() : "Auto")}\n{app.AppLink}"
            };

            var img = new System.Windows.Controls.Image { Width = 22, Height = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            IconHelper.GetIconAsync(app.AppLink, app.CustomIconPath, app.AppTitle).ContinueWith(t =>
            {
                if (t.Result != null) Dispatcher.Invoke(() => img.Source = t.Result);
            });
            iconBox.Child = img;

            // Start Drag from Icon Dock
            iconBox.PreviewMouseLeftButtonDown += (s, e) => _dragStartPoint = e.GetPosition(null);
            iconBox.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var diff = _dragStartPoint - e.GetPosition(null);
                    if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4)
                    {
                        _draggedBoardApp = app;
                        DragDrop.DoDragDrop(iconBox, app, DragDropEffects.Move);
                        _draggedBoardApp = null;
                    }
                }
            };

            PanelIconDock.Children.Add(iconBox);
        }

        // 2. Build Column Drop Target Boxes
        var colBuckets = new List<List<AppItem>>();
        for (int i = 0; i < totalCols; i++) colBuckets.Add(new List<AppItem>());

        foreach (var app in _settings.Apps)
        {
            if (app.Column >= 1 && app.Column <= totalCols)
                colBuckets[app.Column - 1].Add(app);
        }

        for (int i = 0; i < totalCols; i++)
        {
            int colNum = i + 1;

            var colBorder = new Border
            {
                Width = 200,
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.Resources["InputBg"],
                BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                AllowDrop = true,
                Tag = colNum
            };

            var colGrid = new Grid();
            colGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            colGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Column Header with Title, Count Badge, and [X] Delete Column button
            var colHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleTxt = new TextBlock { Text = $"Column {colNum}", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextPrimary"] };
            var badgeTxt = new TextBlock { Text = $"{colBuckets[i].Count} apps", FontSize = 10, Foreground = (Brush)Application.Current.Resources["TextSecondary"], VerticalAlignment = VerticalAlignment.Center };
            
            // Delete Column [X] button
            var btnDeleteCol = new Button
            {
                Content = "✕",
                ToolTip = $"Delete Column {colNum}",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnDeleteCol.MouseEnter += (s, e) => btnDeleteCol.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            btnDeleteCol.MouseLeave += (s, e) => btnDeleteCol.Foreground = (Brush)Application.Current.Resources["TextSecondary"];
            btnDeleteCol.Click += (s, e) => DeleteSpecificColumn(colNum);

            Grid.SetColumn(titleTxt, 0);
            Grid.SetColumn(badgeTxt, 1);
            Grid.SetColumn(btnDeleteCol, 3);
            colHeader.Children.Add(titleTxt);
            colHeader.Children.Add(badgeTxt);
            colHeader.Children.Add(btnDeleteCol);
            Grid.SetRow(colHeader, 0);

            // Column App List
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listStack = new StackPanel { MinHeight = 150 };

            foreach (var app in colBuckets[i])
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBg"],
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(0),
                    AllowDrop = true
                };

                var cardGrid = new Grid();
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var cImg = new System.Windows.Controls.Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
                IconHelper.GetIconAsync(app.AppLink, app.CustomIconPath, app.AppTitle).ContinueWith(t =>
                {
                    if (t.Result != null) Dispatcher.Invoke(() => cImg.Source = t.Result);
                });
                Grid.SetColumn(cImg, 0);

                var cTxt = new TextBlock { Text = app.AppTitle, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = (Brush)Application.Current.Resources["TextPrimary"], VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(6, 0, 4, 0) };
                Grid.SetColumn(cTxt, 1);

                // Quick remove button
                var btnRemove = new Button { Content = "✕", Background = Brushes.Transparent, Foreground = (Brush)Application.Current.Resources["TextSecondary"], BorderThickness = new Thickness(0), Padding = new Thickness(4, 0, 4, 0), FontSize = 10, Cursor = Cursors.Hand };
                btnRemove.Click += (s, e) =>
                {
                    app.Column = 0;
                    SaveConfig();
                    BuildMultiColumnBoard();
                    BuildTrayContextMenu();
                };
                Grid.SetColumn(btnRemove, 2);

                cardGrid.Children.Add(cImg);
                cardGrid.Children.Add(cTxt);
                cardGrid.Children.Add(btnRemove);
                card.Child = cardGrid;

                // Start Drag
                card.PreviewMouseLeftButtonDown += (s, e) => _dragStartPoint = e.GetPosition(null);
                card.MouseMove += (s, e) =>
                {
                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        var diff = _dragStartPoint - e.GetPosition(null);
                        if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4)
                        {
                            _draggedBoardApp = app;
                            DragDrop.DoDragDrop(card, app, DragDropEffects.Move);
                            _draggedBoardApp = null;
                        }
                    }
                };

                // Reordering inside column by dropping directly on another card
                card.DragOver += (s, e) =>
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                };

                card.Drop += (s, e) =>
                {
                    var dragged = _draggedBoardApp ?? e.Data.GetData(typeof(AppItem)) as AppItem;
                    if (dragged != null && dragged != app)
                    {
                        dragged.Column = colNum;

                        Point pos = e.GetPosition(card);
                        bool isAfter = pos.Y > card.ActualHeight / 2;

                        int oldIndex = _settings.Apps.IndexOf(dragged);
                        int targetIndex = _settings.Apps.IndexOf(app);

                        if (oldIndex >= 0 && targetIndex >= 0)
                        {
                            if (isAfter && targetIndex < oldIndex) targetIndex++;
                            else if (!isAfter && targetIndex > oldIndex) targetIndex--;

                            if (oldIndex != targetIndex && targetIndex >= 0 && targetIndex < _settings.Apps.Count)
                            {
                                _settings.Apps.RemoveAt(oldIndex);
                                _settings.Apps.Insert(targetIndex, dragged);
                            }
                        }

                        SaveConfig();
                        BuildMultiColumnBoard();
                        BuildTrayContextMenu();
                        e.Handled = true;
                    }
                };

                listStack.Children.Add(card);
            }

            scroll.Content = listStack;
            Grid.SetRow(scroll, 1);
            colGrid.Children.Add(colHeader);
            colGrid.Children.Add(scroll);
            colBorder.Child = colGrid;

            // Handle Drop into this Column (on empty container space)
            colBorder.DragOver += (s, e) =>
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            };

            colBorder.Drop += (s, e) =>
            {
                var targetApp = _draggedBoardApp ?? e.Data.GetData(typeof(AppItem)) as AppItem;
                if (targetApp != null)
                {
                    targetApp.Column = colNum;

                    // Move to end of this column in the order
                    int lastInCol = _settings.Apps.FindLastIndex(a => a.Column == colNum && a != targetApp);
                    if (lastInCol >= 0)
                    {
                        int oldIdx = _settings.Apps.IndexOf(targetApp);
                        if (oldIdx >= 0)
                        {
                            _settings.Apps.RemoveAt(oldIdx);
                            int insertIdx = lastInCol > oldIdx ? lastInCol : lastInCol + 1;
                            _settings.Apps.Insert(Math.Min(insertIdx, _settings.Apps.Count), targetApp);
                        }
                    }

                    SaveConfig();
                    BuildMultiColumnBoard();
                    BuildTrayContextMenu();
                }
            };

            PanelBoardColumns.Children.Add(colBorder);
        }
    }

    // Delete a specific column with warning prompt if it contains apps
    private void DeleteSpecificColumn(int colNum)
    {
        if (_settings.TrayColumns <= 1)
        {
            MessageBox.Show("You must have at least 1 column.", "Mint", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var appsInCol = _settings.Apps.Where(a => a.Column == colNum).ToList();
        if (appsInCol.Count > 0)
        {
            var result = MessageBox.Show(
                $"Column {colNum} contains {appsInCol.Count} app(s).\n\nDo you want to delete this column and move its apps back to the unassigned dock?",
                "Delete Column",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;
        }

        // 1. Reset apps in the deleted column to Auto (Column 0)
        foreach (var app in appsInCol)
        {
            app.Column = 0;
        }

        // 2. Shift subsequent columns down by 1 (e.g. Column 3 becomes Column 2)
        foreach (var app in _settings.Apps.Where(a => a.Column > colNum))
        {
            app.Column--;
        }

        // 3. Decrement total column count
        _settings.TrayColumns--;
        SaveConfig();
        BuildMultiColumnBoard();
        BuildTrayContextMenu();
    }

    private void DockPool_DragOver(object sender, DragEventArgs e) => e.Effects = DragDropEffects.Move;

    private void DockPool_Drop(object sender, DragEventArgs e)
    {
        var targetApp = _draggedBoardApp ?? e.Data.GetData(typeof(AppItem)) as AppItem;
        if (targetApp != null)
        {
            targetApp.Column = 0; // Reset to Auto
            SaveConfig();
            BuildMultiColumnBoard();
            BuildTrayContextMenu();
        }
    }

    private void BtnIncCols_Click(object sender, RoutedEventArgs e)
    {
        _settings.TrayColumns++;
        SaveConfig();
        BuildMultiColumnBoard();
        BuildTrayContextMenu();
    }

    private void BtnDecCols_Click(object sender, RoutedEventArgs e)
    {
        // Deleting the last column triggers the check and warning if it contains apps
        DeleteSpecificColumn(_settings.TrayColumns);
    }

    private void BtnAutoBalance_Click(object sender, RoutedEventArgs e)
    {
        int totalCols = Math.Max(1, _settings.TrayColumns);
        for (int i = 0; i < _settings.Apps.Count; i++)
        {
            _settings.Apps[i].Column = (i % totalCols) + 1;
        }
        SaveConfig();
        BuildMultiColumnBoard();
        BuildTrayContextMenu();
    }

    // --- PREFERENCES LOGIC ---
    private void RbTrayStyle_Checked(object sender, RoutedEventArgs e)
    {
        _settings.UseClassicTrayMenu = RbTrayClassic.IsChecked == true;
        SaveConfig();
        BuildTrayContextMenu();
    }

    private void RbTheme_Checked(object sender, RoutedEventArgs e)
    {
        if (RbSystem.IsChecked == true) _settings.Theme = AppTheme.System;
        else if (RbDark.IsChecked == true) _settings.Theme = AppTheme.Dark;
        else if (RbLight.IsChecked == true) _settings.Theme = AppTheme.Light;

        ThemeManager.ApplyTheme(_settings.Theme);
        SaveConfig();
        BuildGroupChips();
        BuildTrayContextMenu();
        if (_settings.ActiveViewTab == 1) BuildMultiColumnBoard();
    }

    private void BuildGroupChips()
    {
        PanelGroups.Children.Clear();

        var groups = _settings.Apps.Select(a => a.AppGroup)
                                   .Where(g => !string.IsNullOrWhiteSpace(g))
                                   .Distinct()
                                   .OrderBy(g => g)
                                   .ToList();

        groups.Insert(0, "All");

        foreach (var group in groups)
        {
            var btn = new Button
            {
                Content = group,
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Background = (group == _activeGroupFilter) 
                    ? (Brush)Application.Current.Resources["AccentColor"] 
                    : (Brush)Application.Current.Resources["InputBg"],
                Foreground = (group == _activeGroupFilter) 
                    ? Brushes.White 
                    : (Brush)Application.Current.Resources["TextSecondary"],
                BorderThickness = new Thickness(0)
            };

            btn.Click += (s, e) =>
            {
                _activeGroupFilter = group;
                BuildGroupChips();
                RefreshList(TxtSearch.Text);
            };

            PanelGroups.Children.Add(btn);
        }
    }

    private async void RefreshList(string filter = "")
    {
        _displayList.Clear();
        var query = _settings.Apps.AsEnumerable();

        if (_activeGroupFilter != "All")
            query = query.Where(a => a.AppGroup.Equals(_activeGroupFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter))
            query = query.Where(a => a.AppTitle.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                     a.AppGroup.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var app in query)
        {
            var icon = await IconHelper.GetIconAsync(app.AppLink, app.CustomIconPath, app.AppTitle);
            _displayList.Add(new AppDisplayItem { App = app, Icon = icon });
        }

        TxtFooterCount.Text = $"{_displayList.Count} of {_settings.Apps.Count} Apps";

        if (_displayList.Count > 0 && LstApps.SelectedIndex == -1)
            LstApps.SelectedIndex = 0;
    }

    private async void UpdateEditIconPreview()
    {
        string path = TxtCustomIcon.Text.Trim();
        if (string.IsNullOrWhiteSpace(path)) path = TxtPath.Text.Trim();
        string title = TxtTitle.Text.Trim();

        ImgIconPreview.Source = await IconHelper.GetIconAsync(path, TxtCustomIcon.Text.Trim(), title);
    }

    private void TxtPath_TextChanged(object sender, TextChangedEventArgs e) => UpdateEditIconPreview();
    private void TxtCustomIcon_TextChanged(object sender, TextChangedEventArgs e) => UpdateEditIconPreview();

    private void LaunchApp(AppItem app, bool asAdmin = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = app.AppLink,
                Arguments = app.AppParams,
                UseShellExecute = true
            };

            if (asAdmin) psi.Verb = "runas";

            if (File.Exists(app.AppLink))
                psi.WorkingDirectory = Path.GetDirectoryName(app.AppLink);
            else if (Directory.Exists(app.AppLink))
                psi.WorkingDirectory = app.AppLink;

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not launch item:\n{ex.Message}", "Mint", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshList(TxtSearch.Text.Trim());

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && LstApps.Items.Count > 0)
        {
            LstApps.Focus();
            LstApps.SelectedIndex = 0;
        }
        else if (e.Key == Key.Enter && LstApps.SelectedItem is AppDisplayItem item)
        {
            LaunchApp(item.App);
        }
    }

    private void LstApps_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LstApps.SelectedItem is AppDisplayItem item)
            LaunchApp(item.App);
    }

    private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstApps.SelectedItem is AppDisplayItem item)
            LaunchApp(item.App);
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    // --- Dynamic Drag Auto-Scroll (Classic Tab) ---
    private void LstApps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;

        if (FindVisualParent<ScrollBar>(hit) != null)
        {
            _draggedItem = null;
            return;
        }

        var listBoxItem = FindVisualParent<ListBoxItem>(hit);
        if (listBoxItem != null)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedItem = listBoxItem.DataContext as AppDisplayItem;
        }
        else
        {
            _draggedItem = null;
        }
    }

    private void LstApps_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            try
            {
                DragDrop.DoDragDrop(LstApps, _draggedItem, DragDropEffects.Move);
            }
            finally
            {
                StopAutoScroll();
                RemoveInsertionAdorner();
                _draggedItem = null;
            }
        }
    }

    private void LstApps_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(AppDisplayItem)))
        {
            e.Effects = DragDropEffects.None;
            StopAutoScroll();
            RemoveInsertionAdorner();
            return;
        }

        e.Effects = DragDropEffects.Move;

        Point mousePos = e.GetPosition(LstApps);
        Point windowPos = e.GetPosition(this);
        const double edgeThreshold = 35.0;

        double dragDistance = Math.Abs(windowPos.Y - _dragStartPoint.Y);
        double distanceSpeedBoost = Math.Clamp(dragDistance / 60.0, 1.0, 8.0);

        if (mousePos.Y >= 0 && mousePos.Y < edgeThreshold)
        {
            double penetration = Math.Max(1.0, edgeThreshold - mousePos.Y);
            double speed = (1.5 + (penetration * 0.35)) * distanceSpeedBoost;
            _autoScrollDelta = -Math.Clamp(speed, 2.0, 32.0);
            if (!_autoScrollTimer!.IsEnabled) _autoScrollTimer.Start();
        }
        else if (mousePos.Y > LstApps.ActualHeight - edgeThreshold && mousePos.Y <= LstApps.ActualHeight)
        {
            double penetration = Math.Max(1.0, mousePos.Y - (LstApps.ActualHeight - edgeThreshold));
            double speed = (1.5 + (penetration * 0.35)) * distanceSpeedBoost;
            _autoScrollDelta = Math.Clamp(speed, 2.0, 32.0);
            if (!_autoScrollTimer!.IsEnabled) _autoScrollTimer.Start();
        }
        else
        {
            StopAutoScroll();
        }

        var hit = e.OriginalSource as DependencyObject;
        var targetItem = FindVisualParent<ListBoxItem>(hit);

        if (targetItem != null)
        {
            Point pos = e.GetPosition(targetItem);
            bool isAfter = pos.Y > targetItem.ActualHeight / 2;
            ShowInsertionAdorner(targetItem, isAfter);
        }
        else
        {
            RemoveInsertionAdorner();
        }
    }

    private void LstApps_DragLeave(object sender, DragEventArgs e)
    {
        if (!LstApps.IsMouseOver)
        {
            StopAutoScroll();
            RemoveInsertionAdorner();
        }
    }

    private void StopAutoScroll()
    {
        _autoScrollDelta = 0;
        if (_autoScrollTimer != null && _autoScrollTimer.IsEnabled)
            _autoScrollTimer.Stop();
    }

    private void LstApps_Drop(object sender, DragEventArgs e)
    {
        StopAutoScroll();
        RemoveInsertionAdorner();

        if (e.Data.GetData(typeof(AppDisplayItem)) is AppDisplayItem droppedData)
        {
            var hit = e.OriginalSource as DependencyObject;
            var targetItem = FindVisualParent<ListBoxItem>(hit);

            if (targetItem?.DataContext is AppDisplayItem targetDisplay)
            {
                Point pos = e.GetPosition(targetItem);
                bool isAfter = pos.Y > targetItem.ActualHeight / 2;

                int oldIndex = _settings.Apps.IndexOf(droppedData.App);
                int targetIndex = _settings.Apps.IndexOf(targetDisplay.App);

                if (oldIndex >= 0 && targetIndex >= 0)
                {
                    if (isAfter && targetIndex < oldIndex) targetIndex++;
                    else if (!isAfter && targetIndex > oldIndex) targetIndex--;

                    if (oldIndex != targetIndex && targetIndex >= 0 && targetIndex < _settings.Apps.Count)
                    {
                        _settings.Apps.RemoveAt(oldIndex);
                        _settings.Apps.Insert(targetIndex, droppedData.App);

                        SaveConfig();
                        RefreshList(TxtSearch.Text);
                        BuildTrayContextMenu();

                        var display = _displayList.FirstOrDefault(d => d.App == droppedData.App);
                        if (display != null) LstApps.SelectedItem = display;
                    }
                }
            }
        }
    }

    private void ShowInsertionAdorner(ListBoxItem item, bool isAfter)
    {
        if (_currentAdornedItem == item && _currentAdorner?.IsAfter == isAfter)
            return;

        RemoveInsertionAdorner();

        var layer = AdornerLayer.GetAdornerLayer(item);
        if (layer != null)
        {
            var brush = (Brush)Application.Current.Resources["AccentColor"];
            _currentAdorner = new InsertionAdorner(item, isAfter, brush);
            _currentAdornedItem = item;
            layer.Add(_currentAdorner);
        }
    }

    private void RemoveInsertionAdorner()
    {
        if (_currentAdornedItem != null && _currentAdorner != null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_currentAdornedItem);
            layer?.Remove(_currentAdorner);
            _currentAdorner = null;
            _currentAdornedItem = null;
        }
    }

    private void BtnSortAZ_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Sort all apps alphabetically from A to Z?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _settings.Apps = _settings.Apps.OrderBy(a => a.AppTitle).ToList();
            SaveConfig();
            RefreshList(TxtSearch.Text);
            BuildTrayContextMenu();
        }
    }

    private void MenuLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppDisplayItem item) LaunchApp(item.App);
    }

    private void MenuRunAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppDisplayItem item) LaunchApp(item.App, true);
    }

    private void MenuOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppDisplayItem item && (File.Exists(item.App.AppLink) || Directory.Exists(item.App.AppLink)))
            Process.Start("explorer.exe", $"/select,\"{item.App.AppLink}\"");
    }

    private void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppDisplayItem item)
        {
            _editingApp = item.App;
            TxtTitle.Text = item.App.AppTitle;
            TxtPath.Text = item.App.AppLink;
            TxtArgs.Text = item.App.AppParams;
            TxtGroup.Text = item.App.AppGroup;
            TxtCustomIcon.Text = item.App.CustomIconPath;
            UpdateEditIconPreview();
        }
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is AppDisplayItem item)
        {
            IconHelper.InvalidateCache(item.App.AppTitle);
            _settings.Apps.Remove(item.App);
            SaveConfig();
            BuildGroupChips();
            RefreshList(TxtSearch.Text);
            BuildTrayContextMenu();
            if (_settings.ActiveViewTab == 1) BuildMultiColumnBoard();
        }
    }

    private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Target",
            Filter = "All Files (*.*)|*.*|Programs & Shortcuts (*.exe;*.lnk)|*.exe;*.lnk"
        };

        if (dlg.ShowDialog() == true)
        {
            string file = dlg.FileName;
            if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var (target, args) = ShortcutHelper.ResolveShortcut(file);
                TxtPath.Text = target;
                TxtArgs.Text = args;
                TxtTitle.Text = Path.GetFileNameWithoutExtension(file);
            }
            else
            {
                TxtPath.Text = file;
                TxtTitle.Text = Path.GetFileNameWithoutExtension(file);
            }
            UpdateEditIconPreview();
        }
    }

    private void BtnBrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Icon",
            Filter = "Icons & Images (*.ico;*.png;*.exe;*.dll)|*.ico;*.png;*.exe;*.dll|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            TxtCustomIcon.Text = dlg.FileName;
            UpdateEditIconPreview();
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text) || string.IsNullOrWhiteSpace(TxtPath.Text))
        {
            MessageBox.Show("Please provide a Title and Target path.", "Mint", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_editingApp != null)
        {
            IconHelper.InvalidateCache(_editingApp.AppTitle);
            IconHelper.InvalidateCache(TxtTitle.Text.Trim());

            _editingApp.AppTitle = TxtTitle.Text.Trim();
            _editingApp.AppLink = TxtPath.Text.Trim();
            _editingApp.AppParams = TxtArgs.Text.Trim();
            _editingApp.AppGroup = TxtGroup.Text.Trim();
            _editingApp.CustomIconPath = TxtCustomIcon.Text.Trim();
            _editingApp = null;
        }
        else
        {
            IconHelper.InvalidateCache(TxtTitle.Text.Trim());

            _settings.Apps.Add(new AppItem
            {
                AppTitle = TxtTitle.Text.Trim(),
                AppLink = TxtPath.Text.Trim(),
                AppParams = TxtArgs.Text.Trim(),
                AppGroup = TxtGroup.Text.Trim(),
                CustomIconPath = TxtCustomIcon.Text.Trim()
            });
        }

        SaveConfig();
        BuildGroupChips();
        RefreshList();
        BuildTrayContextMenu();
        BtnClear_Click(this, new RoutedEventArgs());
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _editingApp = null;
        TxtTitle.Clear();
        TxtPath.Clear();
        TxtArgs.Clear();
        TxtGroup.Clear();
        TxtCustomIcon.Clear();
        ImgIconPreview.Source = null;
    }

    private void ApplyAutoStart(bool enable)
    {
        try
        {
            const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key == null) return;

            string? currentVal = key.GetValue("MintLauncher") as string;

            if (enable)
            {
                if (!string.IsNullOrEmpty(Environment.ProcessPath))
                {
                    string expectedVal = $"\"{Environment.ProcessPath}\"";
                    if (!string.Equals(currentVal, expectedVal, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue("MintLauncher", expectedVal);
                    }
                }
            }
            else
            {
                if (currentVal != null)
                {
                    key.DeleteValue("MintLauncher", false);
                }
            }
        }
        catch { }
    }

    private void ChkAutoStart_Click(object sender, RoutedEventArgs e)
    {
        bool enable = ChkAutoStart.IsChecked ?? false;
        _settings.StartWithWindows = enable;
        SaveConfig();
        ApplyAutoStart(enable);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                string file = files[0];
                string title = Path.GetFileNameWithoutExtension(file);
                string path = file;
                string args = "";

                if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    (path, args) = ShortcutHelper.ResolveShortcut(file);

                _settings.Apps.Add(new AppItem { AppTitle = title, AppLink = path, AppParams = args });
                SaveConfig();
                BuildGroupChips();
                RefreshList();
                BuildTrayContextMenu();
            }
        }
    }
}
