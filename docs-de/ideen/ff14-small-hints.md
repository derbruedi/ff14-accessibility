# Small Hints — Grab Bag
## Short independent suggestions for FF14Accessibility, from the KOTOR accessibility mod

**From:** Voice of the Old Republic (KOTOR 1 accessibility mod) — https://github.com/JeanStiletto/voice-of-the-old-republic
**To:** the FF14Accessibility project (derbruedi/ff14-accessibility)
**Date:** 2026-07-15
**Status:** four unrelated small ideas that came out of reading your code
next to ours. Each stands alone; take or drop them individually. None
warrants a document of its own. (Wall-proximity cues, formerly hinted
here, moved into the room-shape guide's section 6 — they share its
walkability grid.)

---

## 1. Passive proximity callouts (and how they feed the discovery filter)

Announce named, refindable things — zone exits, aetherytes, notable
NPCs, entrances — automatically when the player first comes within
~15 m while walking normally. One spoken line, deduplicated per visit,
behind the same stability debounce as everything else. No keypress
needed; this is the blind equivalent of things entering the edge of a
sighted player's screen.

Two design points from ours:

- Dedup per *area visit*, not forever — re-entering a zone re-arms the
  callouts once. Passing the same door six times in one visit speaks
  once.
- If you adopt the discovery filter, these callouts are its best
  recording site: what was passively announced is exactly what the
  player organically "knows". The two features are worth shipping
  together — each makes the other more useful.

## 2. The map flag as a first-class target

Your `PlacesService` covers the game's static map markers well, but we
found no handling of the **player-set / party map flag**. In group play
the flag is how sighted players direct each other ("go to <flag>") — so
for a blind player it is the single most socially important coordinate
in the game. Suggestion:

- Read the current flag position (available client-side from the map
  agent; your sheet/agent knowledge is ahead of ours here).
- Expose it as an entry in your waypoint category, and accept it as a
  target for the walk guide and auto-walk like any other destination.
- Announce when a new flag appears while grouped ("Neue Markierung,
  120 Meter Nordost") — that's the moment the party expects everyone to
  react to.

## 3. Arbitrary positions are frozen coordinates, not objects

For flags, map markers and any "remember this spot" feature: snapshot
the position and a name at creation time and treat that pair as the
target — don't keep resolving a live entity. And route every feature
that acts on "the current target" (announce, walk guide, auto-walk,
distance query) through **one shared last-target slot** that both
objects and frozen positions can occupy. We converged on this after
shipping several features that each kept their own notion of "the
target" and drifted apart; unifying the slot removed a whole class of
"the beacon goes here but auto-walk goes there" bugs.

## 4. One-button log collection in the installer

For beta testing with blind users: add a "Collect logs" button to your
installer that zips the newest plugin log (plus a crash dump, if the
game left one) into the user's Downloads folder, announcing the result.
Testers can then attach one file to a report without any file-system
archaeology — which, over a screen reader, is the difference between
getting bug reports and not getting them. Ours does exactly this and it
paid for itself in the first beta week.

Related habit: keep plugin logs full-fidelity (no rate limiting, no
truncation). The logs' audience is you — or your LLM tooling — not the
user; complete evidence beats small files every time.
