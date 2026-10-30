using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Services;

/// <summary>
/// A panel whose selection can be cut, copied, pasted, duplicated, deleted and fully selected (actors, assets, nodes,
/// dialogue lines, table rows). Phase 11 windows implement it.
/// </summary>
public interface IEditTarget
{
    bool CanCopy { get; }
    bool CanPaste { get; }
    void Cut();
    void Copy();
    void Paste();
    void Duplicate();
    void Delete();
    void SelectAll();
}

/// <summary>Routes the Edit menu (P10.11, T-M02) to the focused text box or the closest <see cref="IEditTarget"/>.</summary>
public sealed class EditRouter(Func<TopLevel?> top)
{
    /// <summary>Fallback target when focus is not inside one (the active document's main panel).</summary>
    public IEditTarget? DefaultTarget { get; set; }

    object? Focused => top()?.FocusManager?.GetFocusedElement();

    public IEditTarget? CurrentTarget =>
        (Focused as Visual)?.GetSelfAndVisualAncestors().OfType<IEditTarget>().FirstOrDefault() ?? DefaultTarget;

    public bool Run(EditAction a)
    {
        if (Focused is TextBox tb)
        {
            switch (a)
            {
                case EditAction.Cut: tb.Cut(); return true;
                case EditAction.Copy: tb.Copy(); return true;
                case EditAction.Paste: tb.Paste(); return true;
                case EditAction.SelectAll: tb.SelectAll(); return true;
                case EditAction.Delete:
                    if (tb.SelectionStart != tb.SelectionEnd) tb.SelectedText = "";
                    return true;
                default: return false;
            }
        }
        if (CurrentTarget is not { } t) return false;
        switch (a)
        {
            case EditAction.Cut: if (!t.CanCopy) return false; t.Cut(); break;
            case EditAction.Copy: if (!t.CanCopy) return false; t.Copy(); break;
            case EditAction.Paste: if (!t.CanPaste) return false; t.Paste(); break;
            case EditAction.Duplicate: if (!t.CanCopy) return false; t.Duplicate(); break;
            case EditAction.Delete: if (!t.CanCopy) return false; t.Delete(); break;
            case EditAction.SelectAll: t.SelectAll(); break;
        }
        return true;
    }
}

public enum EditAction { Cut, Copy, Paste, Duplicate, Delete, SelectAll }
