using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OnScreenChat.Core.Data;

public enum ChatRole
{
    User,
    Assistant,

    /// <summary>
    /// A local acknowledgement (e.g. "Rule saved") shown in the transcript.
    /// Never sent to the model — see the client request builders.
    /// </summary>
    Note,
}

/// <summary>
/// One turn in the transcript. Identity is the <see cref="Id"/>, not the text:
/// a reply is found by id and appended to while it streams.
/// </summary>
public sealed class ChatMessage : INotifyPropertyChanged
{
    private string _text;

    public ChatMessage(ChatRole role, string text = "")
    {
        Role = role;
        _text = text;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public ChatRole Role { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
