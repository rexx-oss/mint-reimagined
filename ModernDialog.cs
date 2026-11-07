using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Mint;

public class ModernDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private ModernDialog(Window? owner, string title, string message, bool isConfirm, string confirmText, string cancelText, bool isDanger)
    {
        this.Owner = owner;
        this.Title = title;
        this.WindowStyle = WindowStyle.None;
        this.AllowsTransparency = true;
        this.Background = Brushes.Transparent;
        this.SizeToContent = SizeToContent.WidthAndHeight;
        this.ShowInTaskbar = false;
        this.Topmost = true;
        this.SnapsToDevicePixels = true;
        this.UseLayoutRounding = true;
        this.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif");

        if (owner != null && owner.IsVisible)
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        else
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // Allow dragging the dialog card
        this.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        // Outer Container Card with Shadow
        var outerBorder = new Border
        {
            Width = 390,
            Margin = new Thickness(16),
            Padding = new Thickness(18, 16, 18, 16),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["CardBg"],
            BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 6,
                Opacity = 0.35,
                Direction = 270
            }
        };

        var mainStack = new StackPanel();

        // Title
        var txtTitle = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["TextPrimary"],
            Margin = new Thickness(0, 0, 0, 8)
        };
        mainStack.Children.Add(txtTitle);

        // Message
        var txtMessage = new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"],
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
            Margin = new Thickness(0, 0, 0, 18)
        };
        mainStack.Children.Add(txtMessage);

        // Buttons Bar
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        if (isConfirm)
        {
            // Cancel / No Button (Escape closes)
            var btnCancel = new Button
            {
                Content = cancelText,
                IsCancel = true,
                Background = (Brush)Application.Current.Resources["InputBg"],
                Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                BorderThickness = new Thickness(1),
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            buttonsPanel.Children.Add(btnCancel);
        }

        // Confirm / OK Button (Enter triggers)
        var btnConfirm = new Button
        {
            Content = confirmText,
            IsDefault = true,
            Background = isDanger 
                ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) // Soft modern danger red
                : (Brush)Application.Current.Resources["AccentColor"],
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Height = 32,
            Padding = new Thickness(16, 0, 16, 0),
            Cursor = Cursors.Hand
        };
        btnConfirm.Click += (s, e) => { this.DialogResult = true; this.Close(); };
        buttonsPanel.Children.Add(btnConfirm);

        mainStack.Children.Add(buttonsPanel);
        outerBorder.Child = mainStack;
        this.Content = outerBorder;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        int roundPreference = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(handle, 33, ref roundPreference, sizeof(int));
    }

    // Displays an informative alert dialog (OK button)
    public static void Show(Window? owner, string message, string title = "Mint")
    {
        var dlg = new ModernDialog(owner, title, message, false, "OK", "", false);
        dlg.ShowDialog();
    }

    // Displays a confirmation warning dialog (returns true if confirmed)
    public static bool Confirm(Window? owner, string message, string title = "Confirm", string confirmText = "Confirm", string cancelText = "Cancel", bool isDanger = false)
    {
        var dlg = new ModernDialog(owner, title, message, true, confirmText, cancelText, isDanger);
        return dlg.ShowDialog() == true;
    }
}
