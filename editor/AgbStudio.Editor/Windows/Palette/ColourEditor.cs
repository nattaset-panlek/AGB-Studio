using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Palette;

/// <summary>
/// The "Colour N" block of the Sprite Editor and Palette Manager: the colour as a 46 px swatch, R / G / B sliders 0-31
/// (5 bits per channel), Hex (the 24-bit source colour) and the 15-bit value the ROM stores. Editing any of them
/// raises <see cref="ColourEdited"/> with the new ARGB colour.
/// </summary>
public sealed class ColourEditor : StackPanel
{
    readonly Swatch _swatch = new() { Width = 46, Height = 46, Name = "ColourSwatch", VerticalAlignment = VerticalAlignment.Center };
    readonly InspectorField[] _channels;
    readonly InspectorField _hex = new() { Kind = FieldKind.XY, Label = "Hex", Name = "HexField" };
    readonly TextBlock? _note;
    uint _argb;
    bool _updating;

    public event Action<uint>? ColourEdited;

    public uint Argb => _argb;
    public ushort Value => Colour15.Of(_argb);
    public InspectorField Red => _channels[0];
    public InspectorField Green => _channels[1];
    public InspectorField Blue => _channels[2];
    public InspectorField Hex => _hex;
    public TextBlock? Note => _note;

    public ColourEditor(bool withNote)
    {
        Name = "ColourEditor";
        _channels = new[] { "R", "G", "B" }.Select(l => new InspectorField { Kind = FieldKind.Slider, Label = l, Name = "Channel_" + l }).ToArray();
        var rows = new StackPanel { Spacing = 4 };
        rows.Children.AddRange(_channels);
        var top = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("46,12,*") };
        top.Children.Add(_swatch);
        Grid.SetColumn(rows, 2);
        top.Children.Add(rows);
        Children.Add(top);
        _hex.Margin = new Thickness(0, 6, 0, 0);
        Children.Add(_hex);
        if (withNote)
        {
            _note = UI.Text("", "Label/11", "text/muted");
            _note.TextWrapping = TextWrapping.Wrap;
            _note.Margin = new Thickness(0, 6, 0, 0);
            _note.Name = "ColourNote";
            Children.Add(_note);
        }
        foreach (var ch in _channels)
            ch.PropertyChanged += (_, e) => { if (e.Property == InspectorField.SliderValueProperty) OnSlider(); };
        _hex.PropertyChanged += (_, e) =>
        {
            if (_updating) return;
            if (e.Property == InspectorField.ValueProperty && Colour15.TryParse24(_hex.Value, out var argb) && argb != _argb) Edit(argb);
            else if (e.Property == InspectorField.Value2Property && Colour15.TryParse15(_hex.Value2, out var c) && c != Value) Edit(Colour15.Argb(c));
        };
    }

    /// <summary>Shows a colour (no event). Transparent shows the checker swatch and disables editing.</summary>
    public void Show(uint argb, bool editable = true)
    {
        _updating = true;
        try
        {
            _argb = argb;
            var transparent = Colour15.IsTransparent(argb);
            _swatch.State = transparent ? SwatchState.Transparent : SwatchState.Default;
            _swatch.Color = Colour15.ToColor(transparent ? 0 : argb);
            var c = Value;
            int[] levels = [Colour15.R(c), Colour15.G(c), Colour15.B(c)];
            for (var i = 0; i < 3; i++)
            {
                _channels[i].Value = transparent ? "-" : levels[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                _channels[i].SliderValue = transparent ? 0 : levels[i] * 100.0 / 31;
                _channels[i].IsEnabled = editable && !transparent;
            }
            _hex.Value = transparent ? "none" : Colour15.Hex24(argb);
            _hex.Value2 = Colour15.Hex15(transparent ? (ushort)0 : c);
            _hex.IsEnabled = editable && !transparent;
            if (_note != null)
                _note.Text = $"GBA colours are 15-bit (5 bits per channel), so {(transparent ? "index 0" : Colour15.Hex24(argb))} is stored as the nearest of 32 levels.";
        }
        finally { _updating = false; }
    }

    void OnSlider()
    {
        if (_updating) return;
        var levels = _channels.Select(ch => (int)Math.Round(ch.SliderValue * 31 / 100)).ToArray();
        var c = Colour15.Make(levels[0], levels[1], levels[2]);
        if (c == Value) return;
        Edit(Colour15.Argb(c));
    }

    /// <summary>Sets one channel (0-31), as dragging its slider does.</summary>
    public void SetChannel(int channel, int level)
    {
        var c = Value;
        int[] l = [Colour15.R(c), Colour15.G(c), Colour15.B(c)];
        l[channel] = Math.Clamp(level, 0, 31);
        Edit(Colour15.Argb(Colour15.Make(l[0], l[1], l[2])));
    }

    void Edit(uint argb)
    {
        Show(argb);
        ColourEdited?.Invoke(argb);
    }
}
