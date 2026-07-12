using System.Collections.Concurrent;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VdLabel;

interface IWindowIconCache
{
    ImageSource? Get(string processPath);
}

sealed class WindowIconCache : IWindowIconCache
{
    private readonly ConcurrentDictionary<string, Lazy<ImageSource?>> icons = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? Get(string processPath)
        => this.icons.GetOrAdd(processPath, static path => new(() => Extract(path))).Value;

    private static ImageSource? Extract(string processPath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(processPath);
            if (icon is null)
            {
                return null;
            }
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
