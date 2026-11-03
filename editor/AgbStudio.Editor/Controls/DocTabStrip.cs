using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Controls;

public sealed class DocTabStrip : StackPanel
{
    #region [Variables/Fields]
    readonly EditorServices _s;
    #endregion

    #region [Properties]
    public IReadOnlyList<DocTab> Tabs => Children.OfType<DocTab>().ToList();
    #endregion

    #region [Unity Methods]
    public DocTabStrip(EditorServices s)
    {
        _s = s;
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Stretch;
        Name = "DocTabs";

        PageChrome.WhileAttached(this, () =>
        {
            _s.Documents.Changed += Rebuild;
            Rebuild();
        }, () =>
        {
            _s.Documents.Changed -= Rebuild;
        });

        Rebuild();
    }
    #endregion

    #region [Public Methods]
    public void Rebuild()
    {
        Children.Clear();
        var docs = _s.Documents.Open.ToList();
        for (var i = 0; i < docs.Count; i++)
        {
            var d = docs[i];
            var tab = new DocTab
            {
                Label = d.Name,
                Glyph = d.Glyph,
                IsDirty = d.IsDirty,
                IsSelected = d == _s.Documents.Active,
                IsClosable = d.CanClose,
                Height = 33,
                Name = "Doc_" + d.Name,
            };

            var doc = d;
            var docIndex = i;
            tab.Activated += (_, _) => PageChrome.Activate(_s, doc);
            tab.CloseRequested += (_, _) => _s.Documents.Close(doc);

            var menu = new ContextMenu();
            var closeItem = new MenuItem { Header = "Close", IsEnabled = doc.CanClose };
            closeItem.Click += (_, _) => _s.Documents.Close(doc);

            var closeOthers = new MenuItem { Header = "Close Other Tabs", IsEnabled = docs.Any(other => other != doc && other.CanClose) };
            closeOthers.Click += (_, _) => CloseOthers(doc);

            var closeRight = new MenuItem { Header = "Close Tabs to the Right", IsEnabled = docs.Skip(docIndex + 1).Any(other => other.CanClose) };
            closeRight.Click += (_, _) => CloseRight(docIndex);

            var closeAll = new MenuItem { Header = "Close All Tabs", IsEnabled = docs.Any(other => other.CanClose) };
            closeAll.Click += (_, _) => CloseAll();

            menu.Items.Add(closeItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(closeOthers);
            menu.Items.Add(closeRight);
            menu.Items.Add(closeAll);

            tab.ContextMenu = menu;
            Children.Add(tab);
        }
    }
    #endregion

    #region [Private Methods]
    void CloseOthers(EditorDocument keep)
    {
        var toClose = _s.Documents.Open.Where(d => d != keep && d.CanClose).ToList();
        foreach (var d in toClose) _s.Documents.Close(d);
    }

    void CloseRight(int currentIndex)
    {
        var toClose = _s.Documents.Open.Skip(currentIndex + 1).Where(d => d.CanClose).ToList();
        foreach (var d in toClose) _s.Documents.Close(d);
    }

    void CloseAll()
    {
        var toClose = _s.Documents.Open.Where(d => d.CanClose).ToList();
        foreach (var d in toClose) _s.Documents.Close(d);
    }
    #endregion
}
