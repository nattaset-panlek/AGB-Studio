using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AgbStudio.Editor.Controls;

/// <summary>Figma "Asset Card": 102 px card with a thumb/* preview, name and meta; Default / Selected.</summary>
public class AssetCard : SelectableRow
{
    public static readonly StyledProperty<string?> NameTextProperty = AvaloniaProperty.Register<AssetCard, string?>(nameof(NameText));
    public static readonly StyledProperty<string?> MetaProperty = AvaloniaProperty.Register<AssetCard, string?>(nameof(Meta));
    public static readonly StyledProperty<string?> PreviewProperty = AvaloniaProperty.Register<AssetCard, string?>(nameof(Preview), "thumb/hero");

    public string? NameText { get => GetValue(NameTextProperty); set => SetValue(NameTextProperty, value); }
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public string? Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }
}

/// <summary>Figma "Frame Thumb": 66 px animation frame with label; Default / Active.</summary>
public class FrameThumb : SelectableRow
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<FrameThumb, string?>(nameof(Label));
    public static readonly StyledProperty<string?> PreviewProperty = AvaloniaProperty.Register<FrameThumb, string?>(nameof(Preview), "thumb/hero");

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }
}

/// <summary>Figma "Scene Card": 166 px card with an art/scene/* preview; Default / Selected.</summary>
public class SceneCard : SelectableRow
{
    public static readonly StyledProperty<string?> NameTextProperty = AvaloniaProperty.Register<SceneCard, string?>(nameof(NameText));
    public static readonly StyledProperty<string?> MetaProperty = AvaloniaProperty.Register<SceneCard, string?>(nameof(Meta));
    public static readonly StyledProperty<string?> PreviewProperty = AvaloniaProperty.Register<SceneCard, string?>(nameof(Preview), "art/scene/day");
    public static readonly StyledProperty<Avalonia.Media.IImage?> PreviewImageProperty = AvaloniaProperty.Register<SceneCard, Avalonia.Media.IImage?>(nameof(PreviewImage));

    public string? NameText { get => GetValue(NameTextProperty); set => SetValue(NameTextProperty, value); }
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public string? Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }
    /// <summary>A picture of the real scene, drawn over <see cref="Preview"/> (World Map).</summary>
    public Avalonia.Media.IImage? PreviewImage { get => GetValue(PreviewImageProperty); set => SetValue(PreviewImageProperty, value); }
}

/// <summary>Figma "Template Card": 252 px card with an art/genre/* preview, title and description; Default / Selected.</summary>
public class TemplateCard : SelectableRow
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<TemplateCard, string?>(nameof(Title));
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<TemplateCard, string?>(nameof(Description));
    public static readonly StyledProperty<string?> PreviewProperty = AvaloniaProperty.Register<TemplateCard, string?>(nameof(Preview), "art/genre/platformer");

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }
}

public enum GlyphCellState { Default, Selected, Missing }

/// <summary>Figma "Glyph Cell": 44 x 48 character cell with code point; Default / Selected / Missing.</summary>
public class GlyphCell : TemplatedControl
{
    public static readonly StyledProperty<string?> CharProperty = AvaloniaProperty.Register<GlyphCell, string?>(nameof(Char));
    public static readonly StyledProperty<string?> CodeProperty = AvaloniaProperty.Register<GlyphCell, string?>(nameof(Code));
    public static readonly StyledProperty<GlyphCellState> StateProperty = AvaloniaProperty.Register<GlyphCell, GlyphCellState>(nameof(State));

    public string? Char { get => GetValue(CharProperty); set => SetValue(CharProperty, value); }
    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public GlyphCellState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
}

public enum TableCellKind { Header, Default, Mono, Selected }

/// <summary>Figma "Table Cell": Header / Default / Mono / Selected.</summary>
public class TableCell : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<TableCell, string?>(nameof(Text));
    public static readonly StyledProperty<TableCellKind> KindProperty = AvaloniaProperty.Register<TableCell, TableCellKind>(nameof(Kind), TableCellKind.Default);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public TableCellKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
}

/// <summary>Figma "Text Input": label, 32 px box and optional help text; Default / Focus.</summary>
public class TextField : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<TextField, string?>(nameof(Label));
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<TextField, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<string?> HelpProperty = AvaloniaProperty.Register<TextField, string?>(nameof(Help));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Help { get => GetValue(HelpProperty); set => SetValue(HelpProperty, value); }
}

/// <summary>Figma "Select": label, 32 px box with chevron and optional help text; Default / Focus.</summary>
public class SelectField : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<SelectField, string?>(nameof(Label));
    public static readonly StyledProperty<string?> HelpProperty = AvaloniaProperty.Register<SelectField, string?>(nameof(Help));
    public static readonly StyledProperty<IReadOnlyList<string>?> OptionsProperty = AvaloniaProperty.Register<SelectField, IReadOnlyList<string>?>(nameof(Options));
    public static readonly StyledProperty<string?> SelectedProperty = AvaloniaProperty.Register<SelectField, string?>(nameof(Selected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Help { get => GetValue(HelpProperty); set => SetValue(HelpProperty, value); }
    public IReadOnlyList<string>? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public string? Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
}

public enum NodeCategory { Event, Input, Physics, Flow, Data, Sound, Actor, Math }

/// <summary>Figma "Node Header": Logic Graph node title bar per category.</summary>
public class NodeHeader : TemplatedControl
{
    public static readonly StyledProperty<NodeCategory> CategoryProperty = AvaloniaProperty.Register<NodeHeader, NodeCategory>(nameof(Category));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<NodeHeader, string?>(nameof(Title));

    public NodeCategory Category { get => GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
}

public enum PinType { Exec, Bool, Fixed, Actor, Text, Vector }
public enum PinDir { In, Out }

/// <summary>Figma "Node Pin": data type colour, In (hollow) / Out (filled); Exec is a play arrow.</summary>
public class NodePin : TemplatedControl
{
    public static readonly StyledProperty<PinType> TypeProperty = AvaloniaProperty.Register<NodePin, PinType>(nameof(Type));
    public static readonly StyledProperty<PinDir> DirProperty = AvaloniaProperty.Register<NodePin, PinDir>(nameof(Dir));

    public PinType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    public PinDir Dir { get => GetValue(DirProperty); set => SetValue(DirProperty, value); }
}

public enum AppIconTheme { Light, Dark, Indigo }

/// <summary>Figma "brand/app-icon": Plus Pad mark on a 128 px rounded tile; Light / Dark / Indigo. Scales with Width/Height.</summary>
public class AppIcon : TemplatedControl
{
    public static readonly StyledProperty<AppIconTheme> VariantProperty = AvaloniaProperty.Register<AppIcon, AppIconTheme>(nameof(Variant));
    public AppIconTheme Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
}
