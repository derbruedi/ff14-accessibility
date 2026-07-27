using System.Collections.Generic;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TripleTriadCard = FFXIVClientStructs.FFXIV.Client.UI.AddonTripleTriad.TripleTriadCard;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads the Triple Triad card game (AddonTripleTriad) for a blind player:
/// the 3x3 board and the player's own hand, each on a hotkey.
///
/// Source of truth: AddonTripleTriad, ilspycmd-verified against
/// FFXIVClientStructs.dll (2026-07-26). The card arrays are declared
/// <c>internal</c> and the struct carries <c>[GenerateInterop(false)]</c>, so
/// there are no public Span accessors — the cards are read by pointer arithmetic
/// at the verified field offsets instead (see the *Offset constants below).
/// </summary>
public sealed class TripleTriadService
{
    private readonly IGameGui   _gameGui;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    // Verified field offsets inside AddonTripleTriad (ilspycmd 2026-07-26).
    // Each deck/board is a FixedSizeArray of TripleTriadCard laid out inline;
    // the three offsets line up exactly at stride sizeof(TripleTriadCard)=168
    // (576 + 5*168 = 1416; 1416 + 5*168 = 2256), which cross-checks the stride.
    private const int BlueDeckOffset = 576;   // FixedSizeArray5 — the player's hand
    private const int RedDeckOffset  = 1416;  // FixedSizeArray5 — opponent's hand (unused here)
    private const int BoardOffset    = 2256;  // FixedSizeArray9 — the 3x3 board, row-major

    public TripleTriadService(IGameGui gameGui, TolkService tolk, IPluginLog log)
    {
        _gameGui = gameGui;
        _tolk    = tolk;
        _log     = log;
    }

    /// <summary>Reads the whole 3x3 board: card counts, whose turn it is, then
    /// every cell (1-9, row-major) as empty or owner + edge numbers.</summary>
    public unsafe void ReadBoard()
    {
        var addon = GetAddon();
        if (addon == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.CardGameNotOpen);
            return;
        }

        var parts = new List<string>(9);
        var yours = 0;
        var enemy = 0;
        for (var i = 0; i < 9; i++)
        {
            var card = CardAt(addon, BoardOffset, i);
            var cell = i + 1;
            if (!card->HasCard)
            {
                parts.Add(AccessibilityStrings.BoardCellEmpty(cell));
                continue;
            }

            var isYours = card->CardOwner == CardOwner.Blue;
            if (isYours) yours++; else enemy++;
            var owner = isYours ? AccessibilityStrings.CardOwnerYours : AccessibilityStrings.CardOwnerEnemy;
            parts.Add(AccessibilityStrings.BoardCellCard(cell, owner, Sides(card)));
        }

        var sb = new StringBuilder();
        sb.Append(AccessibilityStrings.BoardIntro(yours, enemy));
        sb.Append(' ').Append(TurnPhrase(addon->TurnState));
        sb.Append(' ').Append(string.Join(". ", parts)).Append('.');
        _tolk.SpeakInterrupt(sb.ToString());
    }

    /// <summary>Reads the player's own hand (BlueDeck). Cards are announced by
    /// their fixed slot number (1-5) so the reference stays stable as cards are
    /// played out — played slots are skipped, not renumbered.</summary>
    public unsafe void ReadHand()
    {
        var addon = GetAddon();
        if (addon == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.CardGameNotOpen);
            return;
        }

        var parts = new List<string>(5);
        for (var i = 0; i < 5; i++)
        {
            var card = CardAt(addon, BlueDeckOffset, i);
            if (!card->HasCard) continue;          // slot already played
            parts.Add(AccessibilityStrings.HandCard(i + 1, Sides(card)));
        }

        if (parts.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HandEmpty);
            return;
        }

        _tolk.SpeakInterrupt(
            AccessibilityStrings.HandIntro(parts.Count) + " " + string.Join(". ", parts) + ".");
    }

    /// <summary>The four edge numbers in clockwise-from-top order, with the
    /// game's 1-9 / "A" (=10) convention applied.</summary>
    private static unsafe string Sides(TripleTriadCard* card) =>
        AccessibilityStrings.CardSides(
            Num(card->NumSideU), Num(card->NumSideR), Num(card->NumSideD), Num(card->NumSideL));

    /// <summary>Card edge values run 1-10; the game shows 10 as "A".</summary>
    private static string Num(byte value) => value >= 10 ? "A" : value.ToString();

    // HYPOTHESE (in-game zu verifizieren): Waiting = nicht am Zug, Normal/MaskedMove
    // = du bist am Zug. Der Rohwert wird geloggt, damit die Zuordnung anhand echter
    // Spielsituationen bestaetigt werden kann.
    private string TurnPhrase(TurnState state)
    {
        _log.Info($"[TripleTriad] TurnState={(int)state} ({state})");
        return state == TurnState.Waiting
            ? AccessibilityStrings.WaitingTurn
            : AccessibilityStrings.YourTurn;
    }

    /// <summary>Pointer to card <paramref name="index"/> inside the inline array
    /// that starts at <paramref name="byteOffset"/> in the addon.</summary>
    private static unsafe TripleTriadCard* CardAt(AddonTripleTriad* addon, int byteOffset, int index) =>
        (TripleTriadCard*)((byte*)addon + byteOffset + index * sizeof(TripleTriadCard));

    /// <summary>The open, visible TripleTriad addon, or null when the card game
    /// is not on screen.</summary>
    private unsafe AddonTripleTriad* GetAddon()
    {
        var handle = _gameGui.GetAddonByName("TripleTriad");
        if (handle.IsNull) return null;
        var addon = (AddonTripleTriad*)(nint)handle;
        if (addon == null || !addon->AtkUnitBase.IsVisible) return null;
        return addon;
    }
}
