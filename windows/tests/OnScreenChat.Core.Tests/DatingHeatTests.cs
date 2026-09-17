using OnScreenChat.Core.Prompts;

namespace OnScreenChat.Core.Tests;

public class DatingHeatTests
{
    [Fact]
    public void Auto_injects_nothing_so_the_model_reads_the_thread_itself()
    {
        Assert.Equal(string.Empty, DatingHeat.Auto.PromptDirective());
    }

    [Theory]
    [InlineData(DatingHeat.Cool)]
    [InlineData(DatingHeat.Warm)]
    [InlineData(DatingHeat.Spicy)]
    public void Calibrations_append_a_separated_block(DatingHeat heat)
    {
        var directive = heat.PromptDirective();
        Assert.StartsWith("\n\nCALIBRATION:", directive);
    }

    [Fact]
    public void Cool_pulls_the_spicy_slot_back_to_a_re_engagement()
    {
        Assert.Contains("re-engagement or pattern-break", DatingHeat.Cool.PromptDirective());
    }

    [Fact]
    public void Spicy_stays_innuendo_never_explicit()
    {
        Assert.Contains("never crude or explicit", DatingHeat.Spicy.PromptDirective());
    }

    [Theory]
    [InlineData("spicy", DatingHeat.Spicy)]
    [InlineData("Warm", DatingHeat.Warm)]
    [InlineData("auto", DatingHeat.Auto)]
    [InlineData("nonsense", DatingHeat.Auto)]
    [InlineData(null, DatingHeat.Auto)]
    public void Parsing_the_persisted_setting_falls_back_to_auto(string? raw, DatingHeat expected)
    {
        Assert.Equal(expected, DatingHeatExtensions.Parse(raw));
    }

    [Fact]
    public void Every_level_has_a_picker_label()
    {
        Assert.All(Enum.GetValues<DatingHeat>(), heat => Assert.False(string.IsNullOrEmpty(heat.Label())));
    }
}
