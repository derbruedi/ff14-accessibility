using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace FF14Accessibility.Services;

/// <summary>
/// Auf WELCHER SEITE ein Kampfteilnehmer steht, damit der Objekt-Browser
/// "was mich gleich umbringt" von "was fuer mich kaempft" trennen kann.
///
/// User-Meldung aus einem Dungeon: *"everything in combat drops into the enemies
/// category"* - und genau so war es, denn die Kategorie Gegner ist nichts weiter
/// als <c>ObjectKind.BattleNpc</c>, und das ist im Spiel auch der Trust-Trupp, der
/// Duty-Support-NPC, der Karfunkel, die Fee und das Begleitchocobo.
///
/// JEDES SIGNAL HIER IST DAS DES SPIELS SELBST (ilspycmd-geprueft gegen das
/// installierte Dalamud / FFXIVClientStructs):
/// <list type="bullet">
/// <item><see cref="IBattleNpc.BattleNpcKind"/> ist <c>GameObject.SubKind</c>
///   direkt aus dem Objekt-Struct. Pet(2), Buddy(3), Player(4), RaceChocobo(6),
///   LovmMinion(7) und NpcPartyMember(9) koennen STRUKTURELL kein Gegner sein -
///   das Spiel fuehrt sie als Begleiter.</item>
/// <item><see cref="ICharacter.StatusFlags"/> traegt PartyMember und
///   AllianceMember, von Dalamud aus <c>Character.IsPartyMember /
///   IsAllianceMember</c> gebaut. Wer in der Gruppe oder Allianz ist, steht per
///   Definition auf der eigenen Seite, egal welche SubKind er hat.</item>
/// </list>
///
/// WAS HIER BEWUSST NICHT BENUTZT WIRD, UND WARUM. <c>StatusFlags.Hostile</c>
/// (= <c>CharacterData.Flags</c> Bit 0, <c>IsHostile</c>) sieht nach der exakten
/// Antwort aus und ist trotzdem NICHT der Filter: die Quelle belegt nur, dass das
/// Bit existiert - nicht, ob das Spiel es auch auf einem Mob setzt, der noch nicht
/// aggro hat. Heisst es "kaempft gerade gegen mich", wuerde ein Filter darauf jede
/// noch nicht gepullte Gruppe im Dungeon verstecken, und ein blinder Spieler kann
/// nicht sehen, dass die Liste kuerzer geworden ist.
///
/// Der Filter unten kann deshalb immer nur in EINE Richtung wirken: er nimmt etwas
/// aus Gegner heraus, wenn das Spiel es selbst als Begleiter oder Gruppenmitglied
/// fuehrt. Ein Mob, ueber den das Spiel nichts sagt, bleibt Gegner. Etwas kann also
/// nicht aus der Liste fallen.
/// </summary>
internal static class CombatSide
{
    /// <summary>
    /// SubKinds, die das Spiel selbst als Begleiter statt als Kaempfer fuehrt.
    /// <see cref="BattleNpcSubKind.Combatant"/> (5) und
    /// <see cref="BattleNpcSubKind.BNpcPart"/> (1, die einzeln anvisierbaren
    /// Gliedmassen grosser Bosse) fehlen mit Absicht: das SIND Gegner.
    /// </summary>
    private static readonly BattleNpcSubKind[] CompanionSubKinds =
    {
        BattleNpcSubKind.Pet,
        BattleNpcSubKind.Buddy,
        BattleNpcSubKind.Player,
        BattleNpcSubKind.RaceChocobo,
        BattleNpcSubKind.LovmMinion,
        BattleNpcSubKind.NpcPartyMember,
    };

    /// <summary>
    /// True, wenn das Spiel positiv sagt, dass dieses Objekt auf der Seite des
    /// Spielers kaempft: eine Begleiter-SubKind, oder Gruppe/Allianz. Nie geraten -
    /// ein Objekt, ueber das das Spiel nichts sagt, ist kein Verbuendeter.
    /// </summary>
    internal static bool IsAlly(IGameObject obj)
    {
        if (obj is IBattleNpc npc && CompanionSubKinds.Contains(npc.BattleNpcKind))
            return true;

        if (obj is ICharacter character)
        {
            var flags = character.StatusFlags;
            if (flags.HasFlag(StatusFlags.PartyMember) || flags.HasFlag(StatusFlags.AllianceMember))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True fuer einen Kampf-NPC, der kein Verbuendeter ist. Was hier NICHT
    /// verlangt wird: das Hostile-Flag. Ein friedlicher Mob im Nebenraum muss
    /// auffindbar bleiben - siehe die Anmerkungen an der Klasse.
    /// </summary>
    internal static bool IsEnemy(IGameObject obj)
        => obj.ObjectKind == ObjectKind.BattleNpc && !IsAlly(obj);
}
