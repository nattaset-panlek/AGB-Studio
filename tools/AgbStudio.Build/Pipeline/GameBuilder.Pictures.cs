using System.Text;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Pipeline;

/// <summary>Pictures for the bitmap video modes: the scenes' Picture fields and the names scripts give Bitmap.Show become
/// ROM data in gen/pictures, with only the forms something can show.</summary>
public sealed partial class GameBuilder
{
    readonly Dictionary<string, string> _pictureSymbols = new(StringComparer.Ordinal);
    List<string> _scriptPictureTable = [];

    string? PictureSymbol(SceneFile scene) =>
        BitmapConverter.IsBitmapMode(scene.VideoMode) && _pictureSymbols.TryGetValue(scene.Picture, out var symbol) ? symbol : null;

    static string SizeText((int Width, int Height) size) => $"{size.Width}×{size.Height}";

    /// <summary>Checks every picture the game shows and writes its data. A missing image or a wrong size is an error that
    /// names the scene (or Bitmap.Show); the scene is left without a picture and the build goes on to report the rest.</summary>
    void GeneratePictures(List<(string File, SceneFile Scene)> scenes)
    {
        _pictureSymbols.Clear();
        _scriptPictureTable = [];
        var files = _p.ImageFiles().GroupBy(GameProject.AssetName, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var images = new Dictionary<string, ColorImage?>(StringComparer.Ordinal);
        var forms = new Dictionary<string, (bool Bgr, bool Indexed, string Owner)>(StringComparer.Ordinal);

        ColorImage? Load(string name, string owner, string how)
        {
            if (images.TryGetValue(name, out var known)) return known;
            ColorImage? img = null;
            try { img = ColorImage.Load(files[name]); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or FormatException)
            {
                Add(new Diagnostic(Severity.Error, owner, 0, 0, "", $"{how} '{name}', but that image could not be read ({e.Message.TrimEnd('.')}). Save it again as a PNG in your paint program, or pick another picture."));
            }
            return images[name] = img;
        }

        void Want(string name, bool bgr, bool indexed, string owner)
        {
            var had = forms.GetValueOrDefault(name, (false, false, owner));
            forms[name] = (had.Item1 || bgr, had.Item2 || indexed, had.Item3);
        }

        foreach (var (file, scene) in scenes)
        {
            if (scene.VideoMode is < 0 or > 5)
                Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Scene {scene.Name} has video mode {scene.VideoMode}; the GBA has modes 0 to 5. Pick a mode in the Video Mode popover."));
            if (scene.Picture.Length == 0) continue;
            if (!BitmapConverter.IsBitmapMode(scene.VideoMode))
            {
                Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Scene {scene.Name} has the picture '{scene.Picture}', but Mode {scene.VideoMode} draws tile layers, so the picture is not shown. Pick Mode 3, 4 or 5 in the Video Mode popover, or clear the picture."));
                continue;
            }
            if (!files.ContainsKey(scene.Picture))
            {
                Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Scene {scene.Name} shows the picture '{scene.Picture}', which is not in assets/images. Import the image, or pick another picture in the Video Mode popover."));
                continue;
            }
            if (Load(scene.Picture, file, $"Scene {scene.Name} shows the picture") is not { } img) continue;
            var size = BitmapConverter.PictureSize(scene.VideoMode);
            if ((img.Width, img.Height) != size)
            {
                Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Scene {scene.Name} shows the picture '{scene.Picture}', which is {img.Width}×{img.Height}; Mode {scene.VideoMode} shows {SizeText(size)} pictures. Resize the image to {SizeText(size)} in your paint program, or pick another picture in the Video Mode popover."));
                continue;
            }
            Want(scene.Picture, scene.VideoMode != 4, scene.VideoMode == 4, file);
        }

        // Bitmap.Show: the compiler knows the name, not the scene, so the picture gets every form its size and the
        // project's scenes can use.
        var hasMode = new Func<int, bool>(mode => scenes.Any(s => s.Scene.VideoMode == mode));
        var scriptPictures = _scriptOut.Pictures.Where(files.ContainsKey).OrderBy(n => n, StringComparer.Ordinal).ToList();
        foreach (var name in scriptPictures)
        {
            if (Load(name, _p.ProjectPath, "A script shows the picture") is not { } img) continue;
            var fits3or4 = (img.Width, img.Height) == BitmapConverter.PictureSize(3);
            var fits5 = (img.Width, img.Height) == BitmapConverter.PictureSize(5);
            if (!fits3or4 && !fits5)
            {
                Add(new Diagnostic(Severity.Error, _p.ProjectPath, 0, 0, "", $"A script shows the picture '{name}', which is {img.Width}×{img.Height}; Modes 3 and 4 show 240×160 pictures and Mode 5 shows 160×128. Resize the image, or show another picture."));
                continue;
            }
            var bgr = (fits3or4 && hasMode(3)) || (fits5 && hasMode(5));
            var indexed = fits3or4 && hasMode(4);
            if (!bgr && !indexed)
            {
                Add(new Diagnostic(Severity.Warning, _p.ProjectPath, 0, 0, "", $"A script shows the picture '{name}', but no scene uses {(fits5 ? "Mode 5" : "Mode 3 or 4")}, the only {(fits5 ? "mode" : "modes")} that can show it at {img.Width}×{img.Height}, so it is left out of the game. Change a scene's video mode, or resize the image."));
                continue;
            }
            Want(name, bgr, indexed, _p.ProjectPath);
        }

        foreach (var (name, form) in forms.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            WritePicture(name, files[name], images[name]!, form.Bgr, form.Indexed, form.Owner);

        // Picture ids (GameProject.PictureNames): an entry for every image, so ids stay stable when a script drops one.
        if (scriptPictures.Any(_pictureSymbols.ContainsKey))
            _scriptPictureTable = _p.PictureNames().Select(n => scriptPictures.Contains(n) && _pictureSymbols.TryGetValue(n, out var s) ? s : "").ToList();
    }

    void WritePicture(string name, string file, ColorImage img, bool bgr, bool indexed, string owner)
    {
        var id = Cpp.Ident(name);
        var symbol = "agbasset_picture_" + id;
        var sb = new StringBuilder();
        sb.Append("// Generated by AGB Studio from ").Append(Path.GetFileName(file)).Append(". Do not edit.\n#include \"agb/data.hpp\"\nnamespace {\n");
        var bytes = 0L;
        if (bgr)
        {
            var pixels = BitmapConverter.ToBgr(img);
            Cpp.U16(sb, "__attribute__((aligned(4))) const unsigned short kBgr", pixels.Select(c => (int)c));
            bytes += pixels.Length * 2L;
        }
        var colors = 0;
        if (indexed)
        {
            var picture = BitmapConverter.ToIndexed(img);
            if (picture.Reduced)
                Add(new Diagnostic(Severity.Warning, owner, 0, 0, "", $"The picture '{name}' has {picture.SourceColors} colours and Mode 4 shows {BitmapConverter.MaxColors}, so the build reduced it to {picture.Palette.Length}. To keep every colour, use Mode 3, or reduce the image in your paint program."));
            // Cpp.Bytes writes 32-bit words, aligned for DMA.
            Cpp.Bytes(sb, "const unsigned int kIndices", picture.Indices);
            Cpp.U16(sb, "const unsigned short kPalette", picture.Palette.Select(c => (int)c));
            colors = picture.Palette.Length;
            bytes += picture.Indices.Length + colors * 2L;
        }
        sb.Append("}  // namespace\n");
        sb.Append($"extern const agb::data::Picture {symbol} = {{{(bgr ? "kBgr" : "nullptr")}, {(indexed ? "reinterpret_cast<const unsigned char*>(kIndices)" : "nullptr")}, {(indexed ? "kPalette" : "nullptr")}, {colors}, {img.Width}, {img.Height}}};\n");
        Write(Path.Combine(GenDir, "pictures", $"picture_{id}.cpp"), sb.ToString());
        _pictureSymbols[name] = symbol;
        Assets.Tilesets[name + " (picture)"] = new AssetInfo(name, "Picture", file, (int)bytes, 0, $"{img.Width}x{img.Height} px");
    }

    /// <summary>What a scene in a bitmap mode has that the mode cannot show: the frame buffers fill the memory of tile
    /// layers and text.</summary>
    void WarnAboutBitmapMode(string file, SceneFile scene, string mapPath)
    {
        var lost = new List<string>();
        if (scene.Map.Length > 0 && File.Exists(mapPath) && GameProject.Load<MapFile>(mapPath).Layers.Any(l => l.Type != LayerType.CollisionOnly)) lost.Add("map layers");
        if (scene.Actors.Any(a => !a.Hidden && a.Kind is ActorKind.TextLabel or ActorKind.Panel && a.Text.Length > 0)) lost.Add("Text Labels");
        if (scene.Menu != null) lost.Add("menu");
        if (scene.Page.Length > 0 || scene.Hud.Length > 0) lost.Add("UI page");
        if (lost.Count > 0)
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Scene {scene.Name} uses Mode {scene.VideoMode}, whose picture fills the memory for tiles and text, so its {string.Join(", ", lost)} {(lost.Count > 1 ? "are" : "is")} not shown (a map's collision still works). Use Mode 0, 1 or 2 to show them."));
    }
}
