using System.Globalization;
using System.Runtime.InteropServices;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Emu;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>
/// The emulator picture of the Game tab (P12.01): 240 x 160 at the Preferences scale (3x = 720 x 480), nearest
/// neighbour (pixel-perfect), centred on black, with the Hitboxes and Tile collision overlays (P12.05).
/// </summary>
public sealed class GameScreen : Control
{
    readonly PlayController _play;
    readonly WriteableBitmap _bitmap = new(new PixelSize(LibretroCore.Width, LibretroCore.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    readonly int[] _row = new int[LibretroCore.Width];
    MapFile? _map;
    TilesetMeta? _tiles;
    string _mapScene = "";

    public GameScreen(PlayController play)
    {
        _play = play;
        Name = "GameScreen";
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public int Scale => Math.Max(1, _play.Options.Scale);
    public Rect ImageRect
    {
        get
        {
            var w = LibretroCore.Width * Scale;
            var h = LibretroCore.Height * Scale;
            return new Rect(Math.Floor((Bounds.Width - w) / 2), Math.Floor((Bounds.Height - h) / 2), w, h);
        }
    }

    /// <summary>Copies the core's last frame into the bitmap.</summary>
    public void UpdateFrame()
    {
        if (_play.Core is not { } core) return;
        using (var fb = _bitmap.Lock())
        {
            for (var y = 0; y < LibretroCore.Height; y++)
            {
                MemoryMarshal.Cast<uint, int>(core.Frame.AsSpan(y * LibretroCore.Width, LibretroCore.Width)).CopyTo(_row);
                Marshal.Copy(_row, 0, fb.Address + y * fb.RowBytes, LibretroCore.Width);
            }
        }
        InvalidateVisual();
    }

    IBrush? Token(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) ? v as IBrush : null;

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Token("lit/black") ?? Brushes.Black, new Rect(Bounds.Size));
        if (_play.Session == null) return;
        var r = ImageRect;
        // Preferences · Emulator · Filter: Pixel-perfect draws every game pixel as a sharp block
        using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = _play.Options.PixelPerfect ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality }))
            ctx.DrawImage(_bitmap, new Rect(0, 0, LibretroCore.Width, LibretroCore.Height), r);
        var w = _play.World;
        using (ctx.PushClip(r))
        {
            if (_play.TileCollision) DrawCollision(ctx, r, w);
            if (_play.Hitboxes) DrawHitboxes(ctx, r, w);
        }
    }

    void DrawHitboxes(DrawingContext ctx, Rect r, LiveWorld w)
    {
        var k = Scale;
        foreach (var a in w.Actors)
        {
            if (a.ColliderW == 0 || !a.Actor.Alive || !HasBody(a)) continue;
            var token = a.Data?.Kind switch
            {
                AgbStudio.Core.Project.ActorKind.Player => "status/success",
                AgbStudio.Core.Project.ActorKind.Enemy => "status/danger",
                AgbStudio.Core.Project.ActorKind.TriggerZone or AgbStudio.Core.Project.ActorKind.CameraZone => "accent/default",
                _ => "status/warning",
            };
            var x = Math.Floor(a.Actor.X) + a.ColliderX - w.CameraX;
            var y = Math.Floor(a.Actor.Y) + a.ColliderY - w.CameraY;
            var box = new Rect(r.X + x * k + 0.5, r.Y + y * k + 0.5, a.ColliderW * k - 1, a.ColliderH * k - 1);
            ctx.DrawRectangle(null, new Pen(Token(token), 1), box);
            if (Math.Abs(a.Actor.Vx) > 0.01 || Math.Abs(a.Actor.Vy) > 0.01)
                DrawVelocity(ctx, new Point(box.Center.X, box.Y), new Vector(a.Actor.Vx, a.Actor.Vy) * 8 * k, k);
        }
    }

    static readonly HashSet<string> BodyTypes = SceneEditor.Behaviours.Builtins.Where(b => b.Category == "Physics").Select(b => b.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Hitboxes are drawn for the actors that move with a physics body, as the Figma T-P02 Game View outlines the player
    /// and the slime only: a Physics behaviour (Platformer Body, Top-down Mover, Projectile), or a player or enemy. HUD
    /// actors (the hearts) and static triggers (flag, coins, the ? block) get none.
    /// </summary>
    internal static bool HasBody(LiveActorInfo a) =>
        a.Data?.Kind is ActorKind.Player or ActorKind.Enemy
        || a.Data?.Components.Any(c => BodyTypes.Contains(c.Type)) == true
        || a.Behaviours.Any(b => BodyTypes.Contains(b.Type));

    /// <summary>Velocity arrow of a moving actor (Figma T-P02 Game View): from the top of its hitbox, 8 frames of travel long.</summary>
    void DrawVelocity(DrawingContext ctx, Point from, Vector v, int k)
    {
        var brush = Token("lit/velocity");
        var len = v.Length;
        if (brush is null || len < 1) return;
        var dir = v / len;
        var head = Math.Min(len, 4.0 * k);
        var tip = from + v;
        var baseCentre = tip - dir * head;
        var side = new Vector(-dir.Y, dir.X) * (head * 0.6);
        ctx.DrawLine(new Pen(brush, k, lineCap: PenLineCap.Round), from, baseCentre);
        var arrow = new StreamGeometry();
        using (var g = arrow.Open())
        {
            g.BeginFigure(tip, true);
            g.LineTo(baseCentre + side);
            g.LineTo(baseCentre - side);
            g.EndFigure(true);
        }
        ctx.DrawGeometry(brush, null, arrow);
    }

    void DrawCollision(DrawingContext ctx, Rect r, LiveWorld w)
    {
        LoadMap(w.SceneName);
        if (_map is null || _map.CollisionLayer < 0 || _map.CollisionLayer >= _map.Layers.Count) return;
        var cells = _map.Layers[_map.CollisionLayer].Cells;
        var k = Scale;
        var fill = Token("accent/subtle");
        var pen = new Pen(Token("accent/default"), 1);
        var hazard = new Pen(Token("status/danger"), 1);
        int x0 = Math.Max(0, w.CameraX / 16), y0 = Math.Max(0, w.CameraY / 16);
        for (var ty = y0; ty <= y0 + 11 && ty < _map.Height; ty++)
            for (var tx = x0; tx <= x0 + 16 && tx < _map.Width; tx++)
            {
                var i = ty * _map.Width + tx;
                if (i >= cells.Count) continue;
                var c = _tiles?.Tiles.GetValueOrDefault(cells[i])?.Collision ?? CollisionType.None;
                if (c == CollisionType.None) continue;
                var rect = new Rect(r.X + (tx * 16 - w.CameraX) * k + 0.5, r.Y + (ty * 16 - w.CameraY) * k + 0.5, 16 * k - 1, (c == CollisionType.OneWay ? 4 : 16) * k - 1);
                ctx.DrawRectangle(fill, c == CollisionType.Hazard ? hazard : pen, rect);
            }
    }

    void LoadMap(string scene)
    {
        if (scene == _mapScene) return;
        _mapScene = scene;
        _map = null;
        _tiles = null;
        var p = _play.Services.Project.Current;
        var sceneFile = _play.WorldReader?.CurrentScene(_play.World);
        if (p is null || sceneFile is null || sceneFile.Map.Length == 0) return;
        try
        {
            var mapPath = Path.Combine(p.MapsDir, sceneFile.Map + ".map");
            if (!File.Exists(mapPath)) return;
            _map = GameProject.Load<MapFile>(mapPath);
            var tileset = p.TilesetFiles().FirstOrDefault(f => GameProject.AssetName(f) == _map.Tileset);
            if (tileset != null) _tiles = p.LoadMeta<TilesetMeta>(tileset);
        }
        catch (System.Text.Json.JsonException) { _map = null; }
    }

    /// <summary>The actor under a point of the game view (its collider), or null.</summary>
    public LiveActorInfo? ActorAt(Point p)
    {
        var r = ImageRect;
        if (!r.Contains(p)) return null;
        var w = _play.World;
        double gx = (p.X - r.X) / Scale + w.CameraX, gy = (p.Y - r.Y) / Scale + w.CameraY;
        return w.Actors.Where(a => a.ColliderW > 0 && a.Actor.Alive)
            .FirstOrDefault(a => gx >= Math.Floor(a.Actor.X) + a.ColliderX && gx < Math.Floor(a.Actor.X) + a.ColliderX + a.ColliderW
                              && gy >= Math.Floor(a.Actor.Y) + a.ColliderY && gy < Math.Floor(a.Actor.Y) + a.ColliderY + a.ColliderH);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        // Select tool: clicking an actor shows it in Inspector · Live
        if (ActorAt(e.GetPosition(this)) is { } a)
        {
            _play.SelectedActor = a.Name;
            _play.Publish();
        }
    }
}

/// <summary>The Play Mode viewport: game screen plus the fps / VBlank chips (top left) and the selected actor's state chip (bottom right).</summary>
public sealed class GameView : Panel
{
    readonly PlayController _play;
    public GameScreen Screen { get; }
    public Chip FpsChip { get; } = new() { Glyph = "clock", Name = "FpsChip", IsHitTestVisible = false };
    public Chip VBlankChip { get; } = new() { Name = "VBlankChip", IsHitTestVisible = false };
    public Chip ActorChip { get; } = new() { Glyph = "user", Name = "ActorStateChip", IsHitTestVisible = false };

    public GameView(PlayController play)
    {
        _play = play;
        Name = "GameView";
        Screen = new GameScreen(play);
        Children.Add(Screen);
        var top = UI.Row(8, FpsChip, VBlankChip);
        top.Margin = new Thickness(12, 12, 0, 0);
        top.HorizontalAlignment = HorizontalAlignment.Left;
        top.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(top);
        ActorChip.Margin = new Thickness(0, 0, 12, 12);
        ActorChip.HorizontalAlignment = HorizontalAlignment.Right;
        ActorChip.VerticalAlignment = VerticalAlignment.Bottom;
        Children.Add(ActorChip);
        Refresh();
    }

    public void Refresh()
    {
        Screen.UpdateFrame();
        FpsChip.Content = string.Create(CultureInfo.InvariantCulture, $"{_play.Fps:0.0} fps");
        VBlankChip.Content = string.Create(CultureInfo.InvariantCulture, $"vblank {_play.VBlankFreeMs:0.0} ms free");
        var a = _play.World.Find(_play.SelectedActor);
        ActorChip.Content = a is null ? null : $"{a.Name} · state {a.State}";
        ActorChip.IsVisible = a is not null;
        FpsChip.IsVisible = VBlankChip.IsVisible = _play.Session != null;
    }
}
