using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OnScreenChat.Core.Data;
using Windows.System;

namespace OnScreenChat.Views;

/// <summary>
/// Lists the standing rules captured with <c>/command {…}</c> and lets me add or
/// remove them. Every rule here rides along with every reply.
/// </summary>
public sealed partial class RulesEditorDialog : ContentDialog
{
    private readonly RuleStore _store;

    public RulesEditorDialog(RuleStore store)
    {
        InitializeComponent();

        _store = store;
        SecondaryButtonClick += (_, args) =>
        {
            // Clearing the list is not the same as dismissing the dialog.
            args.Cancel = true;
            _store.Clear();
            Refresh();
        };

        Refresh();
    }

    private void OnAdd(object sender, RoutedEventArgs args)
    {
        _store.Add(DraftBox.Text);
        DraftBox.Text = string.Empty;
        DraftBox.Focus(FocusState.Programmatic);
        Refresh();
    }

    private void OnDraftKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter) return;
        args.Handled = true;
        OnAdd(sender, args);
    }

    private void OnDeleteLast(object sender, RoutedEventArgs args)
    {
        if (_store.Rules.Count == 0) return;
        _store.Delete(_store.Rules[^1]);
        Refresh();
    }

    private void Refresh()
    {
        RuleList.ItemsSource = _store.Rules.ToList();

        var empty = _store.Rules.Count == 0;
        EmptyLabel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        IsSecondaryButtonEnabled = !empty;
        DeleteButton.IsEnabled = !empty;
    }
}
