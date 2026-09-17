namespace OnScreenChat.Core.Prompts;

/// <summary>
/// Optional calibration for the "Dating reply" skill. It overrides the model's
/// own read of how interested she is — which is what drives how far the three
/// reply options escalate. <see cref="Auto"/> is the default and injects nothing
/// (the model judges from the messages).
/// </summary>
public enum DatingHeat
{
    Auto,
    Cool,
    Warm,
    Spicy,
}

public static class DatingHeatExtensions
{
    /// <summary>The skill this calibration applies to.</summary>
    public const string SkillId = "dating";

    public static string Label(this DatingHeat heat) => heat switch
    {
        DatingHeat.Auto => "Auto",
        DatingHeat.Cool => "Cool",
        DatingHeat.Warm => "Warm",
        DatingHeat.Spicy => "Spicy",
        _ => "Auto",
    };

    /// <summary>
    /// Appended to the skill's system prompt at send time. <see cref="DatingHeat.Auto"/>
    /// adds nothing so the model keeps reading her interest straight from the thread.
    /// </summary>
    public static string PromptDirective(this DatingHeat heat) => heat switch
    {
        DatingHeat.Cool => DatingHeatText.Cool,
        DatingHeat.Warm => DatingHeatText.Warm,
        DatingHeat.Spicy => DatingHeatText.Spicy,
        _ => string.Empty,
    };

    /// <summary>Parses the persisted setting value; unknown input falls back to Auto.</summary>
    public static DatingHeat Parse(string? raw) =>
        Enum.TryParse<DatingHeat>(raw, ignoreCase: true, out var heat) ? heat : DatingHeat.Auto;
}
