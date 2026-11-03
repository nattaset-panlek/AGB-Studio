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
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(text, status, duration));
            return new ToastMessage(text, status);
        }
        var t = new ToastMessage(text, status);
        Visible.Add(t);
        var d = duration ?? DefaultDuration;
        if (d > TimeSpan.Zero) DispatcherTimer.RunOnce(() => Close(t), d);
        return t;
    }

    public void Close(ToastMessage t)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Close(t));
            return;
        }
        Visible.Remove(t);
    }
}
