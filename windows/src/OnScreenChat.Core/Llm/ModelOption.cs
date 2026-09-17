namespace OnScreenChat.Core.Llm;

/// <summary>
/// Kept as an enum with a single case on purpose: it preserves the Mac app's
/// provider seam, so adding a second backend later is additive rather than a
/// refactor. The Windows build ships no local models.
/// </summary>
public enum ModelProvider
{
    Gemini,
}

/// <summary>A selectable model the user can switch between.</summary>
public sealed record ModelOption
{
    /// <summary>Stable id, persisted to settings.</summary>
    public required string Id { get; init; }

    /// <summary>Shown in the picker.</summary>
    public required string Label { get; init; }

    public required ModelProvider Provider { get; init; }

    /// <summary>Backend model identifier.</summary>
    public required string ModelName { get; init; }

    public static IReadOnlyList<ModelOption> All { get; } =
    [
        new ModelOption
        {
            Id = "gemini:gemini-2.5-flash",
            Label = "Gemini · 2.5 Flash",
            Provider = ModelProvider.Gemini,
            ModelName = "gemini-2.5-flash",
        },
        new ModelOption
        {
            Id = "gemini:gemini-2.5-pro",
            Label = "Gemini · 2.5 Pro",
            Provider = ModelProvider.Gemini,
            ModelName = "gemini-2.5-pro",
        },
    ];

    /// <summary>Flash by default — the cheaper, faster of the two.</summary>
    public static ModelOption Default => All[0];

    public static ModelOption Option(string? id) =>
        All.FirstOrDefault(option => option.Id == id) ?? Default;
}
