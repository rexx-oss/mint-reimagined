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
using System.Windows.Interop;
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
        private AppItem? _editingApp;
        private string _activeGroupFilter = "All";

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
            _notifyIcon.MouseClick += (s, e) => ToggleFlyout();

            LoadConfig();
            ThemeManager.ApplyTheme(_settings.Theme);
            SyncThemeRadios();
            BuildGroupChips();
            RefreshList();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            NativeHelper.EnableWindowRoundedCorners(handle);
        }

        private void ToggleFlyout()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                PositionAboveTaskbar();
                Show();
                Activate();
                TxtSearch.Focus();
                TxtSearch.SelectAll();
            }
        }

        private void PositionAboveTaskbar()
        {
            NativeHelper.GetCursorPos(out var pt);
            var screen = WinForms.Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
            var wa = screen.WorkingArea;

            // Align gracefully right above the taskbar in the lower right
            Left = wa.Right - Width - 12;
            Top = wa.Bottom - Height - 12;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Auto-hide when user clicks away
            Hide();
            ShowLauncherView();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath)) ?? new AppSettings();
            }
            catch { _settings = new AppSettings(); }

            ChkAutoStart.IsChecked = _settings.StartWithWindows;
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
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
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    Background = (group == _activeGroupFilter) 
                        ? (Brush)Application.Current.Resources["AccentColor"] 
                        : (Brush)Application.Current.Resources["CardBg"],
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
                var icon = await IconHelper.GetIconAsync(app.AppLink, app.CustomIconPath);
                _displayList.Add(new AppDisplayItem { App = app, Icon = icon });
            }

            TxtFooterCount.Text = $"{_displayList.Count} of {_settings.Apps.Count} Apps";

            if (_displayList.Count > 0)
                LstApps.SelectedIndex = 0;
        }

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

                Process.Start(psi);
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not launch app:\n{ex.Message}", "Mint", MessageBoxButton.OK, MessageBoxImage.Error);
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
            else if (e.Key == Key.Escape)
            {
                Hide();
            }
        }

        private void LstApps_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LstApps.SelectedItem is AppDisplayItem item)
                LaunchApp(item.App);
            else if (e.Key == Key.Escape)
                Hide();
        }

        private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstApps.SelectedItem is AppDisplayItem item)
                LaunchApp(item.App);
        }

        private void MenuRunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (LstApps.SelectedItem is AppDisplayItem item)
                LaunchApp(item.App, true);
        }

        private void MenuOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (LstApps.SelectedItem is AppDisplayItem item && File.Exists(item.App.AppLink))
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
                ShowSettingsView();
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
            }
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e) => ShowSettingsView();
        private void BtnBackToLauncher_Click(object sender, RoutedEventArgs e) => ShowLauncherView();

        private void ShowSettingsView()
        {
            ViewLauncher.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Visible;
        }

        private void ShowLauncherView()
        {
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewLauncher.Visibility = Visibility.Visible;
            TxtSearch.Focus();
        }

        private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Select Target", Filter = "Applications & Shortcuts (*.exe;*.lnk)|*.exe;*.lnk|All Files (*.*)|*.*" };
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
            }
        }

        private void BtnBrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Select Icon", Filter = "Icons & Images (*.ico;*.png;*.exe)|*.ico;*.png;*.exe|All (*.*)|*.*" };
            if (dlg.ShowDialog() == true) TxtCustomIcon.Text = dlg.FileName;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitle.Text) || string.IsNullOrWhiteSpace(TxtPath.Text))
            {
                MessageBox.Show("Please enter Title and Target.", "Mint", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            ShowLauncherView();
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
                    key.SetValue("MintLauncher", $"\"{Environment.ProcessPath}\"");
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
                }
            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }
    }
}
