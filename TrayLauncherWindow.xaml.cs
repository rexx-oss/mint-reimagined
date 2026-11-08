using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace Mint;

public partial class TrayLauncherWindow : Window
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private readonly MainWindow _mainWindow;

    public TrayLauncherWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
    }

    public void Open(AppSettings settings)
    {
        BuildColumns(settings);

        // 1. Ensure the Win32 window handle exists so DPI and metrics are active
        var handle = new WindowInteropHelper(this).EnsureHandle();

        // 2. Pre-measure the window layout so we have true dimensions BEFORE positioning
        this.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double targetWidth = this.DesiredSize.Width > 0 ? this.DesiredSize.Width : 420;
        double targetHeight = this.DesiredSize.Height > 0 ? this.DesiredSize.Height : 300;

        // 3. Find the exact screen where the mouse cursor / tray icon is located
        GetCursorPos(out var pt);
        var screen = WinForms.Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
        var wa = screen.WorkingArea;

        // 4. Calculate accurate DPI scaling (e.g. 150%)
        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.5;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.5;

        double waRightDIP = wa.Right / dpiX;
        double waBottomDIP = wa.Bottom / dpiY;

        // 5. Position cleanly above the taskbar on the very first click
        this.Left = Math.Max(10, waRightDIP - targetWidth - 10);
        this.Top = Math.Max(10, waBottomDIP - targetHeight - 10);

        // 6. Display and force foreground activation
        this.Show();
        SetForegroundWindow(handle);
        this.Activate();
    }

    private void BuildColumns(AppSettings settings)
    {
        PanelColumns.Children.Clear();
        int totalCols = Math.Max(1, settings.TrayColumns);

        var colBuckets = new List<List<AppItem>>();
        for (int i = 0; i < totalCols; i++) colBuckets.Add(new List<AppItem>());

        var unassigned = new List<AppItem>();
        foreach (var app in settings.Apps)
        {
            if (app.Column >= 1 && app.Column <= totalCols)
                colBuckets[app.Column - 1].Add(app);
            else
                unassigned.Add(app);
        }

        int currentBucket = 0;
        foreach (var app in unassigned)
        {
            colBuckets[currentBucket].Add(app);
            currentBucket = (currentBucket + 1) % totalCols;
        }

        for (int colIdx = 0; colIdx < totalCols; colIdx++)
        {
            if (colBuckets[colIdx].Count == 0) continue;

            if (PanelColumns.Children.Count > 0)
            {
                var divider = new Border
                {
                    Width = 1,
                    Background = (Brush)Application.Current.Resources["BorderColor"],
                    Margin = new Thickness(6, 2, 6, 2)
                };
                PanelColumns.Children.Add(divider);
            }

            var colStack = new StackPanel
            {
                Width = 190,
                Margin = new Thickness(2, 0, 2, 0)
            };

            foreach (var app in colBuckets[colIdx])
            {
                colStack.Children.Add(CreateAppCard(app));
            }

            PanelColumns.Children.Add(colStack);
        }
    }

    private UIElement CreateAppCard(AppItem app)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["InputBg"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 2, 0, 2),
            Cursor = Cursors.Hand,
            ToolTip = app.AppLink
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var img = new Image
        {
            Width = 18,
            Height = 18,
            VerticalAlignment = VerticalAlignment.Center
        };
        IconHelper.GetIconAsync(app.AppLink, app.CustomIconPath, app.AppTitle).ContinueWith(t =>
        {
            if (t.Result != null) Dispatcher.Invoke(() => img.Source = t.Result);
        });
        Grid.SetColumn(img, 0);

        var txt = new TextBlock
        {
            Text = app.AppTitle,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextPrimary"],
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(6, 0, 2, 0)
        };
        Grid.SetColumn(txt, 1);

        grid.Children.Add(img);
        grid.Children.Add(txt);
        card.Child = grid;

        card.MouseEnter += (s, e) => card.Background = (Brush)Application.Current.Resources["CardHoverBg"];
        card.MouseLeave += (s, e) => card.Background = (Brush)Application.Current.Resources["InputBg"];
        card.MouseLeftButtonUp += (s, e) =>
        {
            Hide();
            LaunchApp(app);
        };

        return card;
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
            else if (Directory.Exists(app.AppLink))
                psi.WorkingDirectory = app.AppLink;

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            ModernDialog.Show(this, $"Could not launch item:\n{ex.Message}", "Launch Error");
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _mainWindow.ShowAndRestore();
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _mainWindow.ExitApplication();
    }
}
