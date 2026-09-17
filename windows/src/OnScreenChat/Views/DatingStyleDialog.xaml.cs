using Microsoft.UI.Xaml.Controls;

namespace OnScreenChat.Views;

/// <summary>
/// Edits the "My texting style" block for the Dating reply skill. Stored apart
/// from the skill prompt so it survives prompt re-tuning, and injected at send
/// time so all three reply options come out in the user's own voice.
/// </summary>
public sealed partial class DatingStyleDialog : ContentDialog
{
    public DatingStyleDialog(string style)
    {
        InitializeComponent();

        StyleBox.Text = style;
        SecondaryButtonClick += (_, args) =>
        {
            args.Cancel = true; // clearing the box is not the same as closing
            StyleBox.Text = string.Empty;
        };
        PrimaryButtonClick += (_, _) => Result = StyleBox.Text.Trim();
    }

    /// <summary>The saved style text, once the dialog closes with Save.</summary>
    public string Result { get; private set; } = string.Empty;
}
