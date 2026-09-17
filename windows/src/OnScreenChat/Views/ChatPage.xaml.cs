using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OnScreenChat.Core.Data;
using OnScreenChat.ViewModels;
using Windows.System;

namespace OnScreenChat.Views;

/// <summary>The chat UI: header, transcript, heat dial, input bar.</summary>
public sealed partial class ChatPage : UserControl
{
    private ChatViewModel? _viewModel;

    public ChatPage()
    {
        InitializeComponent();
        Loaded += (_, _) => InputBox.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Controls that must keep receiving clicks rather than dragging the window.
    /// Everything else is a drag surface, as on macOS.
    /// </summary>
    public FrameworkElement[] InteractiveElements =>
    [
        SkillButton, ModelButton, NewChatButton,
        TranscriptScroller, HeatPicker, InputBox, SendButton,
    ];

    public void Bind(ChatViewModel viewModel)
    {
        _viewModel = viewModel;

        Transcript.ItemsSource = viewModel.Messages;
        viewModel.Messages.CollectionChanged += OnMessagesChanged;
        viewModel.PropertyChanged += (_, args) => OnViewModelChanged(args.PropertyName);

        HeatPicker.ItemsSource = viewModel.HeatLabels;
        HeatPicker.SelectedIndex = viewModel.HeatIndex;

        BuildSkillMenu();
        BuildModelMenu();
        RefreshSkill();
        RefreshEmptyState();
        RefreshSendButton();
    }

    // MARK: - Menus

    private void BuildSkillMenu()
    {
        if (_viewModel is null) return;

        SkillFlyout.Items.Clear();

        foreach (var skill in _viewModel.SkillOptions)
        {
            var item = new MenuFlyoutItem { Text = skill.Name, Tag = skill };
            item.Click += (sender, _) =>
            {
                if (((MenuFlyoutItem)sender).Tag is Skill chosen) _viewModel.SelectedSkill = chosen;
            };
            SkillFlyout.Items.Add(item);
        }

        SkillFlyout.Items.Add(new MenuFlyoutSeparator());

        if (_viewModel.ShowsHeatDial)
        {
            var style = new MenuFlyoutItem { Text = "My texting style…" };
            style.Click += async (_, _) => await ShowDatingStyleAsync();
            SkillFlyout.Items.Add(style);
        }

        var rules = new MenuFlyoutItem
        {
            Text = _viewModel.Rules.Rules.Count == 0
                ? "Rules…"
                : $"Rules ({_viewModel.Rules.Rules.Count})…",
        };
        rules.Click += async (_, _) => await ShowRulesAsync();
        SkillFlyout.Items.Add(rules);

        var edit = new MenuFlyoutItem { Text = $"Edit “{_viewModel.SelectedSkill.Name}”…" };
        edit.Click += async (_, _) => await ShowSkillEditorAsync();
        SkillFlyout.Items.Add(edit);
    }

    private void BuildModelMenu()
    {
        if (_viewModel is null) return;

        ModelFlyout.Items.Clear();

        foreach (var option in _viewModel.ModelOptions)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = option.Label,
                Tag = option,
                IsChecked = option.Id == _viewModel.SelectedModel.Id,
            };
            item.Click += (sender, _) =>
            {
                if (((ToggleMenuFlyoutItem)sender).Tag is Core.Llm.ModelOption chosen)
                {
                    _viewModel.SelectedModel = chosen;
                }
                BuildModelMenu();
            };
            ModelFlyout.Items.Add(item);
        }
    }

    // MARK: - Dialogs

    private async Task ShowSkillEditorAsync()
    {
        if (_viewModel is null) return;

        var dialog = new SkillEditorDialog(_viewModel.SelectedSkill) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } updated)
        {
            _viewModel.SaveSkill(updated);
            BuildSkillMenu();
            RefreshSkill();
        }
    }

    private async Task ShowRulesAsync()
    {
        if (_viewModel is null) return;

        var dialog = new RulesEditorDialog(_viewModel.Rules) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
        BuildSkillMenu();
    }

    private async Task ShowDatingStyleAsync()
    {
        if (_viewModel is null) return;

        var dialog = new DatingStyleDialog(_viewModel.DatingStyle) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.DatingStyle = dialog.Result;
        }
    }

    // MARK: - Input

    private void OnInputChanged(object sender, TextChangedEventArgs args)
    {
        if (_viewModel is null) return;
        _viewModel.InputText = InputBox.Text;
    }

    /// <summary>Enter sends; Shift+Enter inserts a newline, as on macOS.</summary>
    private async void OnInputKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter) return;

        var shift = InputEventHelper.IsShiftDown();
        if (shift) return; // let the newline through

        args.Handled = true;
        await SendAsync();
    }

    private async void OnSend(object sender, RoutedEventArgs args)
    {
        if (_viewModel is null) return;

        if (_viewModel.IsStreaming) _viewModel.Stop();
        else await SendAsync();
    }

    private async Task SendAsync()
    {
        if (_viewModel is null || !_viewModel.CanSend) return;

        InputBox.Text = string.Empty;
        await _viewModel.SendAsync();
    }

    private void OnNewChat(object sender, RoutedEventArgs args)
    {
        _viewModel?.NewChat();
        InputBox.Text = string.Empty;
        InputBox.Focus(FocusState.Programmatic);
    }

    private void OnHeatChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_viewModel is null) return;
        _viewModel.HeatIndex = HeatPicker.SelectedIndex;
    }

    // MARK: - Refresh

    private void OnViewModelChanged(string? property)
    {
        switch (property)
        {
            case nameof(ChatViewModel.SelectedSkill):
                RefreshSkill();
                BuildSkillMenu();
                break;

            case nameof(ChatViewModel.IsStreaming):
            case nameof(ChatViewModel.CanSend):
                RefreshSendButton();
                break;

            case nameof(ChatViewModel.SkillOptions):
                BuildSkillMenu();
                break;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        RefreshEmptyState();

        // Keep the newest turn in view, the way the Mac transcript autoscrolls.
        DispatcherQueue.TryEnqueue(() =>
            TranscriptScroller.ChangeView(null, TranscriptScroller.ScrollableHeight, null));
    }

    private void RefreshSkill()
    {
        if (_viewModel is null) return;

        SkillLabel.Text = _viewModel.SelectedSkill.Name;
        InputBox.PlaceholderText = _viewModel.InputHint;
        HeatBar.Visibility = _viewModel.ShowsHeatDial ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshSendButton()
    {
        if (_viewModel is null) return;

        var streaming = _viewModel.IsStreaming;

        // The send button becomes a stop button while a reply streams.
        SendGlyph.Glyph = streaming ? "" : "";
        SendButton.IsEnabled = streaming || _viewModel.CanSend;
        ToolTipService.SetToolTip(SendButton, streaming ? "Stop generating (Ctrl+.)" : "Send (Enter)");
    }

    private void RefreshEmptyState()
    {
        if (_viewModel is null) return;

        var empty = _viewModel.Messages.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

        if (!empty || SkillHints.Children.Count > 0) return;

        foreach (var skill in _viewModel.SkillOptions.Where(skill => skill.Id != "plain"))
        {
            var name = new TextBlock
            {
                Text = skill.Name,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            };
            var hint = new TextBlock
            {
                Text = skill.InputHint,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
            };
            SkillHints.Children.Add(new StackPanel { Children = { name, hint } });
        }
    }
}
