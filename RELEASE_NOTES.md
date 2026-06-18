# Genie 5 — v5.0.0-alpha.6.2

The **Type-Ahead** release. Genie now shows a live **type-ahead counter** in the
command bar so you can see how many commands are queued ahead of the game, and
that limit finally matches your **account tier** (and self-corrects from the
server). This point release also smooths out **script editing**: `#edit` opens
your chosen editor — including the Genie 4 `#config editor` setting that used to
be ignored — and creates a new script for you when one doesn't exist yet.

> **Alpha software.** Expect rough edges. Builds are **unsigned** — Windows
> SmartScreen will warn on first launch (More info → Run anyway). Signing is
> tracked in #33.

## ✨ New since alpha.6.1

- **Type-ahead pip counter** — a small counter in the command bar (between
  the roundtime badge and the input box) shows the per-account type-ahead cap as
  pips: **filled = commands queued ahead of the game, hollow = free slots**.
  It's dim when idle and turns **amber** when you've hit the cap, so you can see
  at a glance when the game is lagging behind your typing.
- **Tier-accurate type-ahead** — the type-ahead limit is now seeded from
  your **account tier** (free = 1 line, premium = 2, premium + LTB = 3) instead
  of a hardcoded value, and **self-calibrates** if the server reports a different
  cap (*"you may only type ahead N lines"*). This stops the auto-walker's batched
  movement from overrunning the buffer on free/premium accounts.
- **Smarter `#edit`**:
  - **Editor selection that works** — `#edit` (and the ✏ icon on the Script Bar)
    now open your configured editor via a clear order: **Display Settings →
    Editor Path**, then the Genie 4 **`#config editor`** setting, then your OS
    default text editor. (`#config editor` was previously accepted but silently
    ignored.)
  - **Create-on-edit** — `#edit <name>` on a script that doesn't exist now
    **creates it** (Genie 4 parity). Give an extension (`#edit foo.js`) and that
    type is created directly; leave it off (`#edit foo`) and a small dialog asks
    which supported type to make (`.cmd`, `.inc`, or `.js`).

## ✅ What works

Connection (Secure SGE / Lich proxy / dev-replay), the StormFront XML parser and
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

**Full changelog:** https://github.com/GenieClient/Genie5/compare/v5.0.0-alpha.6.1...v5.0.0-alpha.6.2
