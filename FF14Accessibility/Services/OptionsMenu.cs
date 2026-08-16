using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// The settings menu (Umschalt+F9). Every sound the mod makes, plus the
/// announcements that fire on their own, reachable and switchable without the
/// config file or a slash command.
///
/// It exists because the mod's sounds had no off switch a player could reach:
/// AoE ping, beacon, route cues and the ability-ready cue were each either a
/// compile-time constant, a hidden JSON key, or one dedicated hotkey in a
/// keyspace that is already full. Asking a blind player to edit
/// pluginConfigs/FF14Accessibility.json to silence a cue is not an option.
///
/// This is a BUILDER, not an input handler: it hands a <see cref="MenuLevel"/> to
/// the existing <see cref="SpokenMenu"/>, which already owns navigation, type-
/// ahead, back/close and the "menu owns the keyboard" rule. One interaction model
/// for every menu in the mod - the numpad works here exactly as it does in the
/// action menu, so there is nothing new to learn.
///
/// Volumes are offered as STEPS rather than as an on/off flip. A flip has to
/// remember the previous level somewhere, and the obvious place (the config
/// field itself) is destroyed by the very act of switching off. Steps sidestep
/// that entirely and are more useful besides: these cues are judged by ear
/// against game audio, so "quieter" is a more common wish than "silent".
/// </summary>
public sealed class OptionsMenu
{
    /// <summary>The offered volume steps. "Off" is a real step, so a step list
    /// covers the toggle case too.
    ///
    /// Bunched towards the QUIET end since 2026-08-03. The old list started
    /// at 25% and then doubled, which left no way to say "audible but well under
    /// the game" - the useful range for a cue you listen past rather than to. The
    /// AoE ping made that concrete: it was too loud at its default and the next
    /// step down was off.</summary>
    private static readonly float[] VolumeSteps = { 0f, 0.1f, 0.2f, 0.35f, 0.5f, 0.75f, 1f };

    private readonly Configuration _config;
    private readonly Action _save;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;
    private readonly HeadingService _heading;
    // The chat section's rows ARE the game's tabs, so the
    // menu asks the game what they are instead of holding a list of its own.
    private readonly GameChatFilters _chatFilters;

    public OptionsMenu(Configuration config, Action save, TolkService tolk, IPluginLog log,
                       HeadingService heading, GameChatFilters chatFilters)
    {
        _config  = config;
        _save    = save;
        _tolk    = tolk;
        _log     = log;
        _heading = heading;
        _chatFilters = chatFilters;
    }

    /// <summary>Builds the top level. Called fresh on every open, so the labels
    /// always report the values the config actually holds right now.</summary>
    public MenuLevel Build()
    {
        var level = new MenuLevel
        {
            Title = AccessibilityStrings.OptionsTitle,
            // Rebuild, damit die Chat-Zeilen dem Umschalter am Ende folgen: der ist
            // StayOpen, und ohne Rebuild stuenden hier nach dem Umschalten noch die
            // Abschnitte des anderen Systems - Zeilen, die auf das Falsche zeigen.
            Rebuild = Build,
        };

        level.Entries.Add(new MenuEntry { Label = AccessibilityStrings.OptionsSounds,        Submenu = BuildSounds });
        level.Entries.Add(new MenuEntry { Label = AccessibilityStrings.OptionsAnnouncements, Submenu = BuildAnnouncements });

        // "CHAT-KANAELE" GIBT ES IN BEIDEN SYSTEMEN, an derselben Stelle und mit
        // derselben Bedeutung: eine flache Liste, eine Zeile je Kanal, aus. Nur die
        // Quelle der Liste unterscheidet sich - im gewohnten System die festen
        // Kategorien des Plugins, im neuen die Kanaele des SPIELS, quer ueber alle
        // Register eingesammelt.
        //
        // DAS WAR EINE KORREKTUR (2026-08-16). Zuerst stand hier ein Entweder-Oder:
        // Kanaele im alten, Register im neuen System. Der User lief auf dem neuen,
        // bekam also weiterhin nur die Register zu sehen und damit genau die Funktion
        // nicht, nach der er gefragt hatte. Ein Abschnitt, den es je nach Einstellung
        // gar nicht gibt, ist fuer den Spieler nicht von einem kaputten zu
        // unterscheiden.
        level.Entries.Add(new MenuEntry
        {
            Label   = AccessibilityStrings.OptionsChatChannels,
            Submenu = _config.UseLegacyChatSystem ? BuildChatChannels : BuildGameChatChannels,
        });

        // Die feine Aufloesung, und nur das neue System hat sie: Register, darin
        // Kanaele, darin die Filterzeilen des Spiels - dort trennt sich "Schaden
        // ausgeteilt" von "Schaden erlitten". Die flache Liste darueber kann das nicht
        // und soll es nicht: sie beantwortet die haeufige Frage in einem Druck, diese
        // hier die seltene genau.
        if (!_config.UseLegacyChatSystem)
            level.Entries.Add(new MenuEntry { Label = AccessibilityStrings.OptionsChatTabs, Submenu = BuildChatTabs });

        level.Entries.Add(ChatSystemRow());
        return level;
    }

    // ── Welches Chatsystem ────────────────────────────────────────

    /// <summary>
    /// Schaltet zwischen dem gewohnten Chatsystem (feste Kategorien, Enter
    /// antwortet im gelesenen Kanal) und dem neuen aus PR #5 (die Puffer folgen
    /// den Registern und Filtern des Spiels) um.
    ///
    /// KEINE Toggle-Zeile, obwohl technisch ein bool dahinter steht: "an/aus"
    /// wuerde bedeuten, dass eines der beiden das Normale und das andere dessen
    /// Abwesenheit ist. Es sind zwei gleichrangige Systeme, also nennt die Zeile
    /// beim Vorlesen das, was gerade gilt, und der Druck sagt, was jetzt gilt.
    ///
    /// Die Zeile steht ganz oben auf dieser Ebene und nicht unter "Chat-Register":
    /// jener Abschnitt gehoert dem NEUEN System und ist im alten wirkungslos -
    /// den Umschalter dort hinein zu legen hiesse, ihn in dem Zustand zu
    /// verstecken, aus dem heraus man ihn am ehesten sucht.
    ///
    /// Sie wirkt SOFORT: beide Chat-Leser laufen dauernd mit und fragen diesen
    /// Wert bei jeder Zeile, beide Nachlesen werden dauernd gefuellt. Deshalb
    /// darf die Ansage auch versprechen, dass nichts verloren ist.
    /// </summary>
    private MenuEntry ChatSystemRow() => new()
    {
        Label    = AccessibilityStrings.OptChatSystemRow(_config.UseLegacyChatSystem),
        StayOpen = true,
        Activate = () =>
        {
            var legacy = !_config.UseLegacyChatSystem;
            _config.UseLegacyChatSystem = legacy;
            Persist();
            _tolk.SpeakInterrupt(AccessibilityStrings.ChatSystemSwitched(legacy));
            _log.Info($"[Einstellungen] Chatsystem -> {(legacy ? "alt (feste Kategorien)" : "neu (Register des Spiels)")}");
        },
    };

    // ── Chat-Kanäle des gewohnten Systems ─────────────────────────

    /// <summary>
    /// Eine Schaltung je Kanal des GEWOHNTEN Chatsystems - das Gegenstück zu
    /// <see cref="BuildChatTabs"/>, das dem neuen gehört.
    ///
    /// DIESE SCHALTER GAB ES SCHON, ERREICHBAR WAREN SIE NICHT. Die Felder stehen
    /// seit jeher in <see cref="Configuration"/> und werden in
    /// <c>LegacyChatReaderService.ShouldSpeak</c> gelesen, aber kein Menü und kein
    /// Befehl hat sie je angefasst: der einzige Weg war, pluginConfigs/
    /// FF14Accessibility.json von Hand zu bearbeiten. Genau das, was dieses Menü
    /// laut seinem eigenen Klassenkommentar nicht sein soll. Aufgefallen ist es,
    /// weil beide READMEs die Kanalauswahl als Funktion beschreiben (Spielerfrage
    /// 2026-08-16, "wie geht das?").
    ///
    /// DIE REIHENFOLGE IST DIE DER NACHLESE (LegacyChatHistoryService.Order), und
    /// die Namen sind wörtlich deren Kategorienamen. Wer einen Kanal stummschaltet,
    /// sucht ihn danach im Nachlese-Browser - dort trägt er dann dasselbe Wort.
    /// "Sammeln" ist die einzige Ausnahme und hat deshalb einen eigenen Namen: es
    /// hat einen eigenen Schalter, wird aber unter "System" archiviert.
    ///
    /// NICHT DABEI, weil ohne Schalter im Leser: Fehlermeldungen des Spiels (die
    /// Antwort auf eine gerade ausgeführte Aktion) und das eigene /echo. Eine Zeile
    /// anzubieten, hinter der ein fest verdrahtetes <c>true</c> steht, wäre eine
    /// Schaltung, die nichts schaltet.
    /// </summary>
    private MenuLevel BuildChatChannels() => new()
    {
        Title   = AccessibilityStrings.OptionsChatChannels,
        Rebuild = BuildChatChannels,
        Entries =
        {
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.Dialogue),
                          () => _config.ReadNpcDialogue,       v => _config.ReadNpcDialogue = v),
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.Say),
                          () => _config.ReadSayChat,           v => _config.ReadSayChat = v),
            // Rufen deckt auch /bruellen, Gruppe auch die weltuebergreifende
            // Gruppe, Fluestern beide Richtungen - so steht es in ShouldSpeak. Ein
            // Kanal, den das Spiel getrennt führt, das Plugin aber nicht, bekommt
            // hier keine eigene Zeile: sie würde eine Trennung versprechen, die
            // der Leser nicht macht.
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.Shout),
                          () => _config.ReadShoutChat,         v => _config.ReadShoutChat = v),
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.Party),
                          () => _config.ReadPartyChat,         v => _config.ReadPartyChat = v),
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.Alliance),
                          () => _config.ReadAllianceChat,      v => _config.ReadAllianceChat = v),
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.Tell),
                          () => _config.ReadTellChat,          v => _config.ReadTellChat = v),
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.FreeCompany),
                          () => _config.ReadFCChat,            v => _config.ReadFCChat = v),
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.System),
                          () => _config.ReadSystemMessages,    v => _config.ReadSystemMessages = v),
            ChannelToggle(AccessibilityStrings.OptChatGathering,
                          () => _config.ReadGatheringMessages, v => _config.ReadGatheringMessages = v),
            // AnnounceLoot heißt anders als seine Nachbarn, wirkt aber an genau
            // derselben Stelle: es ist der Schalter für XivChatType.LootNotice in
            // ShouldSpeak und sonst nirgends (geprüft 2026-08-16). Also gehört er
            // hierher und nicht zu den Ansagen.
            ChannelToggle(ChannelName(LegacyChatHistoryService.Category.Loot),
                          () => _config.AnnounceLoot,          v => _config.AnnounceLoot = v),
        },
    };

    /// <summary>Der Name, unter dem die Nachlese diesen Kanal führt. Eine Quelle für
    /// beides, damit Menü und Browser nicht auseinanderlaufen können.</summary>
    private static string ChannelName(LegacyChatHistoryService.Category category) =>
        AccessibilityStrings.LegacyChatCategoryName(category);

    /// <summary>
    /// Wie <see cref="Toggle"/>, sagt beim ABSCHALTEN aber dazu, dass der Kanal
    /// weiter nachlesbar bleibt.
    ///
    /// Der Satz steht hier und nicht in der Beschriftung, weil er nur einmal
    /// gebraucht wird: in dem Moment, in dem der Spieler etwas stummschaltet und
    /// sich fragt, ob es damit weg ist. Beim Durchblättern wäre er neun Mal im Weg.
    /// </summary>
    private MenuEntry ChannelToggle(string name, Func<bool> get, Action<bool> set) => new()
    {
        Label    = AccessibilityStrings.OptionToggle(name, get()),
        StayOpen = true,
        Activate = () =>
        {
            var value = !get();
            set(value);
            Persist();
            var spoken = AccessibilityStrings.OptionToggled(name, value);
            if (!value) spoken += " " + AccessibilityStrings.ChatChannelStillArchived;
            _tolk.SpeakInterrupt(spoken);
            _log.Info($"[Einstellungen] Chat-Kanal {name} -> {(value ? "an" : "aus")} "
                      + "(Nachlese unberuehrt)");
        },
    };

    // ── Chat-Kanäle des NEUEN Systems (flach über alle Register) ──

    /// <summary>
    /// Dieselbe flache Liste wie <see cref="BuildChatChannels"/>, nur mit den
    /// Kanälen des SPIELS: eine Zeile je Kanal, quer über alle Register
    /// eingesammelt und in der Reihenfolge des spieleigenen Einstellungsfensters.
    ///
    /// WARUM ES SIE NEBEN <see cref="BuildChatTabs"/> GIBT: dort ist ein Kanal drei
    /// Ebenen tief (Register, Kanal, Filterzeile) und steht unter dem Register, in
    /// dem der Spieler ihn eingerichtet hat. Das ist die richtige Form für die feine
    /// Frage ("nur den erlittenen Schaden") und die falsche für die häufige ("die
    /// Freie Gesellschaft will ich nicht mehr hören") - für die muss der Spieler
    /// sonst wissen, in welchem seiner Register der Kanal sitzt. Ein Kanal in
    /// mehreren Registern braucht dann sogar mehrere Griffe.
    ///
    /// EINE ZEILE SCHALTET DEN KANAL IN ALLEN REGISTERN, in denen er vorkommt, und
    /// meldet "an", sobald ihn EIN Register spricht - denn genau dann hört ihn der
    /// Spieler. Das ist dieselbe Oder-Regel, mit der ChatReaderService entscheidet
    /// (dort über die Routen einer Zeile), nur von der anderen Seite gelesen.
    ///
    /// GESCHRIEBEN WIRD NUR DER SPRACH-SCHALTER DES PLUGINS. Die Filterkästchen des
    /// Spiels bleiben unangetastet - was hier verstummt, steht weiter im Chatlog und
    /// in der Nachlese. Siehe ChatTabSpeech, ganz oben.
    /// </summary>
    private MenuLevel BuildGameChatChannels()
    {
        var level = new MenuLevel
        {
            Title   = AccessibilityStrings.OptionsChatChannels,
            Rebuild = BuildGameChatChannels,
        };

        // Kanal -> die Register, die ihn gerade zeigen. Live aus den Filterbytes des
        // Spiels, wie ueberall in diesem Abschnitt: hakt der Spieler drueben etwas
        // an, steht es beim naechsten Oeffnen hier.
        var byChannel = new Dictionary<int, List<GameChatTab>>();
        var channels = new List<int>();
        var tabs = _chatFilters.Available ? _chatFilters.Tabs : Array.Empty<GameChatTab>();
        foreach (var tab in tabs)
        {
            if (_chatFilters.ChannelsInTab(tab.Index, channels) != ChatFilterState.Ready) continue;
            foreach (var key in channels)
            {
                if (!byChannel.TryGetValue(key, out var showing))
                    byChannel[key] = showing = new List<GameChatTab>();
                showing.Add(tab);
            }
        }

        // Sortiert wie das Einstellungsfenster des Spiels (Kategorie, dann
        // DisplayOrder) - GameChatChannel.Sort traegt genau das. Ein Kanal, den das
        // Sheet nicht mehr kennt, faellt raus statt einen erfundenen Namen zu
        // bekommen; dieselbe Regel wie in BuildChatTab.
        var ordered = byChannel.Keys
            .Select(key => _chatFilters.Channel(key))
            .Where(channel => channel != null)
            .OrderBy(channel => channel!.Sort)
            .ToList();

        foreach (var channel in ordered)
        {
            var key = channel!.Key;
            var showing = byChannel[key];
            level.Entries.Add(ChannelToggle(
                channel.Name,
                () => IsChannelAudible(key, showing),
                on => SetChannelInAllTabs(key, showing, on)));
        }

        // Die beiden Zeilen, die zu keinem Register gehoeren - woertlich aus
        // BuildChatTabs uebernommen, damit hier nichts fehlt, was es dort gibt.
        if (level.Entries.Count > 0)
            level.Entries.Add(ChannelToggle(
                AccessibilityStrings.OptChatUnfiltered,
                () => ChatTabSpeech.IsOn(_config, ChatTabSpeech.UnfilteredIndex, true),
                on => ChatTabSpeech.Set(_config, ChatTabSpeech.UnfilteredIndex, on)));
        else
            level.Entries.Add(ChannelToggle(
                AccessibilityStrings.OptChatFallback,
                () => ChatTabSpeech.IsOn(_config, ChatTabSpeech.FallbackIndex, true),
                on => ChatTabSpeech.Set(_config, ChatTabSpeech.FallbackIndex, on)));

        return level;
    }

    /// <summary>
    /// Ob der Spieler diesen Kanal irgendwo hört.
    ///
    /// Die Frage wird genauso beantwortet, wie ChatReaderService sie beim Sprechen
    /// stellt: ein Register muss vorgelesen werden UND die Zeile darin muss an sein.
    /// Geprüft wird bis auf die FILTERZEILEN hinunter, weil das Untermenü einzelne
    /// Zeilen setzen kann und ein gespeicherter Zeilen-Schalter seinen Kanal
    /// aussticht (<see cref="ChatTabSpeech.RowIsOn"/>). Nur den Kanal zu fragen
    /// hiesse, eine Zeile zu übersehen, die noch spricht.
    /// </summary>
    private bool IsChannelAudible(int channelKey, List<GameChatTab> showing)
    {
        var rows = new List<int>();
        foreach (var tab in showing)
        {
            var tabDefault = !tab.CarriesBattleLog;
            if (!ChatTabSpeech.IsOn(_config, tab.Index, tabDefault)) continue;

            // Nicht lesbar: dann entscheidet der Kanal-Schalter allein. Lieber die
            // gröbere Antwort als gar keine - eine Zeile, die in diesem Moment
            // nichts sagen kann, waere schlimmer als eine, die eine Ebene hoeher
            // schaut.
            if (_chatFilters.RowsInTab(tab.Index, channelKey, rows) != ChatFilterState.Ready
                || rows.Count == 0)
            {
                if (ChatTabSpeech.ChannelIsOn(_config, tab.Index, channelKey, true)) return true;
                continue;
            }

            foreach (var rowId in rows)
                if (ChatTabSpeech.RowIsOn(_config, tab.Index, channelKey, rowId, true)) return true;
        }
        return false;
    }

    /// <summary>
    /// Schaltet einen Kanal in jedem Register, das ihn zeigt.
    ///
    /// Die gespeicherten ZEILEN-Schalter dieses Kanals werden dabei vergessen
    /// (<see cref="ChatTabSpeech.ClearRows"/>, vor dem Setzen - siehe dort warum),
    /// damit sie den Kanal wieder erben. Sonst spräche nach einem "aus" noch eine
    /// einzeln eingeschaltete Zeile weiter, und nach einem "an" bliebe eine einzeln
    /// abgeschaltete stumm: in beiden Fällen sagte die Zeile im Menü etwas anderes,
    /// als der Spieler hört. Wer die feine Einstellung behalten will, macht sie
    /// unter "Chat-Register" - dieser Schalter ist die grobe Antwort und sagt das,
    /// indem er die feine überschreibt.
    /// </summary>
    private void SetChannelInAllTabs(int channelKey, List<GameChatTab> showing, bool on)
    {
        var channels = new List<int>();
        var rows = new List<int>();

        foreach (var tab in showing)
        {
            var tabDefault = !tab.CarriesBattleLog;

            if (_chatFilters.RowsInTab(tab.Index, channelKey, rows) == ChatFilterState.Ready)
                ChatTabSpeech.ClearRows(_config, tab.Index, rows);

            // Die Kanalliste des Registers wird frisch gelesen und nicht von oben
            // durchgereicht - aus demselben Grund wie in SetChannelSpeech: SetChannel
            // legt die uebrigen Kanaele still, wenn es ein stummes Register oeffnet,
            // und eine veraltete Liste wuerde dabei den falschen Kanal treffen.
            _chatFilters.ChannelsInTab(tab.Index, channels);

            if (ChatTabSpeech.SetChannel(_config, tab.Index, channelKey, on, tabDefault, channels))
                _log.Info($"[Einstellungen] Registerkarte {tab.Index} wurde nicht vorgelesen - "
                          + $"fuer Kanal {channelKey} eingeschaltet, die uebrigen "
                          + $"{channels.Count - 1} bleiben ungesprochen.");
        }
    }

    // ── Chat tabs ─────────────────────────────────────────────────

    /// <summary>
    /// One switch per CHAT TAB the game has, carrying the
    /// tab's own name.
    ///
    /// This replaces six groups of sixty-odd per-channel switches. That menu
    /// was the visible half of a mod-side catalogue which duplicated something the
    /// game already models - which channels exist, which of them a tab shows, what
    /// the tab is called - and the two could disagree about what the player had
    /// configured. Membership is the game's now (<see cref="GameChatFilters"/>), and
    /// what is left is the one question the game cannot answer: should a screen
    /// reader say this tab out loud.
    ///
    /// A switch here NEVER touches the buffer. Every tab is archived in full and
    /// browsable with Strg+, / Strg+. whatever these rows say - which is why the old
    /// "log disabled channels" row is gone rather than moved: it has nothing left to
    /// decide.
    ///
    /// IT IS THREE LEVELS NOW. The top level is still one row per
    /// tab and still reports that tab's master switch, but the row OPENS instead of
    /// toggling: inside sit the master itself and one row per channel the tab has
    /// switched on, and a channel that covers several of the game's checkboxes opens in
    /// turn. User: *"it does need a master toggle for the battle tab, but then it has to
    /// check what [channels] are enabled in the battle tab and enable or disable realtime
    /// speech for them."*
    ///
    /// Every row at every level is read from the game at the moment the menu is built -
    /// <see cref="GameChatFilters.ChannelsInTab"/> and
    /// <see cref="GameChatFilters.RowsInTab"/> - so they are whatever the player has
    /// ticked in the game's own chat-log settings, in the sheet's order, under the game's
    /// own names. Untick a filter in the game and its row is gone from here on the next
    /// open. That is following the game's list, not keeping one.
    ///
    /// WHY THE THIRD LEVEL EXISTS, and it is a correction: an earlier draft claimed the eleven
    /// battle-log actor groups already separated "damage dealt" from "damage taken". They
    /// do not. Category 4 is the "You" group and carries both - row 51 "Damage dealt by
    /// you." (caster=LocalPlayer) and row 58 "Damage you are dealt." (target=LocalPlayer),
    /// measured with the sheets themselves 2026-08-11. The groups split by WHOSE line it
    /// is; only the game's own checkbox says which way it went. So the user's example
    /// needs the row, and the row is where the switch went.
    /// </summary>
    private MenuLevel BuildChatTabs()
    {
        var level = new MenuLevel
        {
            Title   = AccessibilityStrings.OptionsChatTabs,
            Rebuild = BuildChatTabs,
        };

        var tabs = _chatFilters.Available ? _chatFilters.Tabs : Array.Empty<GameChatTab>();
        foreach (var tab in tabs)
        {
            // Captured per row: the lambdas outlive this loop.
            var index = tab.Index;
            var name = tab.Name;
            var fallback = !tab.CarriesBattleLog;
            level.Entries.Add(new MenuEntry
            {
                // The row still SAYS the tab's state, so "which tabs speak" is still one
                // pass down this list without opening anything.
                Label   = AccessibilityStrings.OptionToggle(name, ChatTabSpeech.IsOn(_config, index, fallback)),
                Submenu = () => BuildChatTab(index, name, fallback),
            });
        }

        // The lines the game has NO checkbox for - Notice, Urgent,
        // Debug, incoming tells, the GM channels: 21 of the 81 chat kinds. They belong to
        // no tab, so no tab's row can reach them, and until this change they were dropped
        // outright. Default ON, because the player has no game-side switch to turn them
        // back on with; this row exists so the class is not unsilenceable either.
        if (level.Entries.Count > 0)
            level.Entries.Add(Toggle(
                AccessibilityStrings.OptChatUnfiltered,
                () => ChatTabSpeech.IsOn(_config, ChatTabSpeech.UnfilteredIndex, true),
                v  => ChatTabSpeech.Set(_config, ChatTabSpeech.UnfilteredIndex, v)));

        // The game's state is not readable - all chat is in the one fallback buffer,
        // so the section offers the one switch that exists rather than speaking its
        // own name over an empty list, which reads as a fault.
        if (level.Entries.Count == 0)
            level.Entries.Add(Toggle(
                AccessibilityStrings.OptChatFallback,
                () => ChatTabSpeech.IsOn(_config, ChatTabSpeech.FallbackIndex, true),
                v  => ChatTabSpeech.Set(_config, ChatTabSpeech.FallbackIndex, v)));

        return level;
    }

    /// <summary>
    /// One tab's speech: the master, then the channels the tab
    /// is currently showing.
    ///
    /// A channel row that has never been touched REPORTS THE MASTER, because that is
    /// what it does - an unset channel inherits its tab (see
    /// <see cref="ChatTabSpeech.ChannelIsOn"/>). So a battle tab that is not read aloud
    /// opens with every row reading "off", which is the truth about what the player will
    /// HEAR - all of it is archived and browsable either way - and switching one of them
    /// on starts speaking that channel alone (see <see cref="SetChannelSpeech"/>).
    /// </summary>
    /// <param name="index">The game's own tab index.</param>
    /// <param name="name">The tab's own name, which is the player's if they renamed it.</param>
    /// <param name="fallback">What the master defaults to when the player has never set
    /// it - derived from the tab's filter set, not typed in. See
    /// <c>ChatReaderService.DefaultSpeechFor</c>.</param>
    private MenuLevel BuildChatTab(int index, string name, bool fallback)
    {
        var level = new MenuLevel
        {
            Title   = name,
            Rebuild = () => BuildChatTab(index, name, fallback),
        };

        level.Entries.Add(Toggle(
            AccessibilityStrings.OptChatTabMaster,
            () => ChatTabSpeech.IsOn(_config, index, fallback),
            v  => ChatTabSpeech.Set(_config, index, v)));

        var channels = new List<int>();
        if (_chatFilters.ChannelsInTab(index, channels) != ChatFilterState.Ready)
            return level;   // not readable this frame: the master row is still honest

        var rows = new List<int>();
        foreach (var key in channels)
        {
            var channel = _chatFilters.Channel(key);

            // A key the sheet no longer has. Skipped rather than given a made-up name:
            // an unnamed row is a row nobody can act on, and inventing one would hide
            // that the channel list and the sheet have come apart.
            if (channel == null) continue;

            var channelKey = key;
            var channelName = channel.Name;

            // How many of the game's own checkboxes this channel covers in THIS tab.
            // One for every channel of categories 1 and 2 - there the row is the channel
            // and a plain switch is the whole truth. Up to sixteen for a battle-log actor
            // group, and those sixteen are where "dealt" and "taken" part company, so
            // that channel gets a level of its own rather than one switch over all of it.
            _chatFilters.RowsInTab(index, channelKey, rows);
            if (rows.Count <= 1)
            {
                level.Entries.Add(Toggle(
                    channelName,
                    () => ChatTabSpeech.ChannelIsOn(_config, index, channelKey,
                                                    ChatTabSpeech.IsOn(_config, index, fallback)),
                    v  => SetChannelSpeech(index, channelKey, v, fallback)));
                continue;
            }

            level.Entries.Add(new MenuEntry
            {
                Label   = AccessibilityStrings.OptionToggle(
                              channelName,
                              ChatTabSpeech.ChannelIsOn(_config, index, channelKey,
                                                        ChatTabSpeech.IsOn(_config, index, fallback))),
                Submenu = () => BuildChatChannel(index, channelKey, channelName, fallback),
            });
        }

        return level;
    }

    /// <summary>
    /// One channel of one tab: the whole group, then each of the
    /// game's own checkboxes under it.
    ///
    /// This level exists for the battle log and its measurement is the reason: category 4
    /// is the "You" group and it carries BOTH "Damage dealt by you." and "Damage you are
    /// dealt." (rows 51 and 58, the sheets themselves 2026-08-11). The eleven actor
    /// groups split by whose line it is; only the row says which way it went. Without
    /// this level the user's example - speak what is done to me, archive what I do -
    /// cannot be expressed at all.
    ///
    /// The rows are the tab's own ticked checkboxes, named by the sheet, in the sheet's
    /// order. Untick one in the game's settings window and it is gone from here.
    /// </summary>
    private MenuLevel BuildChatChannel(int tabIndex, int channelKey, string channelName, bool fallback)
    {
        var level = new MenuLevel
        {
            Title   = channelName,
            Rebuild = () => BuildChatChannel(tabIndex, channelKey, channelName, fallback),
        };

        level.Entries.Add(Toggle(
            AccessibilityStrings.OptChatChannelAll,
            () => ChatTabSpeech.ChannelIsOn(_config, tabIndex, channelKey,
                                            ChatTabSpeech.IsOn(_config, tabIndex, fallback)),
            v  => SetChannelSpeech(tabIndex, channelKey, v, fallback)));

        var rows = new List<int>();
        if (_chatFilters.RowsInTab(tabIndex, channelKey, rows) != ChatFilterState.Ready)
            return level;

        foreach (var id in rows)
        {
            var rowId = id;
            level.Entries.Add(Toggle(
                _chatFilters.RowName(rowId),
                () => ChatTabSpeech.RowIsOn(_config, tabIndex, channelKey, rowId,
                                            ChatTabSpeech.IsOn(_config, tabIndex, fallback)),
                v  => SetRowSpeech(tabIndex, channelKey, rowId, v, fallback)));
        }

        return level;
    }

    /// <summary>
    /// Writes one channel switch, re-reading the tab's channel
    /// list from the game FIRST.
    ///
    /// The list is read again here rather than captured when the menu was built, because
    /// switching a channel on while its tab is not being read aloud stores "not spoken"
    /// for the tab's OTHER channels (see <see cref="ChatTabSpeech.SetChannel"/> for why),
    /// and using a stale list would silence a channel by a filter state that is no longer
    /// current.
    /// </summary>
    private void SetChannelSpeech(int tabIndex, int channelKey, bool on, bool fallback)
    {
        var channels = new List<int>();
        _chatFilters.ChannelsInTab(tabIndex, channels);

        if (ChatTabSpeech.SetChannel(_config, tabIndex, channelKey, on, fallback, channels))
            _log.Info($"[Einstellungen] Registerkarte {tabIndex} wurde nicht vorgelesen - "
                      + $"fuer diesen Kanal eingeschaltet, die uebrigen {channels.Count - 1} "
                      + "bleiben ungesprochen. Protokolliert wird weiterhin alles.");
    }

    /// <summary>
    /// Writes one filter row's switch, re-reading both the tab's
    /// channel list and the channel's row list from the game first - see
    /// <see cref="SetChannelSpeech"/> for why they are not captured.
    /// </summary>
    private void SetRowSpeech(int tabIndex, int channelKey, int rowId, bool on, bool fallback)
    {
        var channels = new List<int>();
        var rows = new List<int>();
        _chatFilters.ChannelsInTab(tabIndex, channels);
        _chatFilters.RowsInTab(tabIndex, channelKey, rows);

        if (ChatTabSpeech.SetRow(_config, tabIndex, channelKey, rowId, on, fallback, channels, rows))
            _log.Info($"[Einstellungen] Registerkarte {tabIndex}, Kanal {channelKey} wurde "
                      + $"nicht vorgelesen - fuer Zeile {rowId} eingeschaltet, die uebrigen "
                      + $"{rows.Count - 1} Zeilen bleiben ungesprochen. Protokolliert wird "
                      + "weiterhin alles.");
    }

    // ── Sounds ────────────────────────────────────────────────────

    private MenuLevel BuildSounds() => new()
    {
        Title   = AccessibilityStrings.OptionsSounds,
        Rebuild = BuildSounds,
        Entries =
        {
            // JEDE ZEILE HIER HAT EINEN DIENST HINTER SICH, der ihr Feld auch liest.
            // Eine Beschriftung ohne Funktion dahinter ist genau der Weg, auf dem eine
            // tote Einstellung eine Ueberarbeitung ueberlebt - deshalb steht hier
            // weniger, als das Menue auf dem Zweig fuehrt, aus dem dieser Beitrag stammt.
            //
            // BEWUSST NICHT DABEI: die Flaechenwarnung. Sie ist schon ueber
            // KeyToggleAoeWarning (Strg+Umschalt+F3) schaltbar, und ihr Ausloeser
            // (CombatService.UpdateAoeWarning) filtert ueber Action.EffectRange und
            // CastType. Nach unserer Messung ist das zu weit gefasst: die Grafik des
            // Telegrafen haengt an Omen, und mehrere tausend Aktionen mit CastType 2
            // zeichnen ueberhaupt keine Flaeche. Eine zweite, bequemere Schaltung fuer
            // etwas, das oefter warnt als das Spiel zeigt, gehoert nicht ins Menue,
            // bevor der Ausloeser stimmt. Der bestehende Hotkey bleibt unangetastet.
            Volume(AccessibilityStrings.OptBeacon,           () => _config.BeaconVolume,        v => _config.BeaconVolume = v),
            Volume(AccessibilityStrings.OptRouteCues,        () => _config.RouteCueVolume,      v => _config.RouteCueVolume = v),
            Toggle(AccessibilityStrings.OptSkillReady,       () => _config.AnnounceSkillReady,  v => _config.AnnounceSkillReady = v),
            Volume(AccessibilityStrings.OptSkillReadyVolume, () => _config.SkillReadyCueVolume, v => _config.SkillReadyCueVolume = v),
        },
    };

    // ── Announcements ─────────────────────────────────────────────

    private MenuLevel BuildAnnouncements() => new()
    {
        Title   = AccessibilityStrings.OptionsAnnouncements,
        Rebuild = BuildAnnouncements,
        Entries =
        {
            // Re-baselines the heading service on every flip. The old N
            // key did this too (Plugin.ToggleHeading) and N is gone as of
            // 2026-08-03 (it collided with MENU_CRAFT), so this row is now the
            // ONLY way to switch the compass - if it skipped the reset, turning
            // the announcement back on would echo the direction the player is
            // already facing on the very next frame.
            Toggle(AccessibilityStrings.OptHeading,       () => _config.AnnounceHeading,
                                                  v => { _config.AnnounceHeading = v; _heading.ResetBaseline(); }),
            Toggle(AccessibilityStrings.OptTargetChanges, () => _config.AnnounceTargetChanges, v => _config.AnnounceTargetChanges = v),
            Toggle(AccessibilityStrings.OptTargetHp,      () => _config.AnnounceTargetHp,      v => _config.AnnounceTargetHp = v),
            Toggle(AccessibilityStrings.OptEnemyCast,     () => _config.AnnounceEnemyCast,     v => _config.AnnounceEnemyCast = v),
            // FOUR ROWS ARE GONE FROM HERE, all for the same
            // reason: they switched chat channels, and chat membership is the game's
            // now. Leaving them would be four switches with nothing behind them.
            //
            //  - "Damage taken" / "Damage dealt" backed Local/CombatLogService, which
            //    is deleted. Battle lines travel the normal chat road and land in
            //    whichever tab the player shows them in; the game's own filter list
            //    splits them by actor far more finely than those two switches did
            //    (own / party / alliance / others / pets / enemies, 16 rows each).
            //  - "Experience" and "Loot" wrote per-channel switches in the deleted
            //    catalogue. Progress (64) and LootNotice (65) are rows in the game's
            //    filter window like every other channel, and they are spoken when the
            //    tab holding them is.
            //
            // Everything they used to control is now under Chat-Register, one row per
            // tab, plus the game's own chat log settings for which lines a tab shows.
            Toggle(AccessibilityStrings.OptMapFlag,       () => _config.AnnounceMapFlag,       v => _config.AnnounceMapFlag = v),
            Toggle(AccessibilityStrings.OptErrorToasts,   () => _config.AnnounceErrorToasts,   v => _config.AnnounceErrorToasts = v),
            Toggle(AccessibilityStrings.OptInfoToasts,    () => _config.AnnounceInfoToasts,    v => _config.AnnounceInfoToasts = v),
            // Writes the static the string formatters read as well
            // as the config field, in that order, so the very next announcement -
            // including this row's own confirmation - already obeys the new setting.
            // Splitting the two is how a setting starts lying about itself.
        },
    };

    // ── Row builders ──────────────────────────────────────────────

    /// <summary>An on/off row. Stays open and speaks the new state itself:
    /// SpokenMenu's Rebuild refreshes the label but deliberately does not re-read
    /// the row, so without this the flip would be silent.</summary>
    private MenuEntry Toggle(string name, Func<bool> get, Action<bool> set) => new()
    {
        Label     = AccessibilityStrings.OptionToggle(name, get()),
        StayOpen  = true,
        Activate  = () =>
        {
            var value = !get();
            set(value);
            Persist();
            _tolk.SpeakInterrupt(AccessibilityStrings.OptionToggled(name, value));
            _log.Info($"[Einstellungen] {name} -> {(value ? "an" : "aus")}");
        },
    };

    /// <summary>A row that opens the volume steps for one sound.</summary>
    private MenuEntry Volume(string name, Func<float> get, Action<float> set) => new()
    {
        Label   = AccessibilityStrings.OptionVolume(name, get()),
        Submenu = () => BuildVolumeSteps(name, get, set),
    };

    private MenuLevel BuildVolumeSteps(string name, Func<float> get, Action<float> set)
    {
        var level = new MenuLevel
        {
            Title   = name,
            Rebuild = () => BuildVolumeSteps(name, get, set),
        };

        foreach (var step in VolumeSteps)
        {
            var value = step;                       // captured per iteration
            level.Entries.Add(new MenuEntry
            {
                Label    = AccessibilityStrings.VolumeStep(value),
                StayOpen = true,                    // so several steps can be tried by ear in one visit
                Activate = () =>
                {
                    set(value);
                    Persist();
                    _tolk.SpeakInterrupt(AccessibilityStrings.VolumeSet(name, value));
                    _log.Info($"[Einstellungen] {name} -> {value:F2}");
                },
            });
        }

        // Open on the step that is currently in force, so the first thing spoken
        // is where the setting stands rather than an unrelated row.
        level.Cursor = NearestStep(get());
        return level;
    }

    /// <summary>Index of the offered step closest to <paramref name="current"/>.
    /// The stored value need not be one of the steps - the shipped defaults
    /// (0.35 beacon, 0.4 route cues) are not - so this picks the nearest rather
    /// than failing to match and silently starting at the top.</summary>
    private static int NearestStep(float current)
    {
        var best = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < VolumeSteps.Length; i++)
        {
            var distance = MathF.Abs(VolumeSteps[i] - current);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        return best;
    }

    /// <summary>Writes the config to disk. Every change is saved immediately:
    /// a settings menu that loses its changes on a crash or a logout is worse
    /// than no settings menu, because the player cannot see that it happened.</summary>
    private void Persist()
    {
        // try-catch: Dalamud file I/O. A failed save must not take the frame down,
        // but it must not pass silently either - the setting would appear to work
        // and then be gone next session.
        try
        {
            _save();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Einstellungen] Konfiguration konnte nicht gespeichert werden.");
        }
    }
}
