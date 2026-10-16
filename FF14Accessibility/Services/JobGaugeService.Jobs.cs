using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;

namespace FF14Accessibility.Services;

/// <summary>
/// Per-job Collect/Announce methods for every Dalamud job gauge except the
/// Summoner and Samurai paths that stay in the main partial (they carry
/// debug probes). Every rising edge and on-demand line is level-gated on the
/// Action sheet of the linked spender/unlock — see docs/game-api.md.
///
/// Counters fire at gauge capacity (visual full). Boolean / flag fields fire
/// when they turn on. Nothing recomputes Action PrimaryCost thresholds.
/// </summary>
public sealed partial class JobGaugeService
{
    // ── Tanks ──────────────────────────────────────────────────────────

    private void CollectWarrior(byte level)
    {
        var g = _gauges.Get<WARGauge>();
        if (g == null) return;
        EdgeAtCap("war.beast", ActionInnerBeast, level, g.BeastGauge, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameBeast), GaugeReadyCueId.Beast);
    }

    private void AnnounceWarrior(byte level)
    {
        var g = _gauges.Get<WARGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionInnerBeast, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameBeast, g.BeastGauge));
        SpeakParts(parts);
    }

    private void CollectPaladin(byte level)
    {
        var g = _gauges.Get<PLDGauge>();
        if (g == null) return;
        EdgeAtCap("pld.oath", ActionSheltron, level, g.OathGauge, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameOath), GaugeReadyCueId.Oath);
    }

    private void AnnouncePaladin(byte level)
    {
        var g = _gauges.Get<PLDGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionSheltron, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameOath, g.OathGauge));
        SpeakParts(parts);
    }

    private void CollectDarkKnight(byte level)
    {
        var g = _gauges.Get<DRKGauge>();
        if (g == null) return;
        EdgeAtCap("drk.blood", ActionBloodspiller, level, g.Blood, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameBlood), GaugeReadyCueId.Blood);
        Edge("drk.darkarts", ActionBlackestNight, level, g.HasDarkArts,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameDarkArts), GaugeReadyCueId.DarkArts);
    }

    private void AnnounceDarkKnight(byte level)
    {
        var g = _gauges.Get<DRKGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionBloodspiller, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameBlood, g.Blood));
        if (Usable(ActionBlackestNight, level) && g.HasDarkArts)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameDarkArts));
        SpeakParts(parts);
    }

    private void CollectGunbreaker(byte level)
    {
        var g = _gauges.Get<GNBGauge>();
        if (g == null) return;
        Edge("gnb.ammo", ActionBurstStrike, level, g.Ammo > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameAmmo), GaugeReadyCueId.Ammo);
        EdgeAtCap("gnb.ammo.full", ActionBurstStrike, level, g.Ammo, CapAmmo,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameAmmo), GaugeReadyCueId.Ammo);
    }

    private void AnnounceGunbreaker(byte level)
    {
        var g = _gauges.Get<GNBGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionBurstStrike, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameAmmo, g.Ammo));
        SpeakParts(parts);
    }

    // ── Melee DPS ──────────────────────────────────────────────────────

    private void CollectMonk(byte level)
    {
        var g = _gauges.Get<MNKGauge>();
        if (g == null) return;
        EdgeAtCap("mnk.chakra", ActionSteelPeak, level, g.Chakra, CapChakra,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameChakra), GaugeReadyCueId.Chakra);

        var beasts = (g.BeastChakra1 != BeastChakra.None ? 1 : 0)
                   + (g.BeastChakra2 != BeastChakra.None ? 1 : 0)
                   + (g.BeastChakra3 != BeastChakra.None ? 1 : 0);
        Edge("mnk.beast.any", ActionPerfectBalance, level, beasts > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameBeastChakra), GaugeReadyCueId.BeastChakra);
        EdgeAtCap("mnk.beast.3", ActionMasterfulBlitz, level, beasts, CapStacks3,
            AccessibilityStrings.GaugeNameThreeBeastChakra, GaugeReadyCueId.ThreeBeastChakra);

        var nadi = g.Nadi;
        Edge("mnk.nadi.lunar", ActionMasterfulBlitz, level, (nadi & Nadi.Lunar) != 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameNadiLunar), GaugeReadyCueId.NadiLunar);
        Edge("mnk.nadi.solar", ActionMasterfulBlitz, level, (nadi & Nadi.Solar) != 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameNadiSolar), GaugeReadyCueId.NadiSolar);
        Edge("mnk.nadi.both", ActionPhantomRush, level,
            (nadi & Nadi.Lunar) != 0 && (nadi & Nadi.Solar) != 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameNadiBoth), GaugeReadyCueId.NadiBoth);
    }

    private void AnnounceMonk(byte level)
    {
        var g = _gauges.Get<MNKGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionSteelPeak, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameChakra, g.Chakra));
        var beasts = (g.BeastChakra1 != BeastChakra.None ? 1 : 0)
                   + (g.BeastChakra2 != BeastChakra.None ? 1 : 0)
                   + (g.BeastChakra3 != BeastChakra.None ? 1 : 0);
        if (Usable(ActionPerfectBalance, level) && beasts > 0)
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameBeastChakra, beasts));
        var nadi = g.Nadi;
        if (Usable(ActionMasterfulBlitz, level) && (nadi & Nadi.Lunar) != 0)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameNadiLunar));
        if (Usable(ActionMasterfulBlitz, level) && (nadi & Nadi.Solar) != 0)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameNadiSolar));
        SpeakParts(parts);
    }

    private void CollectDragoon(byte level)
    {
        var g = _gauges.Get<DRGGauge>();
        if (g == null) return;
        EdgeAtCap("drg.eyes", ActionGeirskogul, level, g.EyeCount, CapEyes,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameEyes), GaugeReadyCueId.Eyes);
        EdgeAtCap("drg.focus", ActionWyrmwindThrust, level, g.FirstmindsFocusCount, CapFirstminds,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameFirstminds), GaugeReadyCueId.Firstminds);
        Edge("drg.lotd", ActionGeirskogul, level, g.IsLOTDActive,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameLotd), GaugeReadyCueId.Lotd);
    }

    private void AnnounceDragoon(byte level)
    {
        var g = _gauges.Get<DRGGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionGeirskogul, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameEyes, g.EyeCount));
        if (Usable(ActionWyrmwindThrust, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameFirstminds, g.FirstmindsFocusCount));
        if (Usable(ActionGeirskogul, level) && g.IsLOTDActive)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameLotd));
        SpeakParts(parts);
    }

    private void CollectNinja(byte level)
    {
        var g = _gauges.Get<NINGauge>();
        if (g == null) return;
        EdgeAtCap("nin.ninki", ActionHellfrogMedium, level, g.Ninki, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameNinki), GaugeReadyCueId.Ninki);
        EdgeAtCap("nin.kazematoi", ActionArmorCrush, level, g.Kazematoi, CapKazematoi,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameKazematoi), GaugeReadyCueId.Kazematoi);
    }

    private void AnnounceNinja(byte level)
    {
        var g = _gauges.Get<NINGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionHellfrogMedium, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameNinki, g.Ninki));
        if (Usable(ActionArmorCrush, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameKazematoi, g.Kazematoi));
        SpeakParts(parts);
    }

    private void CollectReaper(byte level)
    {
        var g = _gauges.Get<RPRGauge>();
        if (g == null) return;
        EdgeAtCap("rpr.soul", ActionBloodStalk, level, g.Soul, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameSoul), GaugeReadyCueId.Soul);
        EdgeAtCap("rpr.shroud", ActionEnshroud, level, g.Shroud, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameShroud), GaugeReadyCueId.Shroud);
        Edge("rpr.enshroud", ActionEnshroud, level, g.EnshroudedTimeRemaining > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameEnshroud), GaugeReadyCueId.Enshroud);
        EdgeAtCap("rpr.void", ActionLemuresSlice, level, g.VoidShroud, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameVoidShroud), GaugeReadyCueId.VoidShroud);
    }

    private void AnnounceReaper(byte level)
    {
        var g = _gauges.Get<RPRGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionBloodStalk, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameSoul, g.Soul));
        if (Usable(ActionEnshroud, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameShroud, g.Shroud));
        if (Usable(ActionEnshroud, level) && g.EnshroudedTimeRemaining > 0)
        {
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameEnshroud));
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameLemureShroud, g.LemureShroud));
            if (Usable(ActionLemuresSlice, level))
                parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameVoidShroud, g.VoidShroud));
        }
        SpeakParts(parts);
    }

    private void CollectViper(byte level)
    {
        var g = _gauges.Get<VPRGauge>();
        if (g == null) return;
        Edge("vpr.coil", ActionUncoiledFury, level, g.RattlingCoilStacks > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameRattlingCoil), GaugeReadyCueId.RattlingCoil);
        EdgeAtCap("vpr.coil.full", ActionUncoiledFury, level, g.RattlingCoilStacks, CapRattling,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameRattlingCoil), GaugeReadyCueId.RattlingCoil);
        EdgeAtCap("vpr.offerings", ActionReawaken, level, g.SerpentOffering, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameSerpentOffering), GaugeReadyCueId.SerpentOffering);
        Edge("vpr.tail", ActionSerpentsTail, level, (byte)g.SerpentCombo != 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameSerpentFollowUp), GaugeReadyCueId.SerpentFollowUp);
    }

    private void AnnounceViper(byte level)
    {
        var g = _gauges.Get<VPRGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionUncoiledFury, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameRattlingCoil, g.RattlingCoilStacks));
        if (Usable(ActionReawaken, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameSerpentOffering, g.SerpentOffering));
        if (Usable(ActionSerpentsTail, level) && (byte)g.SerpentCombo != 0)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameSerpentFollowUp));
        SpeakParts(parts);
    }

    // ── Ranged physical ────────────────────────────────────────────────

    private void CollectBard(byte level)
    {
        var g = _gauges.Get<BRDGauge>();
        if (g == null) return;
        EdgeAtCap("brd.soulvoice", ActionApexArrow, level, g.SoulVoice, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameSoulVoice), GaugeReadyCueId.SoulVoice);
        EdgeAtCap("brd.repertoire", ActionPitchPerfect, level, g.Repertoire, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameRepertoire), GaugeReadyCueId.Repertoire);

        var coda = g.Coda;
        Edge("brd.coda.mage", ActionRadiantFinale, level, coda.Length > 0 && coda[0] != Song.None,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCodaMage), GaugeReadyCueId.CodaMage);
        Edge("brd.coda.army", ActionRadiantFinale, level, coda.Length > 1 && coda[1] != Song.None,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCodaArmy), GaugeReadyCueId.CodaArmy);
        Edge("brd.coda.wanderer", ActionRadiantFinale, level, coda.Length > 2 && coda[2] != Song.None,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCodaWanderer), GaugeReadyCueId.CodaWanderer);
    }

    private void AnnounceBard(byte level)
    {
        var g = _gauges.Get<BRDGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionApexArrow, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameSoulVoice, g.SoulVoice));
        if (Usable(ActionPitchPerfect, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameRepertoire, g.Repertoire));
        if (Usable(ActionRadiantFinale, level))
        {
            var coda = g.Coda;
            if (coda.Length > 0 && coda[0] != Song.None)
                parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCodaMage));
            if (coda.Length > 1 && coda[1] != Song.None)
                parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCodaArmy));
            if (coda.Length > 2 && coda[2] != Song.None)
                parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCodaWanderer));
        }
        SpeakParts(parts);
    }

    private void CollectMachinist(byte level)
    {
        var g = _gauges.Get<MCHGauge>();
        if (g == null) return;
        EdgeAtCap("mch.heat", ActionHypercharge, level, g.Heat, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameHeat), GaugeReadyCueId.Heat);
        EdgeAtCap("mch.battery", ActionRookAutoturret, level, g.Battery, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameBattery), GaugeReadyCueId.Battery);
        Edge("mch.overheat", ActionHypercharge, level, g.IsOverheated,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameOverheat), GaugeReadyCueId.Overheat);
        Edge("mch.robot", ActionRookAutoturret, level, g.IsRobotActive,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameRobot), GaugeReadyCueId.Robot);
    }

    private void AnnounceMachinist(byte level)
    {
        var g = _gauges.Get<MCHGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionHypercharge, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameHeat, g.Heat));
        if (Usable(ActionRookAutoturret, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameBattery, g.Battery));
        if (Usable(ActionHypercharge, level) && g.IsOverheated)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameOverheat));
        if (Usable(ActionRookAutoturret, level) && g.IsRobotActive)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameRobot));
        SpeakParts(parts);
    }

    private void CollectDancer(byte level)
    {
        var g = _gauges.Get<DNCGauge>();
        if (g == null) return;
        EdgeAtCap("dnc.feathers", ActionFanDance, level, g.Feathers, CapFeathers,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameFeathers), GaugeReadyCueId.Feathers);
        EdgeAtCap("dnc.esprit", ActionSaberDance, level, g.Esprit, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameEsprit), GaugeReadyCueId.Esprit);
    }

    private void AnnounceDancer(byte level)
    {
        var g = _gauges.Get<DNCGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionFanDance, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameFeathers, g.Feathers));
        if (Usable(ActionSaberDance, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameEsprit, g.Esprit));
        SpeakParts(parts);
    }

    // ── Casters ────────────────────────────────────────────────────────

    private void CollectBlackMage(byte level)
    {
        var g = _gauges.Get<BLMGauge>();
        if (g == null) return;
        Edge("blm.polyglot", ActionFoul, level, g.PolyglotStacks > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNamePolyglot), GaugeReadyCueId.Polyglot);
        EdgeAtCap("blm.polyglot.full", ActionFoul, level, g.PolyglotStacks, CapPolyglot,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNamePolyglot), GaugeReadyCueId.Polyglot);
        Edge("blm.paradox", ActionParadox, level, g.IsParadoxActive,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameParadox), GaugeReadyCueId.Paradox);
        EdgeAtCap("blm.astralsoul", ActionFlareStar, level, g.AstralSoulStacks, CapAstralSoul,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameAstralSoul), GaugeReadyCueId.AstralSoul);
        EdgeAtCap("blm.hearts", ActionBlizzardIv, level, g.UmbralHearts, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameUmbralHearts), GaugeReadyCueId.UmbralHearts);
    }

    private void AnnounceBlackMage(byte level)
    {
        var g = _gauges.Get<BLMGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionFoul, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNamePolyglot, g.PolyglotStacks));
        if (Usable(ActionBlizzardIv, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameUmbralHearts, g.UmbralHearts));
        if (Usable(ActionFlareStar, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameAstralSoul, g.AstralSoulStacks));
        if (Usable(ActionParadox, level) && g.IsParadoxActive)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameParadox));
        if (g.InAstralFire)
            parts.Add(AccessibilityStrings.GaugeAmount("Astral", g.AstralFireStacks));
        if (g.InUmbralIce)
            parts.Add(AccessibilityStrings.GaugeAmount("Umbral", g.UmbralIceStacks));
        SpeakParts(parts);
    }

    private void CollectRedMage(byte level)
    {
        var g = _gauges.Get<RDMGauge>();
        if (g == null) return;
        EdgeAtCap("rdm.white", ActionEnchantedRiposte, level, g.WhiteMana, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameWhiteMana), GaugeReadyCueId.WhiteMana);
        EdgeAtCap("rdm.black", ActionEnchantedRiposte, level, g.BlackMana, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameBlackMana), GaugeReadyCueId.BlackMana);
        EdgeAtCap("rdm.stacks", ActionVerflare, level, g.ManaStacks, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameManaStacks), GaugeReadyCueId.ManaStacks);
    }

    private void AnnounceRedMage(byte level)
    {
        var g = _gauges.Get<RDMGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionEnchantedRiposte, level))
        {
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameWhiteMana, g.WhiteMana));
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameBlackMana, g.BlackMana));
        }
        if (Usable(ActionVerflare, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameManaStacks, g.ManaStacks));
        SpeakParts(parts);
    }

    private void CollectPictomancer(byte level)
    {
        var g = _gauges.Get<PCTGauge>();
        if (g == null) return;
        EdgeAtCap("pct.palette", ActionSubtractivePalette, level, g.PalleteGauge, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNamePalette), GaugeReadyCueId.Palette);
        EdgeAtCap("pct.paint", ActionHolyInWhite, level, g.Paint, CapPaint,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNamePaint), GaugeReadyCueId.Paint);
        Edge("pct.motif.creature", ActionLivingMuse, level, g.CreatureMotifDrawn,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCreatureMotif), GaugeReadyCueId.CreatureMotif);
        Edge("pct.motif.weapon", ActionSteelMuse, level, g.WeaponMotifDrawn,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameWeaponMotif), GaugeReadyCueId.WeaponMotif);
        Edge("pct.motif.landscape", ActionScenicMuse, level, g.LandscapeMotifDrawn,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameLandscapeMotif), GaugeReadyCueId.LandscapeMotif);
        Edge("pct.portrait.moogle", ActionMogOfTheAges, level, g.MooglePortraitReady,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameMooglePortrait), GaugeReadyCueId.MooglePortrait);
        Edge("pct.portrait.madeen", ActionRetributionMadeen, level, g.MadeenPortraitReady,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameMadeenPortrait), GaugeReadyCueId.MadeenPortrait);
    }

    private void AnnouncePictomancer(byte level)
    {
        var g = _gauges.Get<PCTGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionSubtractivePalette, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNamePalette, g.PalleteGauge));
        if (Usable(ActionHolyInWhite, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNamePaint, g.Paint));
        if (Usable(ActionLivingMuse, level) && g.CreatureMotifDrawn)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCreatureMotif));
        if (Usable(ActionSteelMuse, level) && g.WeaponMotifDrawn)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameWeaponMotif));
        if (Usable(ActionScenicMuse, level) && g.LandscapeMotifDrawn)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameLandscapeMotif));
        if (Usable(ActionMogOfTheAges, level) && g.MooglePortraitReady)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameMooglePortrait));
        if (Usable(ActionRetributionMadeen, level) && g.MadeenPortraitReady)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameMadeenPortrait));
        SpeakParts(parts);
    }

    // ── Healers ────────────────────────────────────────────────────────

    private void CollectWhiteMage(byte level)
    {
        var g = _gauges.Get<WHMGauge>();
        if (g == null) return;
        Edge("whm.lily", ActionAfflatusSolace, level, g.Lily > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameLily), GaugeReadyCueId.Lily);
        EdgeAtCap("whm.lily.full", ActionAfflatusSolace, level, g.Lily, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameLily), GaugeReadyCueId.Lily);
        EdgeAtCap("whm.bloodlily", ActionAfflatusMisery, level, g.BloodLily, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameBloodLily), GaugeReadyCueId.BloodLily);
    }

    private void AnnounceWhiteMage(byte level)
    {
        var g = _gauges.Get<WHMGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionAfflatusSolace, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameLily, g.Lily));
        if (Usable(ActionAfflatusMisery, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameBloodLily, g.BloodLily));
        SpeakParts(parts);
    }

    private void CollectScholar(byte level)
    {
        var g = _gauges.Get<SCHGauge>();
        if (g == null) return;
        Edge("sch.aetherflow", ActionLustrate, level, g.Aetherflow > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameAetherflow), GaugeReadyCueId.Aetherflow);
        EdgeAtCap("sch.fairy", ActionAetherpact, level, g.FairyGauge, Cap100,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameFairy), GaugeReadyCueId.Fairy);
    }

    private void AnnounceScholar(byte level)
    {
        var g = _gauges.Get<SCHGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionLustrate, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameAetherflow, g.Aetherflow));
        if (Usable(ActionAetherpact, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameFairy, g.FairyGauge));
        SpeakParts(parts);
    }

    private void CollectSage(byte level)
    {
        var g = _gauges.Get<SGEGauge>();
        if (g == null) return;
        Edge("sge.addersgall", ActionDruochole, level, g.Addersgall > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameAddersgall), GaugeReadyCueId.Addersgall);
        EdgeAtCap("sge.addersgall.full", ActionDruochole, level, g.Addersgall, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameAddersgall), GaugeReadyCueId.Addersgall);
        Edge("sge.addersting", ActionToxikon, level, g.Addersting > 0,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameAddersting), GaugeReadyCueId.Addersting);
        EdgeAtCap("sge.addersting.full", ActionToxikon, level, g.Addersting, CapStacks3,
            AccessibilityStrings.GaugeFull(AccessibilityStrings.GaugeNameAddersting), GaugeReadyCueId.Addersting);
        Edge("sge.eukrasia", ActionEukrasia, level, g.Eukrasia,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameEukrasia), GaugeReadyCueId.Eukrasia);
    }

    private void AnnounceSage(byte level)
    {
        var g = _gauges.Get<SGEGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionDruochole, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameAddersgall, g.Addersgall));
        if (Usable(ActionToxikon, level))
            parts.Add(AccessibilityStrings.GaugeAmount(AccessibilityStrings.GaugeNameAddersting, g.Addersting));
        if (Usable(ActionEukrasia, level) && g.Eukrasia)
            parts.Add(AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameEukrasia));
        SpeakParts(parts);
    }

    private void CollectAstrologian(byte level)
    {
        var g = _gauges.Get<ASTGauge>();
        if (g == null) return;
        var hasCard = g.Card1 != CardType.None || g.Card2 != CardType.None || g.Card3 != CardType.None;
        Edge("ast.card", ActionAstralDraw, level, hasCard,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCard), GaugeReadyCueId.Card);
        Edge("ast.crown", ActionMinorArcana, level, g.DrawnCrownCard != CardType.None,
            AccessibilityStrings.GaugeReady(AccessibilityStrings.GaugeNameCrownCard), GaugeReadyCueId.CrownCard);
    }

    private void AnnounceAstrologian(byte level)
    {
        var g = _gauges.Get<ASTGauge>();
        if (g == null) { _tolk.Speak(AccessibilityStrings.GaugeNoneForJob); return; }
        var parts = new List<string>();
        if (Usable(ActionAstralDraw, level))
        {
            foreach (var card in new[] { g.Card1, g.Card2, g.Card3 })
            {
                if (card == CardType.None) continue;
                parts.Add(AccessibilityStrings.GaugeReady(CardName(card)));
            }
        }
        if (Usable(ActionMinorArcana, level) && g.DrawnCrownCard != CardType.None)
            parts.Add(AccessibilityStrings.GaugeReady(
                AccessibilityStrings.GaugeNameCrownCard + " " + CardName(g.DrawnCrownCard)));
        SpeakParts(parts);
    }

    private static string CardName(CardType card) => card switch
    {
        CardType.Balance => AccessibilityStrings.L("Waage", "Balance", "Весы"),
        CardType.Bole    => AccessibilityStrings.L("Baum", "Bole", "Древо"),
        CardType.Arrow   => AccessibilityStrings.L("Pfeil", "Arrow", "Стрела"),
        CardType.Spear   => AccessibilityStrings.L("Speer", "Spear", "Копьё"),
        CardType.Ewer    => AccessibilityStrings.L("Krug", "Ewer", "Кувшин"),
        CardType.Spire   => AccessibilityStrings.L("Turm", "Spire", "Шпиль"),
        CardType.Lord    => AccessibilityStrings.L("Lord", "Lord", "Лорд"),
        CardType.Lady    => AccessibilityStrings.L("Lady", "Lady", "Леди"),
        _                => card.ToString(),
    };
}
