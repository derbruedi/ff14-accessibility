using System.Text;
using FF14Accessibility.Services;

namespace RussianDescriptions.Tests;

public class ActionNameTests
{
    private static readonly RussianDescriptionCatalog Names = RussianDescriptionCatalog.Load("RussianActionNames");

    [Fact]
    public void RussianNameCandidatesDoNotCrossSheetsAndStillRequireSourceValidation()
    {
        Assert.Contains(100001u, Names.FindNameCandidates("CraftActionName", "Базовый синтез"));
        Assert.Empty(Names.FindNameCandidates("ActionName", "Базовый синтез"));
        Assert.Null(Names.Find("CraftActionName", 100001, Encoding.UTF8.GetBytes("Changed"), true));
    }

    [Theory]
    [InlineData("CraftActionName", 100001u, "Basic Synthesis", "Базовый синтез")]
    [InlineData("CraftActionName", 100002u, "Basic Touch", "Базовая обработка")]
    [InlineData("ActionName", 260u, "Great Strides", "Большие шаги")]
    [InlineData("BuddyActionName", 2u, "Withdraw", "Отозвать")]
    [InlineData("GeneralActionName", 2u, "Jump", "Прыжок")]
    public void NamesMatchTheirOwnRowAndSource(string table, uint id, string english, string russian)
    {
        var bytes = Names.Find(table, id, Encoding.UTF8.GetBytes(english), true);
        Assert.NotNull(bytes);
        Assert.Equal(russian, Encoding.UTF8.GetString(bytes));
        Assert.Null(Names.Find(table, id, Encoding.UTF8.GetBytes(english + " changed"), true));
        Assert.Null(Names.Find(table, id, Encoding.UTF8.GetBytes(english), false));
    }

    [Fact]
    public void NamesCannotCrossActionTypesOrReplaceDescriptions()
    {
        var source = Encoding.UTF8.GetBytes("Basic Synthesis");
        Assert.Null(Names.Find("ActionName", 100001, source, true));
        Assert.Null(Names.Find("CraftAction", 100001, source, true));
        Assert.Null(Names.Find("NoSuchSheet", 100001, source, true));
    }
}
