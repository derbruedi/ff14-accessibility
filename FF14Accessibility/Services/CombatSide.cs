using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

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
/// Spaeter (2026-10): freundliche Story-/FATE-/Wach-NPCs mit SubKind
/// <c>Combatant</c> landeten ebenfalls unter Gegner, weil nur Begleiter-SubKinds
/// und Gruppe/Allianz herausgenommen wurden. Die sichtbare Spiel-UI trennt das
/// ueber die Nameplate-Farbe (<see cref="CSGameObject.GetNamePlateColorType"/>).
///
/// JEDES SIGNAL HIER IST DAS DES SPIELS SELBST (ilspycmd / ClientStructs-Doku /
/// Kawari-Spawn-Flag):
/// <list type="bullet">
/// <item><see cref="IBattleNpc.BattleNpcKind"/> ist <c>GameObject.SubKind</c>
///   direkt aus dem Objekt-Struct. Pet(2), Buddy(3), Player(4), RaceChocobo(6),
///   LovmMinion(7) und NpcPartyMember(9) koennen STRUKTURELL kein Gegner sein -
///   das Spiel fuehrt sie als Begleiter.</item>
/// <item><see cref="ICharacter.StatusFlags"/> traegt PartyMember und
///   AllianceMember, von Dalamud aus <c>Character.IsPartyMember /
///   IsAllianceMember</c> gebaut. Wer in der Gruppe oder Allianz ist, steht per
///   Definition auf der eigenen Seite, egal welche SubKind er hat.</item>
/// <item><c>GetNamePlateColorType</c> liefert denselben Farbindex, den ein
///   sehender Spieler ueber dem Kopf sieht. Feind-Indizes 4-6 (PvP Lim/Gri/Uld),
///   7 (UnengagedEnemy, auch ungepulled), 9-11 (Engaged/Claimed/Unclaimed).
///   Freundliche Kampf-NPCs: 12 und 13 (<c>NamePlateColorNpc</c>).</item>
/// <item><c>StatusFlags.Hostile</c> (= <c>CharacterData.Flags</c> Bit 0) ist das
///   Spawn-Flag "hostile" und AENDERT die Nameplate - es bedeutet NICHT "gerade
///   aggro". Aggro ist Bit 1 (<c>InCombat</c>) bzw. Nameplate 9-11. Fuer die
///   Entscheidung reicht die Nameplate; Hostile bleibt nur in der ObjProbe zum
///   Abgleich sichtbar.</item>
/// </list>
/// </summary>
internal static class CombatSide
{
    /// <summary>
    /// SubKinds, die das Spiel selbst als Begleiter statt als Kaempfer fuehrt.
    /// <see cref="BattleNpcSubKind.Combatant"/> (5) und
    /// <see cref="BattleNpcSubKind.BNpcPart"/> (1, die einzeln anvisierbaren
    /// Gliedmassen grosser Bosse) fehlen mit Absicht: die werden ueber die
    /// Nameplate als Feind oder Freund sortiert, nicht pauschal als Begleiter.
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
    /// Nameplate-Farbindizes fuer Feinde (ClientStructs-Remarks an
    /// <c>GameObject.GetNamePlateColorType</c>; dieselbe Menge wie ECommons
    /// <c>ObjectFunctions.IsHostile</c>). Typ 7 = UnengagedEnemy bleibt drin,
    /// damit ungepulled Packs im Browser nicht verschwinden.
    /// </summary>
    private static readonly byte[] EnemyNamePlateTypes = { 4, 5, 6, 7, 9, 10, 11 };

    /// <summary>
    /// Nameplate-Farbindizes fuer freundliche Kampf-NPCs
    /// (<c>NamePlateEdgeNpc</c> / <c>NamePlateColorNpc</c>).
    /// </summary>
    private static readonly byte[] FriendlyNpcNamePlateTypes = { 12, 13 };

    /// <summary>
    /// True, wenn das Spiel positiv sagt, dass dieses Objekt auf der Seite des
    /// Spielers kaempft: Begleiter-SubKind, Gruppe/Allianz, oder Nameplate-Farbe
    /// eines freundlichen Kampf-NPCs. Nie geraten.
    /// </summary>
    internal static bool IsAlly(IGameObject obj)
    {
        if (obj is IBattleNpc npc && CompanionSubKinds.Contains(npc.BattleNpcKind))
            return true;

        if (IsGroupMember(obj))
            return true;

        return obj.ObjectKind == ObjectKind.BattleNpc
               && IsFriendlyNpcNamePlate(NamePlateColorType(obj));
    }

    /// <summary>
    /// True, wenn das Spiel diesen Charakter als Mitglied der EIGENEN Gruppe
    /// oder Allianz fuehrt - <see cref="StatusFlags.PartyMember"/> /
    /// <see cref="StatusFlags.AllianceMember"/>, aus Dalamud
    /// <c>ICharacter.StatusFlags</c>. Dieselbe Quelle, die
    /// <see cref="IsAlly"/> schon benutzt; als eigene Frage, weil der
    /// Objekt-Browser sie stellt ("wer hier ist mit mir unterwegs"). Kein
    /// Rueckschluss aus der Kategorie: die Kategorie Verbuendete enthaelt auch
    /// Begleiter und Trust-NPCs, die in keiner Gruppe stehen.
    /// </summary>
    internal static bool IsGroupMember(IGameObject obj)
    {
        if (obj is not ICharacter character) return false;

        var flags = character.StatusFlags;
        return flags.HasFlag(StatusFlags.PartyMember) || flags.HasFlag(StatusFlags.AllianceMember);
    }

    /// <summary>
    /// True fuer einen Kampf-NPC, dessen Nameplate das Spiel als Feind faerbt
    /// und der kein Verbuendeter ist. Ungepulled bleibt sichtbar (Typ 7).
    /// </summary>
    internal static bool IsEnemy(IGameObject obj)
        => obj.ObjectKind == ObjectKind.BattleNpc
           && !IsAlly(obj);

    /// <summary>True for the enemy nameplate indexes, including unpulled (7).</summary>
    internal static bool IsEnemyNamePlate(byte plate) => EnemyNamePlateTypes.Contains(plate);

    /// <summary>True for friendly battle-NPC nameplate indexes 12 and 13.</summary>
    internal static bool IsFriendlyNpcNamePlate(byte plate) => FriendlyNpcNamePlateTypes.Contains(plate);

    /// <summary>
    /// Nameplate-Farbindex aus dem Spiel-Struct, oder 0 wenn das Objekt keine
    /// gueltige Adresse hat. 0 ist kein Feind- und kein Freund-Npc-Index.
    /// </summary>
    internal static unsafe byte NamePlateColorType(IGameObject obj)
    {
        if (obj.Address == 0) return 0;
        return ((CSGameObject*)obj.Address)->GetNamePlateColorType();
    }
}
