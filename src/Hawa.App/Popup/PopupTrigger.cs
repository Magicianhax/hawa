using System.Windows;
using Hawa.Core.Model;

namespace Hawa.App.Popup;

/// <summary>Shows the card on lid open / connect according to settings.</summary>
public sealed class PopupTrigger : IDisposable
{
    private readonly AppServices _services;
    private readonly PopupCard _card;

    public PopupTrigger(AppServices services, PopupCard card)
    {
        _services = services;
        _card = card;
        services.Store.TransitionOccurred += OnTransition;
    }

    private void OnTransition(Transition t)
    {
        var s = _services.Settings;
        bool show = t switch
        {
            LidOpened => s.PopupOnLidOpen,
            Connected => s.PopupOnConnect,
            _ => false,
        };
        if (!show) return;
        Application.Current.Dispatcher.BeginInvoke(() => _card.ShowFor(TimeSpan.FromSeconds(s.PopupDurationSeconds)));
    }

    public void Dispose() => _services.Store.TransitionOccurred -= OnTransition;
}
