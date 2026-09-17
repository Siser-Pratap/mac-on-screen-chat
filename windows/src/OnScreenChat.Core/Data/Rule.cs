namespace OnScreenChat.Core.Data;

/// <summary>
/// A standing rule captured from <c>/command {…}</c>. Unlike a <c>WW:{…}</c>
/// directive, which steers one reply, a rule is persisted and applied to every
/// reply until it's deleted — including across relaunches and "New chat".
/// </summary>
public sealed record Rule
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required int SortOrder { get; init; }
}
