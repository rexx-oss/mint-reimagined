using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace Mint
{
    public class AppDisplayItem
    {
        public AppItem App { get; set; } = new();
        public ImageSource? Icon { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps.json");
        private AppSettings _settings = new();
        private readonly ObservableCollection<AppDisplayItem> _displayList = new();
        private readonly WinForms.NotifyIcon _notifyIcon;
        private Point _dragStartPoint;
        private AppItem? _selectedAppForEdit;

        public MainWindow()
        {
            InitializeComponent();
            LstApps.ItemsSource = _displayList;

            _notifyIcon = new WinForms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "Mint Launcher",
                Visible = true
            };
            _notifyIcon.DoubleClick += (s, e) => ShowAndRestore();

            LoadConfig();
            ThemeManager.ApplyTheme(_settings.Theme);
            UpdateThemeRadioButtons();

            RefreshList();
            BuildTrayMenu();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { _settings = new AppSettings(); }

            ChkAutoStart.IsChecked = _settings.StartWithWindows;
            RefreshGroupDropdown();
        }

        private void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        private void UpdateThemeRadioButtons()
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
            BuildTrayMenu();
        }

        private async void RefreshList(string filter = "")
        {
            _displayList.Clear();
            var query = _settings.Apps.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filter))
                query = query.Where(a => a.AppTitle.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                         a.AppGroup.Contains(filter, StringComparison.OrdinalIgnoreCase));

            foreach (var app in query)
            {
                var icon = await IconHelper.GetIconAsync(app.AppLink, app.CustomIconPath, app.AppTitle);
                _displayList.Add(new AppDisplayItem { App = app, Icon = icon });
            }

            TxtListHeader.Text = $"Apps ({_settings.Apps.Count})";
        }

        private void RefreshGroupDropdown()
        {
            var groups = _settings.Apps.Select(a => a.AppGroup)
                                       .Where(g => !string.IsNullOrWhiteSpace(g))
                                       .Distinct()
                                       .OrderBy(g => g)
                                       .ToList();
            CmbGroups.ItemsSource = groups;
        }

        public void BuildTrayMenu()
        {
            var menu = new WinForms.ContextMenuStrip();

            // Set tray context menu colors to match theme
            bool isDark = ThemeManager.IsDarkThemeActive;
            menu.BackColor = isDark ? System.Drawing.Color.FromArgb(24, 24, 27) : System.Drawing.Color.FromArgb(255, 255, 255);
            menu.ForeColor = isDark ? System.Drawing.Color.FromArgb(244, 244, 245) : System.Drawing.Color.FromArgb(24, 24, 27);

            var groups = _settings.Apps
                .Where(a => !string.IsNullOrEmpty(a.AppGroup))
                .GroupBy(a => a.AppGroup)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var groupItem = new WinForms.ToolStripMenuItem(group.Key);
                groupItem.BackColor = menu.BackColor;
                groupItem.ForeColor = menu.ForeColor;

                foreach (var app in group)
                {
                    var item = new WinForms.ToolStripMenuItem(app.AppTitle);
                    item.BackColor = menu.BackColor;
                    item.ForeColor = menu.ForeColor;
                    item.Click += (s, e) => LaunchApp(app);
                    groupItem.DropDownItems.Add(item);
                }
                menu.Items.Add(groupItem);
            }

            if (groups.Any()) menu.Items.Add(new WinForms.ToolStripSeparator());

            foreach (var app in _settings.Apps.Where(a => string.IsNullOrEmpty(a.AppGroup)))
            {
                var item = new WinForms.ToolStripMenuItem(app.AppTitle);
                item.BackColor = menu.BackColor;
                item.ForeColor = menu.ForeColor;
                item.Click += (s, e) => LaunchApp(app);
                menu.Items.Add(item);
            }

            menu.Items.Add(new WinForms.ToolStripSeparator());

            var openItem = new WinForms.ToolStripMenuItem("Settings");
            openItem.BackColor = menu.BackColor;
            openItem.ForeColor = menu.ForeColor;
            openItem.Click += (s, e) => ShowAndRestore();
            menu.Items.Add(openItem);

            var exitItem = new WinForms.ToolStripMenuItem("Exit");
            exitItem.BackColor = menu.BackColor;
            exitItem.ForeColor = menu.ForeColor;
            exitItem.Click += (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Application.Current.Shutdown();
            };
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void LaunchApp(AppItem app)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = app.AppLink,
                    Arguments = app.AppParams,
                    UseShellExecute = true
                };

                if (File.Exists(app.AppLink))
                    psi.WorkingDirectory = Path.GetDirectoryName(app.AppLink);

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not launch app:\n{ex.Message}", "Mint Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAndRestore()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Select Target", Filter = "All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) PopulateFromPath(dlg.FileName);
        }

        private void BtnBrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Select Icon", Filter = "Icons & Images (*.ico;*.png;*.exe)|*.ico;*.png;*.exe|All (*.*)|*.*" };
            if (dlg.ShowDialog() == true) TxtIcon.Text = dlg.FileName;
        }

        private void PopulateFromPath(string path)
        {
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var (target, args) = ShortcutHelper.ResolveShortcut(path);
                TxtPath.Text = target;
                TxtArgs.Text = args;
                TxtTitle.Text = Path.GetFileNameWithoutExtension(path);
            }
            else
            {
                TxtPath.Text = path;
                TxtTitle.Text = Path.GetFileNameWithoutExtension(path);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0) PopulateFromPath(files[0]);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitle.Text) || string.IsNullOrWhiteSpace(TxtPath.Text))
            {
                MessageBox.Show("Please enter both a Title and Target path.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedAppForEdit != null)
            {
                _selectedAppForEdit.AppTitle = TxtTitle.Text.Trim();
                _selectedAppForEdit.AppLink = TxtPath.Text.Trim();
                _selectedAppForEdit.AppParams = TxtArgs.Text.Trim();
                _selectedAppForEdit.AppGroup = CmbGroups.Text.Trim();
                _selectedAppForEdit.CustomIconPath = TxtIcon.Text.Trim();
                _selectedAppForEdit = null;
            }
            else
            {
                _settings.Apps.Add(new AppItem
                {
                    AppTitle = TxtTitle.Text.Trim(),
                    AppLink = TxtPath.Text.Trim(),
                    AppParams = TxtArgs.Text.Trim(),
                    AppGroup = CmbGroups.Text.Trim(),
                    CustomIconPath = TxtIcon.Text.Trim()
                });
            }

            SaveConfig();
            RefreshList();
            RefreshGroupDropdown();
            BuildTrayMenu();
            BtnClear_Click(this, new RoutedEventArgs());
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedAppForEdit = null;
            TxtTitle.Clear();
            TxtPath.Clear();
            TxtArgs.Clear();
            TxtIcon.Clear();
            CmbGroups.Text = string.Empty;
        }

        private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstApps.SelectedItem is AppDisplayItem item)
            {
                _selectedAppForEdit = item.App;
                TxtTitle.Text = item.App.AppTitle;
                TxtPath.Text = item.App.AppLink;
                TxtArgs.Text = item.App.AppParams;
                CmbGroups.Text = item.App.AppGroup;
                TxtIcon.Text = item.App.CustomIconPath;
            }
        }

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppDisplayItem item)
            {
                int idx = _settings.Apps.IndexOf(item.App);
                if (idx > 0)
                {
                    _settings.Apps.RemoveAt(idx);
                    _settings.Apps.Insert(idx - 1, item.App);
                    SaveConfig();
                    RefreshList();
                    BuildTrayMenu();
                }
            }
        }

        private void BtnDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppDisplayItem item)
            {
                int idx = _settings.Apps.IndexOf(item.App);
                if (idx >= 0 && idx < _settings.Apps.Count - 1)
                {
                    _settings.Apps.RemoveAt(idx);
                    _settings.Apps.Insert(idx + 1, item.App);
                    SaveConfig();
                    RefreshList();
                    BuildTrayMenu();
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AppDisplayItem item)
            {
                _settings.Apps.Remove(item.App);
                SaveConfig();
                RefreshList();
                RefreshGroupDropdown();
                BuildTrayMenu();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshList(TxtSearch.Text.Trim());

        private void ChkAutoStart_Click(object sender, RoutedEventArgs e)
        {
            _settings.StartWithWindows = ChkAutoStart.IsChecked ?? false;
            SaveConfig();

            const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key != null)
            {
                if (_settings.StartWithWindows)
                    key.SetValue("MintLauncher", $"\"{Environment.ProcessPath}\"");
                else
                    key.DeleteValue("MintLauncher", false);
            }
        }

        private void LstApps_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _dragStartPoint = e.GetPosition(null);

        private void LstApps_MouseMove(object sender, MouseEventArgs e)
        {
            var diff = _dragStartPoint - e.GetPosition(null);
            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                if (LstApps.SelectedItem is AppDisplayItem selected)
                    DragDrop.DoDragDrop(LstApps, selected, DragDropEffects.Move);
            }
        }

        private void LstApps_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(AppDisplayItem)) is AppDisplayItem droppedData)
            {
                int targetIdx = _displayList.IndexOf(droppedData);
                int oldIdx = _settings.Apps.IndexOf(droppedData.App);

                if (targetIdx >= 0 && oldIdx >= 0 && targetIdx != oldIdx)
                {
                    _settings.Apps.RemoveAt(oldIdx);
                    _settings.Apps.Insert(targetIdx, droppedData.App);
                    SaveConfig();
                    RefreshList();
                    BuildTrayMenu();
                }
            }
        }
    }
}