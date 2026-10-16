using FF14Accessibility.Services;

namespace RussianDescriptions.Tests;

public class DescriptionTermsTests
{
    [Fact]
    public void ReplacesNamesAfterEvaluationWithoutChangingNumbers()
    {
        var source = "Мощность Basic Synthesis: 120. Great Strides, 3 шага, 20%.";
        var result = RussianDescriptionTerms.Translate(source, true);
        Assert.Equal("Мощность Базовый синтез: 120. Большие шаги, 3 шага, 20%.", result);
        Assert.Equal(source, RussianDescriptionTerms.Translate(source, false));
    }

    [Fact]
    public void DoesNotReplaceSubstringsInsideNamesOrIdentifiers()
    {
        var text = "SomeBasic SynthesisX _Great Strides2 Basic Synthesis_7";
        Assert.Equal(text, RussianDescriptionTerms.Translate(text, true));
        Assert.Equal("", RussianDescriptionTerms.Translate("", true));
    }

    [Fact]
    public void LongestNameWinsAndUnlistedTextRemainsReadable()
    {
        var source = "Basic Synthesis II; Basic Synthesis; неизвестное умение 12345.";
        var result = RussianDescriptionTerms.Translate(source, true);
        Assert.Equal("Базовый синтез II; Базовый синтез; неизвестное умение 12345.", result);
    }
}
