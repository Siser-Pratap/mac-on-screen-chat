using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OnScreenChat.Core.Data;
using Windows.ApplicationModel.DataTransfer;

namespace OnScreenChat.Views;

/// <summary>
/// One turn in the transcript. Laid out in code rather than with a template
/// selector, because the three roles differ in alignment and colour more than
/// they differ in structure.
/// </summary>
public sealed partial class MessageBubble : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message), typeof(ChatMessage), typeof(MessageBubble),
            new PropertyMetadata(null, OnMessageChanged));

    private ChatMessage? _bound;

    public MessageBubble()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public ChatMessage? Message
    {
        get => (ChatMessage?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private static void OnMessageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MessageBubble)sender).Rebind(args.NewValue as ChatMessage);

    private void Rebind(ChatMessage? message)
    {
        Detach();
        _bound = message;
        if (message is null) return;

        // The reply is appended to in place while it streams.
        message.PropertyChanged += OnMessagePropertyChanged;
        Apply();
    }

    private void Detach()
    {
        if (_bound is null) return;
        _bound.PropertyChanged -= OnMessagePropertyChanged;
        _bound = null;
    }

    private void OnMessagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) =>
        DispatcherQueue.TryEnqueue(Apply);

    private void Apply()
    {
        if (_bound is null) return;

        if (_bound.Role == ChatRole.Note)
        {
            NoteBubble.Visibility = Visibility.Visible;
            TurnRow.Visibility = Visibility.Collapsed;
            NoteText.Text = _bound.Text;
            return;
        }

        NoteBubble.Visibility = Visibility.Collapsed;
        TurnRow.Visibility = Visibility.Visible;

        var isUser = _bound.Role == ChatRole.User;

        Bubble.HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Bubble.Background = isUser
            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        BodyText.Foreground = isUser
            ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        BodyText.Text = _bound.Text;

        // An assistant bubble with nothing in it yet is the "thinking" state.
        var awaiting = !isUser && _bound.Text.Length == 0;
        Spinner.Visibility = awaiting ? Visibility.Visible : Visibility.Collapsed;
        BodyText.Visibility = awaiting ? Visibility.Collapsed : Visibility.Visible;

        CopyButton.Visibility = !isUser && _bound.Text.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnCopy(object sender, RoutedEventArgs args)
    {
        if (_bound is null) return;

        var package = new DataPackage();
        package.SetText(_bound.Text);
        Clipboard.SetContent(package);

        CopyGlyph.Glyph = ""; // checkmark
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1.2);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => CopyGlyph.Glyph = "";
        timer.Start();
    }
}
