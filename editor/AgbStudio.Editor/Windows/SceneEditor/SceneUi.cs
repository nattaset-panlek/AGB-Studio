using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>Popover surface with a Popover Header (T-O01..T-O06): light dismiss, Esc and the close button.</summary>
public abstract class ScenePopover : Border, IOverlayContent
{
    public event Action<object?>? Closed;
    protected void Close(object? result) => Closed?.Invoke(result);

    /// <summary>Puts the keyboard in a search box once the popover is shown (type to filter at once).</summary>
    protected static void FocusOnShow(TextBox box) =>
        box.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => box.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);

    protected void Build(string title, Control content, double width)
    {
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = width;
        var header = new PopoverHeader { Title = title, Name = "PopoverHeader" };
        header.CloseRequested += (_, _) => Close(null);
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.AddRange([header, content]);
        Child = dock;
    }
}

/// <summary>Bitmaps of game pixels (sprite frames, tiles, the GBA screen) for the editor panels.</summary>
public static class PixelArt
{
    /// <summary>A bitmap from 0xAARRGGBB pixels.</summary>
    public static WriteableBitmap Bitmap(uint[] argb, int width, int height)
    {
        var bmp = new WriteableBitmap(new PixelSize(Math.Max(1, width), Math.Max(1, height)), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var fb = bmp.Lock();
        var row = new int[width];
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(argb, y * width * 4, row, 0, width * 4);
            System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, width);
        }
        return bmp;
    }

    /// <summary>One sprite frame with its palette (index 0 transparent), cropped to the authored frame size.</summary>
    public static uint[] SpriteFrame(ConvertedSprite s, int frame, out int width, out int height)
    {
        width = s.SourceWidth;
        height = s.SourceHeight;
        var px = new uint[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = GbaScreenRenderer.SpritePixel(s, frame, x, y);
                px[y * width + x] = i == 0 ? 0u : ScreenFrame.ToArgb(s.Palette[i]);
            }
        return px;
    }

    public static WriteableBitmap? SpriteBitmap(SceneAssets? assets, string sprite, int frame = 0)
    {
        if (assets?.Sprite(sprite) is not { } s || s.FrameCount == 0) return null;
        var px = SpriteFrame(s, Math.Clamp(frame, 0, s.FrameCount - 1), out var w, out var h);
        return Bitmap(px, w, h);
    }

    /// <summary>A PNG asset as it is on disk (tileset / image thumbnails).</summary>
    public static WriteableBitmap? Png(string path)
    {
        try
        {
            var img = AgbStudio.Core.Png.Read(path);
            return Bitmap(img.Pixels, img.Width, img.Height);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>An Image control that draws game pixels without smoothing at an integer scale that fits the box.</summary>
    public static Image View(Bitmap? bmp, double maxW, double maxH, int? scale = null)
    {
        var img = new Image { Source = bmp, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
        if (bmp != null)
        {
            var k = scale ?? Math.Max(1, (int)Math.Floor(Math.Min(maxW / bmp.PixelSize.Width, maxH / bmp.PixelSize.Height)));
            if (k * bmp.PixelSize.Width > maxW || k * bmp.PixelSize.Height > maxH)
            {
                var f = Math.Min(maxW / bmp.PixelSize.Width, maxH / bmp.PixelSize.Height);
                img.Width = bmp.PixelSize.Width * f;
                img.Height = bmp.PixelSize.Height * f;
            }
            else
            {
                img.Width = bmp.PixelSize.Width * k;
                img.Height = bmp.PixelSize.Height * k;
            }
        }
        return img;
    }

    /// <summary>Game pixels scaled to fit a box (any factor), without smoothing (small thumbnails).</summary>
    public static Image Fit(Bitmap? bmp, double maxW, double maxH)
    {
        var img = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = maxW, Height = maxH, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
        return img;
    }

    /// <summary>A colour from BGR555 (palette swatches).</summary>
    public static Color Color(ushort bgr) => Avalonia.Media.Color.FromUInt32(ScreenFrame.ToArgb(bgr));
}

