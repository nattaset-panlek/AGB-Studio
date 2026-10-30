using System.Runtime.InteropServices;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Graphics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>Game pixels (ARGB, as the project files hold them): sprite frames, the GBA text box. Data, not UI colours.</summary>
public sealed record PixelData(int Width, int Height, uint[] Argb)
{
    public WriteableBitmap ToBitmap()
    {
        var bmp = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        var buf = new int[Width * Height];
        for (var i = 0; i < buf.Length; i++)
        {
            var c = Argb[i];
            var a = c >> 24;
            // GBA art is either opaque or fully transparent; premultiply the rest
            buf[i] = a == 0 ? 0 : a == 255 ? unchecked((int)c) : unchecked((int)Premul(c, a));
        }
        for (var y = 0; y < Height; y++) Marshal.Copy(buf, y * Width, fb.Address + y * fb.RowBytes, Width);
        return bmp;
    }

    static uint Premul(uint c, uint a)
    {
        uint r = (c >> 16 & 0xFF) * a / 255, g = (c >> 8 & 0xFF) * a / 255, b = (c & 0xFF) * a / 255;
        return a << 24 | r << 16 | g << 8 | b;
    }
}

/// <summary>Draws <see cref="PixelData"/> scaled by a whole number, nearest neighbour, centred in its bounds.</summary>
public sealed class PixelImage : Control
{
    public static readonly StyledProperty<PixelData?> PixelsProperty = AvaloniaProperty.Register<PixelImage, PixelData?>(nameof(Pixels));
    public static readonly StyledProperty<int> MaxScaleProperty = AvaloniaProperty.Register<PixelImage, int>(nameof(MaxScale), 8);

    WriteableBitmap? _bmp;

    static PixelImage() => AffectsRender<PixelImage>(PixelsProperty, MaxScaleProperty);

    public PixelImage() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    public PixelData? Pixels { get => GetValue(PixelsProperty); set => SetValue(PixelsProperty, value); }
    public int MaxScale { get => GetValue(MaxScaleProperty); set => SetValue(MaxScaleProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PixelsProperty) { _bmp = Pixels?.ToBitmap(); }
    }

    public override void Render(DrawingContext context)
    {
        if (_bmp is null || Pixels is not { } p) return;
        var k = Math.Max(1, Math.Min(MaxScale, (int)Math.Floor(Math.Min(Bounds.Width / p.Width, Bounds.Height / p.Height))));
        if (Bounds.Width < p.Width || Bounds.Height < p.Height) k = 1;
        var w = p.Width * k;
        var h = p.Height * k;
        var x = Math.Round((Bounds.Width - w) / 2);
        var y = Math.Round((Bounds.Height - h) / 2);
        context.DrawImage(_bmp, new Rect(0, 0, p.Width, p.Height), new Rect(x, y, w, h));
    }
}

/// <summary>The project's sprites (assets/sprites/*.png) as first-frame thumbnails for tables, portraits and pickers.</summary>
public static class SpriteLibrary
{
    static readonly Dictionary<string, (DateTime Time, PixelData Pixels)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Sprite names of the project (file name without extension), sorted.</summary>
    public static IReadOnlyList<string> Names(GameProject? p) =>
        p is null ? [] : p.SpriteFiles().Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).Select(GameProject.AssetName).Distinct().Order(StringComparer.Ordinal).ToList();

    public static string? PathOf(GameProject? p, string name) =>
        p is null || name.Length == 0 ? null : p.SpriteFiles().FirstOrDefault(f => string.Equals(GameProject.AssetName(f), name, StringComparison.OrdinalIgnoreCase) && f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

    /// <summary>The first animation frame of a sprite (frame size from its .meta.json), or null if there is no such sprite.</summary>
    public static PixelData? Frame(GameProject? p, string? name)
    {
        if (name is null || PathOf(p, name) is not { } path) return null;
        var time = File.GetLastWriteTimeUtc(path);
        if (Cache.TryGetValue(path, out var c) && c.Time == time) return c.Pixels;
        try
        {
            var img = Png.Read(path);
            var meta = p!.LoadMeta<SpriteMeta>(path);
            var w = Math.Clamp(meta.FrameWidth, 1, img.Width);
            var h = Math.Clamp(meta.FrameHeight, 1, img.Height);
            var px = new uint[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++) px[y * w + x] = img.Pixels[y * img.Width + x];
            var data = new PixelData(w, h, px);
            Cache[path] = (time, data);
            return data;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException) { return null; }
    }
}

/// <summary>
/// A GBA screen preview (240 x 160, scaled): the scene backdrop and, over it, the dialogue box exactly as the runtime
/// draws it (<see cref="GbaText.RenderBox"/>). Pixels are game data; the backdrop is the Figma scene art.
/// </summary>
public sealed class GbaScreen : Control
{
    public static readonly StyledProperty<string?> BackdropProperty = AvaloniaProperty.Register<GbaScreen, string?>(nameof(Backdrop), "art/scene/day");
    public static readonly StyledProperty<PixelData?> BoxProperty = AvaloniaProperty.Register<GbaScreen, PixelData?>(nameof(Box));

    WriteableBitmap? _box;

    static GbaScreen() => AffectsRender<GbaScreen>(BackdropProperty, BoxProperty);

    public GbaScreen() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    public string? Backdrop { get => GetValue(BackdropProperty); set => SetValue(BackdropProperty, value); }
    /// <summary>The 224 x 40 dialogue box drawn at (8, 112) of the screen, or null when no text is shown.</summary>
    public PixelData? Box { get => GetValue(BoxProperty); set => SetValue(BoxProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoxProperty) _box = Box?.ToBitmap();
    }

    protected override Size MeasureOverride(Size availableSize) => new(double.IsNaN(Width) ? 240 : Width, double.IsNaN(Height) ? 160 : Height);

    public override void Render(DrawingContext context)
    {
        var k = Bounds.Width / GbaText.ScreenWidth;
        using (context.PushTransform(Matrix.CreateScale(k, Bounds.Height / GbaText.ScreenHeight)))
        {
            if (!string.IsNullOrEmpty(Backdrop)) IconCatalog.Art(Backdrop).Render(context, new Size(GbaText.ScreenWidth, GbaText.ScreenHeight));
            if (_box != null && Box is { } b) context.DrawImage(_box, new Rect(0, 0, b.Width, b.Height), new Rect(GbaText.BoxX, GbaText.BoxY, b.Width, b.Height));
        }
    }
}
