using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mint;

public static class IconHelper
{
    private static readonly ConcurrentDictionary<string, ImageSource> WpfCache = new();
    private static readonly ConcurrentDictionary<string, System.Drawing.Image> GdiCache = new();
    private static readonly string CacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon_cache");

    static IconHelper()
    {
        try
        {
            if (!Directory.Exists(CacheDir)) Directory.CreateDirectory(CacheDir);
        }
        catch { }
    }

    public static string GetSafeDiskFileName(string appTitle)
    {
        string safe = string.Join("_", appTitle.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(CacheDir, safe + ".png");
    }

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

    public static void InvalidateCache(string appTitle)
    {
        if (!string.IsNullOrWhiteSpace(appTitle))
        {
            WpfCache.TryRemove(appTitle, out _);
            GdiCache.TryRemove(appTitle, out _);
        }
    }

    public static async Task<ImageSource?> GetIconAsync(string filePath, string customIconPath, string appTitle)
    {
        string sourcePath = !string.IsNullOrWhiteSpace(customIconPath) && File.Exists(customIconPath)
            ? customIconPath
            : filePath;

        if (string.IsNullOrWhiteSpace(sourcePath) && string.IsNullOrWhiteSpace(appTitle)) return null;

        // Key by AppTitle so multiple shortcuts to the same .exe maintain separate icons
        string cacheKey = !string.IsNullOrWhiteSpace(appTitle) ? appTitle : sourcePath;

        if (WpfCache.TryGetValue(cacheKey, out var cached))
            return cached;

        return await Task.Run(() =>
        {
            try
            {
                string diskPath = GetSafeDiskFileName(appTitle);

                // 1. Check disk cache first (loads manual .png placed in icon_cache/)
                if (File.Exists(diskPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(diskPath);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    WpfCache[cacheKey] = bmp;
                    return (ImageSource)bmp;
                }

                // 2. Extract from source file and save to disk cache
                string target = ResolveTargetPath(sourcePath);
                if (!File.Exists(target) && !Directory.Exists(target)) return null;

                string ext = Path.GetExtension(target).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(target);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 32;
                    bmp.EndInit();
                    bmp.Freeze();
                    WpfCache[cacheKey] = bmp;
                    return (ImageSource)bmp;
                }

                using var icon = Icon.ExtractAssociatedIcon(target);
                if (icon == null) return null;

                using (var bitmap = icon.ToBitmap())
                {
                    try { bitmap.Save(diskPath, ImageFormat.Png); } catch { }
                }

                var bs = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));
                bs.Freeze();

                WpfCache[cacheKey] = bs;
                return bs;
            }
            catch { return null; }
        });
    }

    public static System.Drawing.Image? GetGdiIcon(string filePath, string customIconPath, string appTitle)
    {
        string sourcePath = !string.IsNullOrWhiteSpace(customIconPath) && File.Exists(customIconPath)
            ? customIconPath
            : filePath;

        if (string.IsNullOrWhiteSpace(sourcePath) && string.IsNullOrWhiteSpace(appTitle)) return null;

        string cacheKey = !string.IsNullOrWhiteSpace(appTitle) ? appTitle : sourcePath;

        if (GdiCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            string diskPath = GetSafeDiskFileName(appTitle);
            if (File.Exists(diskPath))
            {
                using var stream = new FileStream(diskPath, FileMode.Open, FileAccess.Read);
                using var img = System.Drawing.Image.FromStream(stream);
                var scaled = new Bitmap(img, new System.Drawing.Size(16, 16));
                GdiCache[cacheKey] = scaled;
                return scaled;
            }

            string target = ResolveTargetPath(sourcePath);
            if (!File.Exists(target) && !Directory.Exists(target)) return null;

            using var icon = Icon.ExtractAssociatedIcon(target);
            if (icon != null)
            {
                using var bmp = icon.ToBitmap();
                var scaled = new Bitmap(bmp, new System.Drawing.Size(16, 16));
                GdiCache[cacheKey] = scaled;
                return scaled;
            }
        }
        catch { }

        return null;
    }

    public static void CleanupOrphanedIcons(IEnumerable<string> activeTitles)
    {
        try
        {
            if (!Directory.Exists(CacheDir)) return;
            var validFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var title in activeTitles)
            {
                validFiles.Add(Path.GetFileName(GetSafeDiskFileName(title)));
            }

            foreach (var file in Directory.GetFiles(CacheDir, "*.png"))
            {
                if (!validFiles.Contains(Path.GetFileName(file)))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }
}
