using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mint
{
    public static class IconHelper
    {
        private static readonly ConcurrentDictionary<string, ImageSource> WpfCache = new();
        private static readonly ConcurrentDictionary<string, System.Drawing.Image> GdiCache = new();

        private static string ResolveTargetPath(string sourcePath)
        {
            string target = sourcePath;
            if (!Path.IsPathRooted(target))
            {
                string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string fullPath = Path.Combine(systemDir, target);
                if (File.Exists(fullPath)) target = fullPath;
            }
            return target;
        }

        public static async Task<ImageSource?> GetIconAsync(string filePath, string customIconPath)
        {
            string sourcePath = !string.IsNullOrWhiteSpace(customIconPath) && File.Exists(customIconPath)
                ? customIconPath
                : filePath;

            if (string.IsNullOrWhiteSpace(sourcePath)) return null;

            if (WpfCache.TryGetValue(sourcePath, out var cached))
                return cached;

            return await Task.Run(() =>
            {
                try
                {
                    string target = ResolveTargetPath(sourcePath);
                    if (!File.Exists(target) && !Directory.Exists(target)) return null;

                    string ext = Path.GetExtension(target).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(target);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 32;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        WpfCache[sourcePath] = bitmap;
                        return (ImageSource)bitmap;
                    }

                    using var icon = Icon.ExtractAssociatedIcon(target);
                    if (icon == null) return null;

                    var bs = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bs.Freeze();

                    WpfCache[sourcePath] = bs;
                    return bs;
                }
                catch { return null; }
            });
        }

        public static System.Drawing.Image? GetGdiIcon(string filePath, string customIconPath)
        {
            string sourcePath = !string.IsNullOrWhiteSpace(customIconPath) && File.Exists(customIconPath)
                ? customIconPath
                : filePath;

            if (string.IsNullOrWhiteSpace(sourcePath)) return null;

            if (GdiCache.TryGetValue(sourcePath, out var cached))
                return cached;

            try
            {
                string target = ResolveTargetPath(sourcePath);
                if (!File.Exists(target) && !Directory.Exists(target)) return null;

                string ext = Path.GetExtension(target).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                {
                    using var img = System.Drawing.Image.FromFile(target);
                    var scaled = new Bitmap(img, new System.Drawing.Size(16, 16));
                    GdiCache[sourcePath] = scaled;
                    return scaled;
                }

                using var icon = Icon.ExtractAssociatedIcon(target);
                if (icon != null)
                {
                    using var bmp = icon.ToBitmap();
                    var scaled = new Bitmap(bmp, new System.Drawing.Size(16, 16));
                    GdiCache[sourcePath] = scaled;
                    return scaled;
                }
            }
            catch { }

            return null;
        }
    }
}
