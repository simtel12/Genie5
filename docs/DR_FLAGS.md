# DR `flags` verb — parser-relevant reference

The DragonRealms `flags` verb (`FLAG {flag_name} {on|off}` to change one) toggles
per-character server settings the game consults when emitting output. A subset of
them reshape the XML/text stream `DrXmlParser` reads, so if a player has one of
those in an unexpected state the parser can misbehave (missing room titles,
short combat lines, an altered prompt, and so on).

This file documents (1) the full flag set, (2) which flags actually affect the
parser, and (3) the states the parser is **verified against** — the baseline the
connect-time probe compares to.

> Verified against a live StormFront session on **2026-07-09** (character
> Renucci). The real verb reports **35 flags** — the Elanthipedia
> [Flags_command](https://elanthipedia.play.net/Flags_command) page was stale
> (listed 32; missing `StatusPrompt`/`HideLogin`, and its `Dialogs`/`Inventory`/
> `BriefExp` don't exist). Re-capture with `#audit xmlhunting` → `flags` if DR
> changes the set.

## Output format

Plain-text mono table on the **main** stream — there is **no** dedicated `<flag>`
XML element, so state must be read from the display text:

```
Usage
  FLAG {flag_name} {on|off}
Flag names may be abbreviated.
Example
  FLAG LOGON ON
  FLAG LOGON OFF
  Flag            Status  Behavior for this setting
  LogOn              ON   Show logon messages.
  RoomBrief          OFF  Display the full text of the room description.
  …
For other setting options, see AVOID, SET, and TOGGLE.
```

Note the **Status** column describes the *current effect*, so negative-sense
flags read "inverted": `RoomBrief OFF` = full descriptions, `AvoidX OFF` = allow,
`HideX OFF` = visible. For parsing we only take `name → ON/OFF` and interpret
meaning ourselves.

## Stream-affecting flags (the parser cares)

These 11 change what the parser sees. The connect-time probe warns when any
differs from the **Verified** column.

| Flag | Verified | If flipped, the parser input changes… |
|---|---|---|
| `RoomNames`       | ON  | room-title component disappears |
| `Description`     | ON  | room-desc `<preset id='roomDesc'>` disappears |
| `RoomBrief`       | OFF | brief (truncated) room text instead of full |
| `BattleBrief`     | ON  | combat-line verbosity |
| `CombatBrief`     | ON  | combat-line verbosity (own vs others) |
| `MonsterBold`     | ON  | creatures wrapped in `<pushBold>` — see #160 / `BoldElementTests` |
| `StatusPrompt`    | ON  | **status text prepended to the prompt line** (`PromptEvent`) |
| `ConciseThoughts` | OFF | thought/gweth message length |
| `HidePreStrings`  | OFF | other players' titles in room LOOKs / "Also here" |
| `HidePostStrings` | OFF | elaborate LOOK / room-window output |
| `ShowRoomID`      | ON  | room IDs shown on LOOK |

## Cosmetic / social flags (enumerated, not warned)

No stream-shape impact — they add/remove message lines or gate social actions,
but don't change the structure the parser depends on:

`LogOn`, `LogOff`, `Disconnect`, `ShowDeaths`, `Inactivity`, `Portrait`,
`AvoidJoiners`, `AvoidHolders`, `AvoidDancers`, `AvoidWhispers`, `AvoidDraggers`,
`AvoidTeachers`, `AvoidSinging`, `NoHarnessShare`, `HarnessWarning`,
`HarnessVerbose`, `AutoSneak`, `HideLogin`, `DeathLocation`, `HideMyCusLogin`,
`HideOtCusLogin`, `HideTrivia`, `SkinKills`, `LootKills`.

## How Genie 5 uses this

- **Probe:** on connect (StormFront/Lich, not Wizard) `GenieCore` arms
  `DrXmlParser.BeginFlagsCaptureWindow()` then silently sends `flags`. The
  response is parsed into a `FlagsReportEvent` and **suppressed from display**,
  so the login stays quiet. Only recognised report lines (a known flag name +
  ON/OFF, or the fixed header/boilerplate/footer) are swallowed — interleaved
  game text is never eaten.
- **Warning:** `GenieCore.HandleFlagsReport` compares the report against
  `VerifiedFlagBaseline` (the **Verified** column above) and echoes one advisory
  line naming any stream-affecting flag that deviates. Silent when all match.
- **Toggle:** `#config flagscheck off` disables the probe (default on). A
  user-typed `flags` is never armed, so it always displays normally.

Source: `DrXmlParser` (`KnownFlagNames`, `BeginFlagsCaptureWindow`,
`TryCaptureFlagsLine`), `GenieCore` (`VerifiedFlagBaseline`,
`HandleFlagsReport`), tests in `tests/Genie.Core.Tests/FlagsProbeTests.cs`.
