using System.Collections.ObjectModel;
using AgbStudio.Editor.Controls;
using Avalonia.Threading;

namespace AgbStudio.Editor.Services;

public sealed record ToastMessage(string Text, ToastStatus Status);

/// <summary>Toasts (P10.18): shown in the main window and closed by the user or after a timeout.</summary>
public sealed class ToastService
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

    public ObservableCollection<ToastMessage> Visible { get; } = [];

    public ToastMessage Show(string text, ToastStatus status = ToastStatus.Success, TimeSpan? duration = null)
    {
        var t = new ToastMessage(text, status);
        Visible.Add(t);
        var d = duration ?? DefaultDuration;
        if (d > TimeSpan.Zero) DispatcherTimer.RunOnce(() => Close(t), d);
        return t;
    }

    public void Close(ToastMessage t) => Visible.Remove(t);
}
