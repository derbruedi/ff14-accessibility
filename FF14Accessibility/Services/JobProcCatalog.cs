namespace FF14Accessibility.Services;

/// <summary>A player status that enables a special action. IDs are from the
/// installed Status sheet, not matched by translated text or enemy statuses.</summary>
public sealed record JobProcDefinition(uint StatusId, string EnglishName, byte JobId, byte BaseClassId = 0)
{
    public bool AppliesTo(uint jobId) => jobId == JobId || (BaseClassId != 0 && jobId == BaseClassId);
    public bool MatchesSource(string name) => string.Equals(EnglishName, name, StringComparison.Ordinal);
}

public static class JobProcCatalog
{
    // PvE ready/instant-cast effects. Resource gauges already have their own
    // reader; ordinary damage buffs, debuffs and combo potency buffs do not
    // belong here. The English name is checked against the live data once.
    public static IReadOnlyList<JobProcDefinition> All { get; } = Array.AsReadOnly<JobProcDefinition>([
        // Paladin / Gladiator
        new(1902, "Atonement Ready", 19, 1),
        new(2673, "Divine Might", 19, 1),
        new(3019, "Confiteor Ready", 19, 1),
        new(3827, "Supplication Ready", 19, 1),
        new(3828, "Sepulchre Ready", 19, 1),
        new(3831, "Blade of Honor Ready", 19, 1),
        new(3847, "Goring Blade Ready", 19, 1),
        // Warrior / Marauder
        new(1897, "Nascent Chaos", 21, 3),
        new(2624, "Primal Rend Ready", 21, 3),
        new(3834, "Primal Ruination Ready", 21, 3),
        new(3901, "Wrathful", 21, 3),
        // Dark Knight
        new(1972, "Delirium", 32),
        new(3836, "Delirium", 32),
        new(3837, "Scorn", 32),
        // Gunbreaker
        new(1842, "Ready to Rip", 37),
        new(1843, "Ready to Tear", 37),
        new(1844, "Ready to Gouge", 37),
        new(2686, "Ready to Blast", 37),
        new(3839, "Ready to Raze", 37),
        new(3840, "Ready to Reign", 37),
        new(3886, "Ready to Break", 37),
        // Monk / Pugilist
        new(3841, "Earth's Rumination", 20, 2),
        new(3842, "Wind's Rumination", 20, 2),
        new(3843, "Fire's Rumination", 20, 2),
        // Dragoon / Lancer
        new(802, "Fang and Claw Bared", 22, 4),
        new(803, "Wheel in Motion", 22, 4),
        new(1243, "Dive Ready", 22, 4),
        new(1863, "Draconian Fire", 22, 4),
        new(1870, "Enhanced Piercing Talon", 22, 4),
        new(3844, "Nastrond Ready", 22, 4),
        new(3845, "Dragon's Flight", 22, 4),
        new(3846, "Starcross Ready", 22, 4),
        // Ninja / Rogue
        new(497, "Kassatsu", 30, 29),
        new(1186, "Ten Chi Jin", 30, 29),
        new(1955, "Assassinate Ready", 30, 29),
        new(2690, "Raiju Ready", 30, 29),
        new(2691, "Fleeting Raiju Ready", 30, 29),
        new(2723, "Phantom Kamaitachi Ready", 30, 29),
        new(3851, "Tenri Jindo Ready", 30, 29),
        new(3848, "Shadow Walker", 30, 29),
        // Samurai: Tsubame-gaeshi is already announced from SAMGauge.Kaeshi.
        new(1236, "Enhanced Enpi", 34),
        new(2959, "Ogi Namikiri Ready", 34),
        new(3855, "Zanshin Ready", 34),
        // Reaper
        new(2587, "Soul Reaver", 39),
        new(2592, "Immortal Sacrifice", 39),
        new(2594, "Soulsow", 39),
        new(2845, "Enhanced Harpe", 39),
        new(3857, "Oblatio", 39),
        new(3858, "Executioner", 39),
        new(3860, "Perfectio Parata", 39),
        new(3905, "Ideal Host", 39),
        // Viper: Serpent's Tail follow-ups are already read from VPRGauge.
        new(3671, "Ready to Reawaken", 41),
        // Bard / Archer
        new(122, "Straight Shot Ready", 23, 5),
        new(128, "Barrage", 23, 5),
        new(2692, "Blast Arrow Ready", 23, 5),
        new(3002, "Shadowbite Ready", 23, 5),
        new(3861, "Hawk's Eye", 23, 5),
        new(3862, "Resonant Arrow Ready", 23, 5),
        new(3863, "Radiant Encore Ready", 23, 5),
        // Machinist
        new(3864, "Hypercharged", 31),
        new(3865, "Excavator Ready", 31),
        new(3866, "Full Metal Machinist", 31),
        // Dancer
        new(1814, "Flourishing Cascade", 38),
        new(1815, "Flourishing Fountain", 38),
        new(1816, "Flourishing Windmill", 38),
        new(1817, "Flourishing Shower", 38),
        new(1820, "Threefold Fan Dance", 38),
        new(2693, "Silken Symmetry", 38),
        new(2694, "Silken Flow", 38),
        new(2698, "Flourishing Finish", 38),
        new(2699, "Fourfold Fan Dance", 38),
        new(2700, "Flourishing Starfall", 38),
        new(3017, "Flourishing Symmetry", 38),
        new(3018, "Flourishing Flow", 38),
        new(3867, "Last Dance Ready", 38),
        new(3868, "Finishing Move Ready", 38),
        new(3869, "Dance of the Dawn Ready", 38),
        // Black Mage / Thaumaturge
        new(164, "Thundercloud", 25, 7),
        new(165, "Firestarter", 25, 7),
        new(3870, "Thunderhead", 25, 7),
        // Summoner / Arcanist
        new(1212, "Further Ruin", 27, 26),
        new(2701, "Further Ruin", 27, 26),
        new(2724, "Ifrit's Favor", 27, 26),
        new(2725, "Garuda's Favor", 27, 26),
        new(2853, "Titan's Favor", 27, 26),
        new(3873, "Ruby's Glimmer", 27, 26),
        new(3874, "Refulgent Lux", 27, 26),
        new(4400, "Crimson Strike Ready", 27, 26),
        // Red Mage
        new(1234, "Verfire Ready", 35),
        new(1235, "Verstone Ready", 35),
        new(1249, "Dualcast", 35),
        new(3875, "Magicked Swordplay", 35),
        new(3876, "Thorned Flourish", 35),
        new(3877, "Grand Impact Ready", 35),
        new(3878, "Prefulgence Ready", 35),
        // Pictomancer
        new(3679, "Rainbow Bright", 42),
        new(3680, "Hammer Time", 42),
        new(3681, "Starstruck", 42),
        new(3690, "Subtractive Spectrum", 42),
        new(3691, "Monochrome Tones", 42),
        // White Mage / Conjurer
        new(155, "Freecure", 24, 6),
        new(3879, "Sacred Sight", 24, 6),
        new(3881, "Divine Grace", 24, 6),
        // Scholar
        new(3882, "Impact Imminent", 28),
        // Astrologian
        new(815, "Enhanced Benefic II", 33),
        new(3893, "Divining", 33),
        new(3895, "Suntouched", 33),
        // Sage's Eukrasia and Addersting are already read from SGEGauge.
        // Blue Mage
        new(2494, "Touch of Frost", 36),
        new(2497, "Auspicious Trance", 36),
    ]);

    private static readonly Dictionary<uint, JobProcDefinition> ById = All.ToDictionary(x => x.StatusId);

    public static JobProcDefinition? Find(uint statusId, uint jobId) =>
        ById.TryGetValue(statusId, out var entry) && entry.AppliesTo(jobId) ? entry : null;
}
