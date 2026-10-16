using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Was ein anderer SPIELER ist: Klasse (Job), Stufe und ob das Spiel ihn als
/// Mitglied der eigenen Gruppe fuehrt.
///
/// <para>
/// Jede Angabe kommt aus dem Charakter-Objekt des Spiels selbst
/// (<c>FFXIVClientStructs.FFXIV.Client.Game.Character.Character.ClassJob</c>
/// und <c>.Level</c>, am installierten FFXIVClientStructs per
/// Metadata-Peek bestaetigt). Nichts wird aus dem Namen, der Ausruestung oder
/// der Kategorie zurueckgerechnet. Der Klassenname kommt aus dem Blatt
/// <c>ClassJob</c>, also in der Sprache, die der Client selbst benutzt - keine
/// eigene Uebersetzungstabelle, die bei einem Patch still veralten wuerde.
/// </para>
///
/// <para>
/// EHRLICH BLEIBEN, WENN DAS SPIEL SCHWEIGT: meldet das Objekt die Klasse als 0
/// (bei fremden Spielern ist das Nachladen der Objektdaten nicht
/// selbstverstaendlich), gibt es keinen Klassennamen - der Aufrufer nennt dann
/// nur den Namen. Eine geratene Klasse, oder eine aus dem Kategorie-Wort
/// ("Spieler") abgeleitete, waere eine Erfindung ueber einen echten Menschen.
/// Aus demselben Grund wird die Stufe nur genannt, wenn das Spiel eine meldet.
/// </para>
///
/// <para>
/// Die Gruppenzugehoerigkeit steht bewusst NICHT hier, sondern in
/// <see cref="CombatSide.IsGroupMember"/> - dort liegt schon die eine Stelle,
/// die das Spiel nach "Gruppe oder Allianz" fragt.
/// </para>
/// </summary>
internal static class PlayerInfo
{
    /// <summary>True fuer Objekte, die das Spiel selbst als Spieler fuehrt.</summary>
    internal static bool IsPlayer(IGameObject obj) => obj.ObjectKind == ObjectKind.Pc;

    /// <summary>
    /// Der Klassenname des Spielers, oder leer, wenn das Spiel keine Klasse
    /// meldet. Nie ein Ersatzwort - der Aufrufer entscheidet, was er dann sagt.
    /// </summary>
    internal static string JobName(IDataManager data, IGameObject obj)
    {
        var jobId = JobId(obj);
        if (jobId == 0) return string.Empty;

        if (!data.GetExcelSheet<ClassJob>().TryGetRow(jobId, out var job)) return string.Empty;

        return RussianGameText.Name(data, job, x => x.Name).Trim();
    }

    /// <summary>
    /// Die Stufe des Spielers, oder 0, wenn das Spiel keine meldet. Die 0 ist
    /// eine Aussage ("nicht bekannt"), keine Stufe 1 - deshalb prueft der
    /// Aufrufer auf &gt; 0, bevor er sie ausspricht.
    /// </summary>
    internal static unsafe int Level(IGameObject obj)
    {
        var chara = AsCharacter(obj);
        return chara == null ? 0 : chara->Level;
    }

    /// <summary>
    /// Der Rohwert der Klasse, fuer Log und Fehlersuche. Was das Spiel nicht
    /// meldet, ist hier 0 - dieselbe Zahl, die <see cref="JobName"/> schweigen
    /// laesst.
    /// </summary>
    internal static unsafe byte JobId(IGameObject obj)
    {
        var chara = AsCharacter(obj);
        return chara == null ? (byte)0 : chara->ClassJob;
    }

    /// <summary>
    /// Der Rohwert der Heimatwelt, oder 0 wenn unbekannt. Zum Abgleich mit dem
    /// WorldId aus dem Chat-<c>PlayerPayload</c>.
    /// </summary>
    internal static unsafe ushort HomeWorldId(IGameObject obj)
    {
        var chara = AsCharacter(obj);
        return chara == null ? (ushort)0 : chara->HomeWorld;
    }

    /// <summary>
    /// Das Charakter-Objekt hinter einem Spieler-Objekt, oder null. Der Cast auf
    /// die Client-Struktur ist derselbe, den UIReaderService fuer die
    /// Charaktererschaffung benutzt; nur Objekte, die das Spiel als Spieler
    /// fuehrt, gehen hinein - ein NPC hat an dieser Stelle keine Klasse zu
    /// melden, die den Spieler interessiert.
    /// </summary>
    private static unsafe FFXIVClientStructs.FFXIV.Client.Game.Character.Character* AsCharacter(IGameObject obj)
    {
        if (!IsPlayer(obj) || obj.Address == nint.Zero) return null;

        return (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)obj.Address;
    }
}
