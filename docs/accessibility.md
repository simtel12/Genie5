# Accessibility — screen-reader labelling

Genie 5's Avalonia UI carries `AutomationProperties` attached properties so
screen readers (NVDA, Narrator, VoiceOver) can identify controls and follow
low-churn game state. This page documents the labelling pattern, where labels
belong, the live-region policy, per-platform support, and a manual NVDA test
protocol.

## The labelling pattern

Three attached properties, all set directly in AXAML:

| Property | Meaning | Example |
|---|---|---|
| `AutomationProperties.Name` | What the control **is** | `"Command input"`, `"Health"` |
| `AutomationProperties.HelpText` | What the control **does** — mirrors the control's `ToolTip.Tip` where one exists | `"Stop this script (#stop)"` |
| `AutomationProperties.AutomationId` | Stable programmatic handle for UI automation / testing. Used **only** on the command input (`CommandInput`) and the Send button (`SendButton`) in `MainWindow.axaml` | `"CommandInput"` |

Static example (icon-only button):

```xml
<Button Content="✕"
        AutomationProperties.Name="Stop script"
        AutomationProperties.HelpText="Stop this script (#stop)"
        ToolTip.Tip="Stop this script (#stop)"
        Command="{Binding ...}"/>
```

Dynamic example (value-bearing control): bind `Name` with a `StringFormat`
that **reuses the exact binding path the control's `Text` already uses** — do
not invent new ViewModel properties for accessibility:

```xml
<TextBlock Text="{Binding Vitals.LeftHand, FallbackValue=Empty}"
           AutomationProperties.Name="{Binding Vitals.LeftHand, StringFormat='Left hand: {0}'}"
           AutomationProperties.LiveSetting="Polite"
           ... />
```

Additional conventions:

- Buttons and menu items with **text content** need no `Name` — the
  automation peer derives it from `Content` / `Header` automatically. Only
  glyph-only buttons (`⏸ ✏ ✕ ▲ ▼ ⚙ 📌 ⬈`) and controls with *composite*
  content (a CheckBox whose content is a StackPanel) need an explicit `Name`.
- `ProgressBar`s get just a `Name` (`Health`, `Mana`, `Fatigue`, `Spirit`,
  `Concentration`, `Spell preparation`, …). Screen readers read the current
  value themselves via the RangeValue pattern — do not bake the number into
  the `Name`.
- Cryptic abbreviation badges (stance `OFF/ADV/FWD/NEU/GRD/DEF`, status
  `HIDE/INVIS/STUN/WEB/JOIN/BLEED/POIS/DIS`) keep their visible text as the
  derived name and carry the tooltip text as `HelpText`.
- Dialog fields mirror their adjacent visible label (`Account`, `Password`,
  `Character`, `Instance`, `Data folder`, …) so the spoken name matches what a
  sighted helper sees on screen.

## Labels go on real controls, not layout containers

`AutomationProperties.Name` belongs on controls that produce an automation
peer: `TextBox`, `TextBlock`, `Button`, `ToggleButton`, `CheckBox`,
`RadioButton`, `ComboBox`, `ListBox`, `Slider`, `ProgressBar`, `ColorPicker`,
and similar.

**Never** put it on `Border`, `Grid`, `StackPanel`, `DockPanel`, `WrapPanel`,
`Canvas`, or other pure layout panels — they have no automation peer, so the
property is silently ignored. When a badge is a `Border` wrapping a
`TextBlock` (the hands-strip status chips), the label/help text goes on the
inner `TextBlock`, not the `Border` that happens to carry the `ToolTip`.

## LiveSetting policy

`AutomationProperties.LiveSetting="Polite"` marks a control as a live region:
the screen reader announces changes without stealing focus, when it next has
idle time.

Use it **only on low-churn dynamic values**:

- ✅ Left hand contents (`Vitals.LeftHand`)
- ✅ Right hand contents (`Vitals.RightHand`)
- ✅ Prepared spell (`Vitals.PreparedSpellLabel` / `PreparedSpell`)

Do **not** use it on:

- ❌ Roundtime — ticks several times a second; a live region would flood the
  speech queue. It has a bound `Name` ("Roundtime: N.N seconds") so it reads
  correctly when *navigated to*, but never self-announces.
- ❌ Vitals bars — combat changes them constantly.
- ❌ Anything inside a stream / game-text area — the game window can emit
  dozens of lines per second. Reading game text aloud is the job of the
  built-in TTS (`#tts`), which the user controls, not of UIA live regions.

`Assertive` is not used anywhere.

## Platform support matrix

| Platform | Avalonia 11.3 accessibility backend | Status |
|---|---|---|
| Windows | UI Automation (UIA) | **Full** — NVDA and Narrator read names, help text, range values, and polite live regions. Primary supported target. |
| macOS | NSAccessibility bridge | **Partial** — names and roles surface to VoiceOver; live regions and some patterns are incomplete in Avalonia's bridge. |
| Linux | none (no AT-SPI in Avalonia 11.3) | **Not available** — Orca cannot see the UI at all. The built-in text-to-speech (`#tts`, `#speak`) is the accessibility path on Linux. |

Because of the Linux gap (and as a user-controllable firehose filter
everywhere), the `#tts` game-text speech pipeline remains the primary
accessibility feature for actual gameplay; the UIA labels in this pass make
the *chrome* (input, vitals, hands, dialogs, panels) navigable.

## Manual NVDA test protocol (Windows)

Prerequisites: NVDA running, a build of Genie 5, a recorded session for
DevReplay.

1. **Launch** Genie 5. NVDA should announce the window title.
2. **Tab to the command input.** NVDA reads "Command input, edit" (plus the
   help text on a second `NVDA+Tab` / when help-text reading is enabled).
   Tab once more: "Send, button".
3. **Object-navigate the hands strip** (`NVDA+Numpad arrows` in object review,
   or mouse-hover): the left-hand value reads "Left hand: …", right hand
   "Right hand: …", prepared spell "Prepared spell: …". Stance/status badges
   read their abbreviation with the full explanation as help text.
4. **Object-navigate the vitals strip**: each bar reads as
   "Health, progress bar, N %" (likewise Mana / Fatigue / Spirit /
   Concentration). The Vitals dock panel bars read the same
   (Stamina in place of Fatigue, matching that panel's visible label).
5. **Open the Connect dialog** (File ▸ Connect…). Tab through the fields:
   Profile, Profile name, Connection mode, Data folder, Instance, Account,
   Password, Character, Fetch characters — each reads its label; Password is
   masked.
6. **Run a DevReplay session.** As the recording plays, hand contents and
   prepared-spell changes are announced politely (after current speech, no
   focus steal). Roundtime must **not** self-announce while ticking —
   navigate to the RT badge to hear the current value on demand.
