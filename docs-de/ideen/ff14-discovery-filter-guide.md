# Discovery-Driven Object Filtering
## An implementation guide for FF14Accessibility, from the KOTOR accessibility mod

**From:** Voice of the Old Republic (KOTOR 1 accessibility mod) — https://github.com/JeanStiletto/voice-of-the-old-republic
**To:** the FF14Accessibility project (derbruedi/ff14-accessibility)
**Date:** 2026-07-15
**Status:** short companion to the room-shape and route-guidance guides.
The feature described is shipped and user-tested in our mod; the storage
recommendation is adapted to your platform. Deliberately brief — you
already have the hard part (a working category/filter system); this is
one predicate and one persistence file on top of it.

---

## 1. The idea

Your object navigation currently shows the player **everything** within
range. A sighted player doesn't know everything — they know what they
have *seen*. Discovery-driven filtering gives blind players the same
knowledge model:

- The **default** object list contains only objects the player has
  already encountered organically — announced by proximity, targeted,
  interacted with, involved in a quest step they did.
- A **toggle** ("Extended" in our mod) widens the list to everything,
  for when the player explicitly wants to scout the unknown.

Two effects, both bigger than they sound:

- **No spoilers.** Cycling no longer announces the boss room's contents,
  a hidden vendor, or a quest NPC the player hasn't met. Exploration
  stays exploration.
- **Refinding beats finding.** The most common navigation act is not
  "what's here?" but "take me back to that NPC / gathering point /
  door". A discovery list is short and consists entirely of things the
  player has a memory of — cycling through it is fast and every entry
  is meaningful.

Our users run with discovery as the default and toggle to extended
rarely and deliberately. **Discovery must be the default** for the
knowledge model to mean anything — an opt-in filter that players have
to remember to enable protects nobody from spoilers.

---

## 2. The one rule that makes or breaks it

**Record discovery only from organic encounters — never from the
extended list itself.** If browsing the extended cycle marks objects as
discovered, the toggle deletes its own meaning within a session (one
extended sweep = everything "known" forever). Our record sites are:
passive/targeted narration, proximity landmark callouts, and
interactions. The extended cycle reads the world but writes nothing.

For your plugin the natural record sites are: an object announced by
your existing target/nearest narration, an object the player interacted
with, a quest target the player actually reached, and objects announced
by the walk guide on arrival. Not: entries merely enumerated by Ctrl+N
cycling while the extended toggle is on.

---

## 3. What to store, and where

### Keys, not names

Store a stable, locale-independent key per discovered object and
regenerate the display name live at cycle time. Never store display
strings — they break on language change and go stale on patches. FFXIV
hands you better keys than our engine ever did: `DataId` for event
NPCs, event objects and gathering points, `NameId` for battle NPCs —
sheet row IDs, stable across sessions and languages. Scope each key to
its zone: `TerritoryType` + key.

Skip dynamic content entirely, as we did: generic spawned mobs, other
players, pets. Discovery is for things with a persistent identity worth
refinding — NPCs, gathering nodes, aetherytes, entrances, quest objects.

### Does the game already store this? Partially — use it where it does

We used an in-game persistence mechanism (KOTOR's save-embedded script
variables) so knowledge travels with the save file. You don't have
saves — but FFXIV **already tracks some discovery server-side, and for
those categories you should read the game's own state instead of
keeping your own:**

- **Aetherytes:** the attunement flag *is* discovery, with perfect
  parity to sighted players. Unattuned aetherytes belong in extended
  only.
- **Quests:** accepted/completed state gates which quest NPCs and
  objects the player "knows".
- **Hunt/sighting log entries, unlocked FATEs, gathering log:** same
  pattern where applicable — your BestiaryService likely touches some
  of this already.

Everything without a server-side flag (ordinary NPCs, entrances,
vendors, event objects) needs plugin-side storage — and on Dalamud that
is the boring, standard answer: **a JSON file in your plugin config
directory, keyed per character** (`ContentId`), since knowledge belongs
to the character, not the account or the machine. A dictionary of
zone-id → set of keys; load on login, save debounced on change. At a
few dozen entries per zone this stays trivially small for the life of a
character.

---

## 4. Wiring it into your existing filter

This is genuinely one predicate. Your `GetCategoryObjects()` already
filters the object table by category and range; add:

- if the discovery toggle is off (= discovery mode, the default): keep
  an object only if `IsDiscovered(territory, KeyOf(obj))` — or if its
  category is exempt (see below).
- if on (= extended): keep everything, record nothing.

Category exemptions we ended up with, and recommend: **quest targets of
active quests are always visible** regardless of discovery (the game
has explicitly told the player about them — filtering them out is
hostile), and the same for anything the game marks on the minimap for
sighted players. Discovery filtering applies to the ambient world, not
to active objectives.

UX details that mattered for us:

- The toggle announces its state when flipped ("Erweiterte Liste an /
  aus") and lives with your other mode toggles.
- When a discovery-filtered category is empty, say so distinctly:
  "Keine bekannten NPCs" — not the same phrase as "none in range", so
  the player learns the toggle exists and what it does.
- Offer a "forget this zone" command for testing and for players who
  want to re-experience an area. (Ours is config-side; rarely used but
  cheap.)

---

## 5. Build order

1. `KeyOf(obj)` + the per-character JSON store (load/save/contains/add).
2. Record calls at your organic narration and interaction sites.
3. The predicate in `GetCategoryObjects()` behind a new toggle,
   **extended as default at first** — run it silently for a few days and
   compare list contents.
4. Flip discovery to default, add the empty-category phrasing and the
   game-side checks (aetherytes, quests) where they replace your own
   bookkeeping.

Total effort is small; the value is in the defaults and the recording
discipline, not the code. Questions welcome — open an issue on our repo.
