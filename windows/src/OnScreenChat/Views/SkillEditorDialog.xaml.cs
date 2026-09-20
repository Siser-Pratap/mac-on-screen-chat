using Microsoft.UI.Xaml.Controls;
using OnScreenChat.Core.Data;

namespace OnScreenChat.Views;

/// <summary>Edits a skill's name, input hint, and system prompt.</summary>
public sealed partial class SkillEditorDialog : ContentDialog
{
    private readonly Skill _original;

    internal SkillEditorDialog(Skill skill)
    {
        InitializeComponent();

        _original = skill;

        PrimaryButtonClick += (_, _) => Result = _original with
        {
            Name = NameBox.Text.Trim(),
            InputHint = HintBox.Text,
            SystemPrompt = PromptBox.Text,
        };

        NameBox.Text = skill.Name;
        HintBox.Text = skill.InputHint;
        PromptBox.Text = skill.SystemPrompt;
    }

    /// <summary>
    /// The edited skill, once the dialog closes with Save. Deliberately
    /// <c>internal</c>: a public property of a Core model type makes the XAML
    /// type-info generator emit <c>new Skill()</c>, which cannot compile
    /// against Skill's <c>required</c> members. Only ChatPage reads it.
    /// </summary>
    internal Skill? Result { get; private set; }

    private void OnNameChanged(object sender, TextChangedEventArgs args) =>
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);
}
