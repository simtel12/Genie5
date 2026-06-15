# Genie 5 — v5.0.0-alpha.6

The **Weighted Travel** release. The auto-walker now routes by *effort*, not raw
hop count — it avoids brutal open-water swims and cliff climbs when a bridge or
gate exists, paces reliably through identical-looking rooms, and recovers from a
stuck move instead of hanging. To make the routing smart, we need your help
filling in **Exit Details** (see *Help wanted* below).

> **Alpha software.** Expect rough edges. Builds are **unsigned** — Windows
> SmartScreen will warn on first launch (More info → Run anyway). Signing is
> tracked in #33.

## ✨ New since alpha.5

- **Weighted Travel** — the pathfinder now scores routes by effort, not just the
  number of rooms:
  - **Avoids high-roundtime terrain.** A one-hop open-water swim (15s of
    "flounder" roundtime per stroke) or a cliff/wall climb no longer beats a
    slightly longer dry route over a bridge or through a gate.
  - **Server-uid pacing.** In stretches of identical-looking rooms (lava fields,
    marshes) the walker now tracks the live server room id, so it keeps moving
    instead of stalling where the map can't tell two rooms apart.
  - **Stuck-move recovery.** A per-step watchdog fails a stuck auto-walk
    cleanly (and signals `#goto`-driven scripts) instead of hanging forever.
  - **Cross-zone transitions** follow boundary-room map notes, so a route can
    walk from one zone into the next without stopping at the edge.
- **`#goto` script compatibility** — the engine emits the Genie 4 automapper
  signals (`YOU HAVE ARRIVED!` / `AUTOMAPPER MOVEMENT FAILED` /
  `DESTINATION NOT FOUND`), so power-travel scripts like `travel.cmd` drive the
  mapper correctly instead of hanging in their `matchwait`.
- **Edit Exit — Skill / Environment / Guild** — right-click an exit → **Edit
  Exit** is reorganised into three clear sections, each with dropdowns:
  - **Skill** — required trained ranks (Athletics, Climbing, …) from a
    searchable list of all 53 DR skills.
  - **Environment** — how the exit is traversed (Bridge, Boat, Rope, Ladder,
    Ford, …) plus its RT cost and boat/ferry wait window.
  - **Guild** — guild-restricted routes (Thief passages, Trader caravans, Ranger
    trails, Moon Mage portals) and a minimum level.
- **Fetch skills** — the mapper's "Fetch skills now" banner now sends `info` +
  `exp all`, priming the pathfinder with your guild, circle, and full skill ranks
  in one click.
- **Stability** — `travel.cmd` and other alias/trigger-heavy scripts no longer
  crash the client (#40 — command re-entrancy guard).
- **Live Audit** (`#audit on`) — tees the raw XML stream and parsed events to a
  diagnostic log, so travel/mapper issues can be reproduced from one file.
- **Mapper quality of life** — **Ctrl+Click** a room to walk there (Go Here);
  the Mapper floats by default when you open it without a saved dock location.

## 📋 Help wanted — fill in Exit Details

Weighted Travel is only as good as the data behind it. Map arcs ship today with
**no** skill / environment / timing information, so the pathfinder falls back to
sensible guesses. You can make it precise:

1. Open the **Mapper**, find an exit that needs detail — a river swim, a climb, a
   boat, a guild-only passage.
2. **Right-click the exit → Edit Exit.**
3. Fill in what you know:
   - **Skill** — e.g. the river crossing needs *Athletics ≥ 50* (and note the
     ranks often differ by direction — upstream is harder than down).
   - **Environment** — *Swim* / *Bridge* / *Boat* and its **RT cost** (or wait
     window for scheduled boats).
   - **Guild** — restrict the route to *Thief*, *Trader*, *Ranger*, *Moon Mage*…
4. **Save.** It persists into the zone XML and rides along with the community
   Maps repo, so everyone's routing gets smarter over time.

These fields are Genie 5 extensions that **old Genie 4 clients ignore**, so
edited maps stay fully backward-compatible.

## ✅ What works

Connection (Direct SGE / Lich proxy / dev-replay), the StormFront XML parser and
live GameState, the full Genie 4 `.cmd` script engine plus JavaScript `.js`
scripts, the rules engines (`#alias` / `#trigger` / `#highlight` / `#substitute`
/ `#gag` / `#macro` / `#class` / `#var`) with `.cfg` persistence, the AutoMapper
(click-to-goto, `#goto`, weighted + cross-zone routing), dockable panels with
save/load layouts, the plugin host, and the in-app updater. See the
[README status table](README.md#status) for the full list.

## 🚧 Not working yet / known gaps

- **Unsigned builds** — SmartScreen warning on Windows (#33).
- **macOS / Linux update channels** — the in-app updater self-updates on Windows
  only; other platforms install fresh builds manually for now (#27).
- **Same-description rooms** — server-uid pacing greatly improves walking through
  identical rooms, but routes through them can still occasionally mis-resolve
  (#76 / #77); `#mapper reset` helps. Learning terrain RT from play is future
  work.
- **No light theme** yet (single dark palette, #20); no injuries panel (#18);
  no Familiar/Death/Assess stream tabs (#17); no raw-XML inspector window (#14).

## ⬇️ Downloads

Grab the installer or portable build for your platform from the assets below:

| Platform | Installer | Portable |
|---|---|---|
| Windows | `01-Windows-Genie5-Setup.exe` | `01-Windows-Genie5-Portable.zip` |
| macOS (Apple Silicon) | `02-macOS-Apple-Silicon-Genie5.dmg` | `02-…-Portable.zip` |
| macOS (Intel) | `03-macOS-Intel-Genie5.dmg` | `03-…-Portable.zip` |
| Linux (x64) | `04-Linux-Genie5.AppImage` | — |

**Full changelog:** https://github.com/GenieClient/Genie5/compare/v5.0.0-alpha.5...v5.0.0-alpha.6
