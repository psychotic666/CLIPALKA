using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clipalka.App.Models;

namespace Clipalka.App.Services;

public static class RecentClipService
{
    public static IReadOnlyList<RecentClipItem> Load(string outputDirectory, int count = 4)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(outputDirectory, "*.mp4", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(count)
                .Select(file => new RecentClipItem(
                    file.FullName,
                    CleanTitle(file.Name),
                    FormatSavedAt(file.LastWriteTime),
                    TryReadDuration(file.FullName),
                    TryReadThumbnail(file.FullName)))
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string CleanTitle(string fileName)
    {
        var title = Path.GetFileNameWithoutExtension(fileName);
        var dateSeparator = title.LastIndexOf(" - 20", StringComparison.Ordinal);
        return dateSeparator > 0 ? title[..dateSeparator] : title;
    }

    private static string FormatSavedAt(DateTime value) =>
        value.Date == DateTime.Today
            ? $"Сегодня, {value:HH:mm}"
            : value.ToString("dd.MM.yyyy, HH:mm");

    private static ImageSource TryReadThumbnail(string filePath)
    {
        try
        {
            var interfaceId = typeof(IShellItemImageFactory).GUID;
            var result = SHCreateItemFromParsingName(filePath, nint.Zero, ref interfaceId, out var factory);
            if (result != 0 || factory is null)
            {
                return CreateFallbackThumbnail();
            }

            try
            {
                var size = new NativeSize(360, 200);
                result = factory.GetImage(size, ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk, out var bitmapHandle);
                if (result != 0 || bitmapHandle == nint.Zero)
                {
                    return CreateFallbackThumbnail();
                }

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmapHandle,
                        nint.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(size.Width, size.Height));
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(bitmapHandle);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(factory);
            }
        }
        catch (Exception)
        {
            return CreateFallbackThumbnail();
        }
    }

    private static string TryReadDuration(string filePath)
    {
        try
        {
            return ReadDuration(filePath);
        }
        catch (Exception)
        {
            return "—:—";
        }
    }

    private static string ReadDuration(string filePath)
    {
        var propertyStoreId = typeof(IPropertyStore).GUID;
        var result = SHGetPropertyStoreFromParsingName(filePath, nint.Zero, 0, ref propertyStoreId, out var store);
        if (result != 0 || store is null)
        {
            return "—:—";
        }

        try
        {
            var durationKey = new PropertyKey(
                new Guid("64440490-4C8B-11D1-8B70-080036B11A03"),
                3);
            store.GetValue(ref durationKey, out var value);
            try
            {
                if (value.ValueType != 21 || value.LongValue <= 0)
                {
                    return "—:—";
                }

                var duration = TimeSpan.FromTicks(value.LongValue);
                return duration.TotalHours >= 1
                    ? duration.ToString(@"h\:mm\:ss")
                    : duration.ToString(@"m\:ss");
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(store);
        }
    }

    private static ImageSource CreateFallbackThumbnail()
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/capture-preview.png", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize(int width, int height)
    {
        public int Width = width;
        public int Height = height;
    }

    [Flags]
    private enum ShellImageFlags
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, ShellImageFlags flags, out nint bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropertyVariant
    {
        [FieldOffset(0)] public ushort ValueType;
        [FieldOffset(8)] public long LongValue;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        uint GetCount();
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropertyVariant value);
        void SetValue(ref PropertyKey key, ref PropertyVariant value);
        void Commit();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        nint bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? imageFactory);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(
        string path,
        nint bindContext,
        uint flags,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropertyVariant value);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint objectHandle);
}
