# Accessibility audit and improvement suggestions

## Introduction and appreciation of the current state

FF14 Accessibility (as of V4.61) is an unusually well developed Dalamud plugin. It already covers a large part of everyday play: character creation (partly), login and lobby, movement and target acquisition through an object browser with categories, auto-walk via vnavmesh, a walk guide with a sound beacon, NPC dialogues, quest acceptance and tracking including objective sentences, simple solo combat with HP/MP and cast announcements, inventory and gil, an action bar, the bestiary (hunting log) including habitat announcement and monster tracking, an emote browser, and an automatic key conflict check against the game's own bindings.

The quality of the development itself is remarkable: nearly every behavioural decision in the code is commented with a date, a user feedback reference or log evidence. That shows an iterative, user-driven approach that is very solid for a one-person accessibility project.

At the same time, the analysis of all 16 services shows clear gaps in group play (party HP, aggro, AoE areas), in the economy/social functions (market board, trading, retainers, mail, duty finder, party finder), in crafting, plus some technical legacy issues (hard coding, sources of announcement spam, missing configurability through a settings interface). The suggestions below are grouped by topic; a top-5 prioritisation comes at the end.

A fundamental realism constraint up front: a Dalamud plugin has no access to the game's audio engine and no way to change the game client graphically. Everything suggested here has to be implementable through read access to game data (ObjectTable, UI node trees, Lumina sheets, native game structures via reflection) plus our own TTS output (Tolk) and our own sound files (NAudio, like the existing beacon system).

## Combat and encounters

### Party HP through panned earcons

What: for every visible party member (`IPartyList`), play a short tone panned left/right whose pitch or timbre encodes the HP state in 20-percent steps, similar to the existing beacon principle in `BeaconService.cs`. Optionally add a key for the textual announcement "Party: Alice 80 percent, Bob 45 percent, ...".
Why it matters: currently `CombatService.AnnounceStatus()` reads only `LocalPlayer` and one's own target; group fights are effectively invisible to a blind player. Without party status, active support (healing, rescuing) is barely possible.
Effort: medium (IPartyList is already available through Dalamud, and the panning logic already exists conceptually in BeaconService).
Priority: high

### Aggro/enmity announcement

What: through the native enmity/hate data structure (ClientStructs `EnmityModule`), detect whether the player holds position 1 on an enemy (tank aggro), and announce briefly on a change ("aggro on you" / "aggro lost").
Why it matters: for tank players and for general danger assessment in combat, aggro information is central and currently completely absent.
Effort: medium (requires reflection on a native structure not yet used, similar to what has already been done successfully for RaptureHotbarModule/AgentEmote).
Priority: medium

### Cooldown/GCD announcement ("ready")

What: via `ActionManager.GetRecastTime`/`IsRecastActive` and `HotbarSlot.IsSlotUsable`, detect when an ability is usable again and announce that with a short recognition tone or on a query key.
Why it matters: this is already noted in `STATUS.md` (lines 824-827, 1907) as the next planned step, but is not yet technically verified. Without cooldown feedback an efficient rotation is barely possible for blind players.
Effort: medium to large (the native structure first has to be verified with ilspycmd, as is already the practice for other features).
Priority: high

### The enemy's running cast time instead of a one-off announcement

What: extend the existing `IsCasting`/`CastActionId` detection in `CombatService.cs` with a remaining-time announcement (e.g. short interstitial tones that speed up, or an announcement at 50 percent remaining), plus explicitly stating whether the cast is interruptible (`IsCastInterruptible` is according to the analysis currently only logged, not spoken).
Why it matters: whether a dangerous cast is interruptible and how much time is left is one of the most important pieces of combat information for sighted players, and is completely missing for blind players.
Effort: small to medium (the data source is already opened up, only the speech output is missing).
Priority: high

### AoE area detection (ground effects)

What: through `ObjectTable` entries of the ground-effect/omen type (as far as they are readable through Dalamud/ClientStructs), determine the position and area of dangerous ground markers and output them as a directional warning tone (e.g. "area to the left, 3 metres" or a short tone that grows louder on approach).
Why it matters: dodging AoEs is one of the central combat skills in FF14 and is practically impossible for blind players without technical help. This is at the same time the largest technical challenge of the whole project.
Effort: large (it is unclear whether and how reliably ground-effect data is accessible through reflection; it requires a research phase of its own, and may only be feasible for simple, circular areas).
Priority: medium (high importance, but high technical risk - realistically classify this as a medium-term research project, not a quick win)

### Introduce buff/debuff announcements in a controlled way

What: stop treating the status effect bar (`_StatusCustom0`) purely as a spam source (currently unfiltered according to `STATUS.md` lines 96-98, with sprint countdown spam) and instead use it deliberately: announce important debuffs (immobilisation, silence, vulnerability stacks) when they appear/disappear, while suppressing countdown values as a matter of principle.
Why it matters: buffs/debuffs decide fights, but currently they are neither filtered nor used deliberately - only documented as a known announcement spam problem.
Effort: medium (building a curated ignore/announce list per status ID).
Priority: medium

## Improving what exists: announcement quality and spam reduction

### Finally filter the known spam sources

What: fix the spam sources documented in `STATUS.md` (lines 96-104) since V4.60/61 but left unfixed: `_StatusCustom0` (sprint countdown once a second), `_FlyText` ("+Sprint", "700", "(+100%)"), plus the remaining login chatter ("INVENTORY", "SIDE BY SIDE", "menu, 0 entries").
Why it matters: these disturbances have already been identified but were deliberately deferred according to the status documentation. They produce unnecessary, distracting speech output precisely in combat-adjacent situations.
Effort: small (extend the ignore list along the lines of `HudNoiseAddons` in `UIReaderService.cs`).
Priority: high

### A central configuration interface instead of pure code configuration

What: a real Dalamud settings UI (or at least a text-based configuration menu operable by keyboard) for the switches already present in `Configuration.cs` (chat channels, beacon volume, target change announcements etc.), plus new switches for announcement verbosity (short/normal/detailed) per topic area.
Why it matters: currently settings can only be changed by editing the configuration file or by recompiling (the analysis of `UIReaderService.cs` confirms: no runtime configuration, no settings interface). For end users without developer knowledge that is a considerable hurdle.
Effort: medium (Dalamud offers standard mechanisms for plugin configuration windows, which then have to be made screen-reader accessible themselves - e.g. purely keyboard-navigable ImGui elements).
Priority: high

### Code architecture: centralise addon names and node IDs

What: move the string literals for addon names and hard-coded node IDs currently spread across the whole of `UIReaderService.cs` (4092 lines) into a central configuration structure (dictionary/enum); merge recurring node traversal patterns (`ReadFirstTextInComponent`, `ReadAllTexts`, `ScanAddonTexts` and others) into a shared generic helper function.
Why it matters: this is not a functional gap but a maintainability investment: with over 4000 lines in a single file and heavily duplicated traversal code, every future extension (e.g. new addons for market/retainer, see below) becomes increasingly error-prone and laborious.
Effort: large (refactoring without functional change, high regression risk in such a central file - should be done step by step with test coverage per addon).
Priority: low (technical debt, but not a user pain point - only tackle it when new large features in the same area are due)

### Make speech rate/volume controllable from the plugin

What: use `Tolk_SetRate`/comparable Tolk functions to set a speech rate per announcement category (e.g. combat announcements faster, quest text normal), instead of relying entirely on the global screen reader setting.
Why it matters: in hectic combat situations faster but terser speech output can help; currently, according to the analysis of `TolkService.cs`, the plugin has no influence on this at all.
Effort: small to medium (depending on whether the Tolk version in use supports this control natively).
Priority: low

### Follow multilingualism through consistently

What: move the texts hard-coded to German in several places in `UIReaderService.cs` (e.g. `ConfirmButtonLabels`, the reward labels "Erfahrung"/"Gil") into the existing but only partly used `AccessibilityStrings` class, which already distinguishes DE/EN.
Why it matters: German speech output is currently hard-wired in several places even though the project already has bilingual infrastructure elsewhere - with an English game client the plugin would recognise things wrongly or not at all in those places.
Effort: medium
Priority: low

## Navigation and movement

### Improve front/back discrimination in the sound beacon

What: the existing beacon system (`BeaconService.cs`) currently encodes angular deviation only through pitch (880 Hz ahead down to 220 Hz behind) and the side through stereo pan; at 0° and 180° the pan signal delivers the same (centred) value, which makes "slightly ahead-left" and "slightly behind-left" harder to tell apart. An additional, third sound property (e.g. a timbre change or a short echo effect for "behind") would make the front/back distinction unambiguous - similar to the object signature principle from pure audio games such as Shades of Doom.
Why it matters: precise directional perception is the foundation of all manual navigation (the walk guide); an ambiguity here affects every single use.
Effort: small to medium (an extension of the existing `BeaconSampleProvider` class).
Priority: medium

### Object signature sounds for landmarks

What: define a short, characteristic recognition tone for each object category (NPC, gathering node, quest target, aetheryte, treasure chest), played in addition to the speech announcement while cycling through the object browser (`NavigationService.CycleObject`).
Why it matters: makes it easier to recognise object types quickly without waiting for the full text - a concept used successfully in comparable audio games (Shades of Doom) and transferable one-to-one to the existing category structure here.
Effort: small (short sound files plus a category→sound mapping in the existing cue/beacon system).
Priority: low

### Track known auto-walk problems systematically

What: convert the log mechanisms marked "DIAGNOSTIC (temporary)" in `AutoWalkService.cs` (waypoint logging for zone transition snags) into a permanent but unobtrusive diagnostic log, and report the known "bridge trap" bug (on bridges/walkways the navmesh wrongly casts onto a much lower floor) as well as the vnavmesh-internal mesh bug at the "Deep Forest" zone transition mentioned in `STATUS.md` back to the vnavmesh developers in documented form, since these problems lie outside this plugin's control.
Why it matters: auto-walk is a core function; unsolved navmesh problems that are only patched over lead to a loss of trust among blind users who have to rely entirely on the system while walking.
Effort: small (feedback to a third-party project) to medium (further workaround logic of our own).
Priority: medium

### Communicate vertical navigation and floor changes more clearly

What: when "no path found" is due to a separate mesh island (e.g. city levels reachable only by lift/aethernet), distinguish that more explicitly from a genuine pathfinding failure and - where possible - detect automatically whether a lift/staircase is nearby, instead of only pointing to the aethernet.
Why it matters: the existing system already recognises this problem conceptually according to a code comment ("walking can NEVER cross that gap") and already offers a good basic solution with the aethernet hint; an even more precise distinction (lift vs. pure distance) would further reduce misinterpretations.
Effort: medium
Priority: low

### Party rally point / "where is my group?"

What: offer a direction/distance announcement to the nearest or to all party members using `IPartyList` position data (analogous to the existing object browser logic), particularly useful when entering a dungeon or after a death/retreat.
Why it matters: in group content (dungeons, trials) staying with the group is crucial for blind players; currently there is no tool for it.
Effort: small to medium (position data is available through Dalamud, and the direction calculation already exists in `NavigationService.cs`).
Priority: medium

## UI areas: inventory, market, retainer, party finder

### Make the market board (auction house) accessible

What: make the market board window (search, price list, buying/selling) accessible through the existing universal addon reader or a dedicated handler; this is already noted in `STATUS.md` as a planned but not yet started item.
Why it matters: without market board access a substantial part of the in-game economy (buying gear, selling materials) is unusable for blind players - this was found as implemented in none of the 16 service files and nowhere in the status document.
Effort: large (a complex, list-based window with sort/filter functions, roughly as laborious as the already solved bestiary, probably more so because of the price/quantity fields).
Priority: high

### Make retainers usable

What: make the retainer windows (assigning ventures, viewing/editing the sale list, storage) accessible through dedicated or universal addon handlers.
Why it matters: according to the analysis there is currently only a bare object label ("retainer") in the target category announcement - no window logic at all. Retainers are central to the economy and inventory management for many players.
Effort: medium to large
Priority: medium

### Gear set/equipment announcement

What: make the current equipment (equipped items per slot) readable through `IGameInventory`/`InventoryType.EquippedItems` and announce it on a key press; in due course also make gear set switching screen-reader readable.
Why it matters: according to the analysis `InventoryService.cs` covers bags, key items and gil, but not equipment - a player currently cannot find out what they are wearing without using the sighted UI.
Effort: small to medium (the inventory API structure is already established in the project, and `EquippedItems` is a regular `GameInventoryType`).
Priority: high

### Trading between players (trade window)

What: make the trade window (offering items/gil, confirmation) accessible in the same way as the already solved NPC delivery window (`Request`).
Why it matters: trading between players is a fundamental social/economic interaction that is currently unsupported, even though the technical solution for a very similar window (NPC request) already exists and can be reused.
Effort: small to medium (the high similarity to the already solved request window is a clear advantage).
Priority: medium

### Duty Finder and Party Finder

What: make the dungeon/trial selection in the Duty Finder as well as the Party Finder lists (description, requirements, joining) readable through the universal or a dedicated handler.
Why it matters: without Duty Finder access blind players cannot get into group content (dungeons, trials, raids) on their own - one of the biggest access barriers to "playing completely independently".
Effort: medium (predominantly list-based windows, a similar structure to the list addons already solved).
Priority: high

### Mail (letter box)

What: make the letter box window (sender list, subject, attachments, accepting) accessible.
Why it matters: currently not mentioned or supported anywhere in the project; necessary for trading via retainer sales and for general communication.
Effort: small to medium (a simple list window)
Priority: low

### Basic crafting support

What: at minimum a simple announcement of the synthesis progress (progress/quality/durability values) during crafting, plus reading out the recipe book.
Why it matters: crafting is so far not mentioned or supported anywhere in the whole project; it is an extensive game area of its own whose complete coverage (rotation, real-time quality feedback) would be very laborious, but even a simple progress announcement would be a first way in.
Effort: large (full support), small to medium (basic status announcement only)
Priority: low (compared with the combat/market/duty finder gaps; classify as a long-term goal)

## Onboarding and getting started for new blind players

### Interactive first-time setup tutorial

What: a guided introduction (e.g. through `/acc tutorial` or automatically on the very first start) that walks step by step through the most important keys (object browser, auto-walk, help key, combat status), with short practice tasks instead of only a static help announcement.
Why it matters: the current help (`Ctrl+F1`) reads out a long, undifferentiated list of all keys at once - for complete newcomers without sight that is a high cognitive hurdle. Comparable projects such as Hearthstone Access deliberately rely on a dedicated, guided tutorial for screen reader users.
Effort: medium (concept and texts, no new technical infrastructure needed, uses the existing TTS output).
Priority: high

### Consolidate and expand the context-sensitive "where am I" announcement

What: promote the existing `AnnounceActiveWindow`/`Ctrl+F2` function more strongly as the central orientation aid and extend it with a short "what can I do here" note (e.g. automatically speak the most important operating hints for a new window type, like a context help).
Why it matters: new users in particular often do not know which menu/window they are in or which keys apply there - a consolidated, comprehensible announcement reduces confusion considerably.
Effort: small to medium
Priority: medium

### Practice mode / reduced complexity for combat newcomers

What: a kind of "training hint" on first entering combat mode that briefly explains the most important combat keys (HP announcement, hotbar, target switching), following the principle from pure audio games (Shades of Doom, Hearthstone Access) of easing users gently into the sound/speech vocabulary first.
Why it matters: combat systems are the most complex hurdle for blind newcomers; a gentle start increases the likelihood that new players do not give up early.
Effort: small (purely textual/conceptual, no new technology).
Priority: medium

### A central help system organised by topic instead of one long list

What: split the help announcement (`AnnounceHelp` in `Plugin.cs`) into thematic subsections (e.g. "Ctrl+F1 for navigation, Ctrl+F2 for combat, Ctrl+F3 for menus") that can be paged through with a key, instead of a single long block of prose.
Why it matters: the current help is already extensive (navigation, combat, inventory and emotes mixed together) and grows longer with every new feature - a flat list becomes increasingly unwieldy to read out.
Effort: small
Priority: medium

## Endgame: raids, Extreme/Savage - a realistic assessment

Completing Extreme or Savage raid mechanics fully and independently (complex, time-critical AoE patterns, stack/spread mechanics, role-specific special tasks) is only achievable to a very limited degree with the Dalamud means realistically available today. The basic technical prerequisite for it - reliable, low-latency detection of ground effects/telegraphs - is a laborious feature even in existing sighted add-ons (Cactbot, ACT overlays), one that depends on external encounter databases and constant maintenance per fight.

Realistically implementable and worthwhile, on the other hand:
- Party HP and one's own HP/cast information (see above) as a foundation for "support" roles (healers) even in harder content.
- A general, non-encounter-specific AoE detection for simple, clearly recognisable ground markers (see the suggestion above), which helps at least partly in Extreme/Savage too.
- Working together with a sighted person in the group (callouts over Discord/voice chat) remains the most practical route for highly complex encounter mechanics for the foreseeable future - the plugin should not try to replace that, but complement it where possible (e.g. through the cast interrupt feature mentioned above).

This assessment should be communicated transparently so that expectations stay realistic: "perfectly playable" is an achievable goal for solo and group content up to and including normal difficulty; for Extreme/Savage, "playable with support" is the more likely outcome.

## Sound design: developing the existing beacon concept further

The existing beacon system is already functional direction/distance feedback (pitch for angle, stereo pan for side, volume for distance), but it is not a true sonar with environmental scanning. Comparable projects show the following transferable concepts:

### Several simultaneous signature tones (object radar instead of a single target)

What: instead of only the one active navigation target, several nearby relevant objects (e.g. the 2-3 nearest enemies or gathering nodes) could be made audible simultaneously as quiet, distinguishable earcons in the background, similar to a sonar sweep.
Why it matters: currently the object browser has to be cycled through actively to find out what is nearby; a passive background signal would enable spatial awareness without an active query - a core concept from pure audio games such as Shades of Doom.
Effort: large (designing several simultaneous audio sources without them confusing each other is a substantial sound design challenge, though technically feasible with the existing NAudio substructure).
Priority: low (an exciting future feature, but not an acute pain point compared with the gaps named above).

### A warmer/colder principle for waypoint navigation

What: following the ESO accessibility addon FCOAccessibility, add a continuous "warmer/colder" effect to the walk guide (e.g. the pitch rises the closer one gets to the direct path to the target, independently of the pure target angle).
Why it matters: can help when the player deviates slightly from the optimal path, without them having to estimate the exact number of degrees - a lower-threshold complement to the existing angle/distance logic.
Effort: small to medium (an extension of the existing beacon system)
Priority: low

## Top 5 recommendation: what to tackle first

1. Filter the known announcement spam sources (`_StatusCustom0` sprint countdown, `_FlyText`, login chatter) - small effort, an immediately noticeable improvement in everyday usage quality, already identified since version 4.60/61 and simply not yet implemented.
2. Party HP announcement for group content (panned earcons plus a summary key) - closes the largest functional gap for everything beyond solo content and is feasible with the existing infrastructure (IPartyList, the BeaconService pattern).
3. Gear set/equipment announcement in the inventory - small to medium effort, closes a surprisingly fundamental gap (the player currently does not know what they are wearing).
4. Add cast interrupt information and a running announcement of the enemy's cast time - the data source is already opened up (`IsCastInterruptible` is already logged), only the speech output is missing; high impact on combat safety at low technical risk.
5. Make the Duty Finder and market board fundamentally accessible - larger efforts, but they close the two gaps that most strongly obstruct the goal of "playing completely without sight" (no independent access to group content, no independent market participation).
