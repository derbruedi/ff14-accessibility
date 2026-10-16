using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Regression.Tests;

public unsafe class ChatPlayerTests
{
    [Fact]
    public void OneNamesakeFromAnotherWorldIsNeverTheRequestedPlayer()
    {
        Character character = default;
        character.HomeWorld = 10;
        var other = Player("Name Surname", (nint)(&character));
        Assert.Null(Find([other], new("Name Surname", "World 20", 20)));
    }

    [Fact]
    public void WorldDisambiguatesNamesakes()
    {
        Character first = default, second = default;
        first.HomeWorld = 10;
        second.HomeWorld = 20;
        var a = Player("Name Surname", (nint)(&first));
        var b = Player("Name Surname", (nint)(&second));
        Assert.Same(b, Find([a, b], new("Name Surname", "World 20", 20)));
        Assert.Null(Find([a, b], new("Name Surname", "", 0)));
    }

    [Fact]
    public void MissingPlayerAndAmbiguousWorldAreNotGuessed()
    {
        Character character = default;
        character.HomeWorld = 20;
        var a = Player("Name Surname", (nint)(&character));
        Assert.Null(Find([a], new("Different Name", "World 20", 20)));
        Assert.Null(Find([a, a], new("Name Surname", "World 20", 20)));
        Assert.Same(a, Find([a], new("Name Surname", "", 0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoryMenuUsesExactlyTheFocusedLineWithoutFallingBackToAnotherSender(bool legacy)
    {
        var sender = new TellTarget("Name Surname", "World", 20);
        if (legacy)
        {
            var history = new LegacyChatHistoryService(null!, new Configuration());
            history.Add(LegacyChatHistoryService.Category.Dialogue, "player line", sender: sender);
            Assert.Equal(sender, history.CurrentChatPlayer);
            history.Add(LegacyChatHistoryService.Category.Dialogue, "system line");
            Assert.Null(history.CurrentChatPlayer);
            Field(history, "_cursor", 0);
            Assert.Equal(sender, history.CurrentChatPlayer);
        }
        else
        {
            var history = new MessageHistoryService(null!, new Configuration());
            history.Add(MessageHistoryService.DialogueKey, "player line", sender: sender);
            Assert.Equal(sender, history.CurrentChatPlayer);
            history.Add(MessageHistoryService.DialogueKey, "system line");
            Assert.Null(history.CurrentChatPlayer);
            Field(history, "_cursor", 0);
            Assert.Equal(sender, history.CurrentChatPlayer);
        }
    }

    private static IGameObject Player(string name, nint address) => Proxy.Of<IGameObject>(m => m.Name switch
    {
        "get_Name" => new SeString(new TextPayload(name)),
        "get_ObjectKind" => ObjectKind.Pc,
        "get_Address" => address,
        _ => throw new InvalidOperationException(m.Name)
    });

    private static object? Find(IGameObject[] players, TellTarget requested)
    {
        var service = (ChatPlayerService)RuntimeHelpers.GetUninitializedObject(typeof(ChatPlayerService));
        Field(service, "_objects", Proxy.Of<IObjectTable>(m => m.Name == "GetEnumerator"
            ? ((IEnumerable<IGameObject>)players).GetEnumerator() : throw new InvalidOperationException(m.Name)));
        return typeof(ChatPlayerService).GetMethod("FindInObjectTable", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [requested]);
    }

    private static void Field(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    public class Proxy : DispatchProxy
    {
        private Func<MethodInfo, object?> _call = null!;
        public static T Of<T>(Func<MethodInfo, object?> call) where T : class
        {
            var proxy = Create<T, Proxy>();
            ((Proxy)(object)proxy)._call = call;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => _call(method!);
    }
}
