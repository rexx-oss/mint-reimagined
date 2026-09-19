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
        private static readonly ConcurrentDictionary<string, ImageSource> MemoryCache = new();

        public static async Task<ImageSource?> GetIconAsync(string filePath, string customIconPath, string appTitle)
        {
            string sourcePath = !string.IsNullOrWhiteSpace(customIconPath) && File.Exists(customIconPath)
                ? customIconPath
                : filePath;

            if (string.IsNullOrWhiteSpace(sourcePath)) return null;

            if (MemoryCache.TryGetValue(sourcePath, out var cached))
                return cached;

            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                        return null;

                    string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(sourcePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 32;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        MemoryCache[sourcePath] = bitmap;
                        return (ImageSource)bitmap;
                    }

                    using var icon = Icon.ExtractAssociatedIcon(sourcePath);
                    if (icon == null) return null;

                    var bs = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bs.Freeze();

                    MemoryCache[sourcePath] = bs;
                    return bs;
                }
                catch
                {
                    return null;
                }
            });
        }
    }
}