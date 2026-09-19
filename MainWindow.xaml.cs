using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace Mint;

public class AppDisplayItem
{
    public AppItem App { get; set; } = new();
    public System.Windows.Media.ImageSource? Icon { get; set; }
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
            DashStyle = System.Windows.Media.DashStyles.Dash
        };
        IsHitTestVisible = false;
    }

    protected override void OnRender(System.Windows.Media.DrawingContext dc)
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
    private readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps.json");
    private AppSettings _settings = new();
    private readonly ObservableCollection<AppDisplayItem> _displayList = new();
    private readonly WinForms.NotifyIcon _notifyIcon;
    private AppItem? _editingApp;
    private string _activeGroupFilter = "All";

    // Drag & Drop State
    private Point _dragStartPoint;
    private AppDisplayItem? _draggedItem;
    private InsertionAdorner? _currentAdorner;
    private ListBoxItem? _currentAdornedItem;

    // Auto-scroll when dragging near edges
    private DispatcherTimer? _autoScrollTimer;
    private double _autoScrollDelta = 0;
    private ScrollViewer? _listScrollViewer;

    public MainWindow()
    {
        InitializeComponent();
        LstApps.ItemsSource = _displayList;

        // Load Mint icon for both window and system tray
        var appIcon = LoadMintIcon();
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = appIcon,
            Text = "Mint Launcher",
            Visible = true
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                ShowAndRestore();
            }
        };
        _notifyIcon.DoubleClick += (s, e) => ShowAndRestore();

        InitAutoScroll();
        LoadConfig();
        ThemeManager.ApplyTheme(_settings.Theme);
        SyncThemeRadios();
        BuildGroupChips();
        RefreshList();
        BuildTrayContextMenu();
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
            Interval = TimeSpan.FromMilliseconds(35)
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
        var menu = new WinForms.ContextMenuStrip
        {
            Renderer = new ModernTrayRenderer(),
            ShowImageMargin = true
        };

        bool isDark = ThemeManager.IsDarkThemeActive;
        var font = new System.Drawing.Font("Segoe UI Semibold", 9.5f);
        var foreColor = isDark ? System.Drawing.Color.FromArgb(237, 237, 240) : System.Drawing.Color.FromArgb(30, 34, 41);

        // Grouped apps with submenus
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

        // Ungrouped apps
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

        // Exit
        var exitItem = new WinForms.ToolStripMenuItem("Exit", null)
        {
            Font = font,
            ForeColor = foreColor,
            Padding = new WinForms.Padding(6, 4, 6, 4)
        };
        exitItem.Click += (s, e) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void ShowAndRestore()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        TxtSearch.Focus();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
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
                new AppItem { AppTitle = "Notepad", AppLink = "notepad.exe", AppGroup = "Tools" },
                new AppItem { AppTitle = "Calculator", AppLink = "calc.exe", AppGroup = "Tools" },
                new AppItem { AppTitle = "Command Prompt", AppLink = "cmd.exe", AppGroup = "System" }
            };
            SaveConfig();
        }

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

    private void SyncThemeRadios()
    {
        RbSystem.IsChecked = _settings.Theme == AppTheme.System;
        RbDark.IsChecked = _settings.Theme == AppTheme.Dark;
        RbLight.IsChecked = _settings.Theme == AppTheme.Light;
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
                    ? (System.Windows.Media.Brush)Application.Current.Resources["AccentColor"] 
                    : (System.Windows.Media.Brush)Application.Current.Resources["InputBg"],
                Foreground = (group == _activeGroupFilter) 
                    ? System.Windows.Media.Brushes.White 
                    : (System.Windows.Media.Brush)Application.Current.Resources["TextSecondary"],
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

    // --- Visual Tree Helpers ---
    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    // --- Drag & Drop: Controlled Auto-Scroll & Insertion Adorner ---
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

        // Controlled Auto-Scroll: smooth ramp between 1.2px and 4.0px per tick
        Point mousePos = e.GetPosition(LstApps);
        const double edgeThreshold = 30.0;

        if (mousePos.Y >= 0 && mousePos.Y < edgeThreshold)
        {
            double speedFactor = (edgeThreshold - mousePos.Y) / edgeThreshold;
            _autoScrollDelta = -(1.2 + (speedFactor * 2.8));
            if (!_autoScrollTimer!.IsEnabled) _autoScrollTimer.Start();
        }
        else if (mousePos.Y > LstApps.ActualHeight - edgeThreshold && mousePos.Y <= LstApps.ActualHeight)
        {
            double speedFactor = (mousePos.Y - (LstApps.ActualHeight - edgeThreshold)) / edgeThreshold;
            _autoScrollDelta = (1.2 + (speedFactor * 2.8));
            if (!_autoScrollTimer!.IsEnabled) _autoScrollTimer.Start();
        }
        else
        {
            StopAutoScroll();
        }

        // Dotted insertion hint line
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
            var brush = (System.Windows.Media.Brush)Application.Current.Resources["AccentColor"];
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
            _settings.Apps.Remove(item.App);
            SaveConfig();
            BuildGroupChips();
            RefreshList(TxtSearch.Text);
            BuildTrayContextMenu();
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
            _editingApp.AppTitle = TxtTitle.Text.Trim();
            _editingApp.AppLink = TxtPath.Text.Trim();
            _editingApp.AppParams = TxtArgs.Text.Trim();
            _editingApp.AppGroup = TxtGroup.Text.Trim();
            _editingApp.CustomIconPath = TxtCustomIcon.Text.Trim();
            _editingApp = null;
        }
        else
        {
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

    private void ChkAutoStart_Click(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = ChkAutoStart.IsChecked ?? false;
        SaveConfig();

        const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
        if (key != null)
        {
            if (_settings.StartWithWindows)
                key.SetValue("MintLauncher", $"\"{Environment.ProcessPath}\" --minimized");
            else
                key.DeleteValue("MintLauncher", false);
        }
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
