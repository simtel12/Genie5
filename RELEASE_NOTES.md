# Genie 5 — v5.0.0-alpha.6.1

The **Secure Login** release. Genie now signs in over an **encrypted TLS
connection** — the same secure path Lich 5 uses — and falls back to the legacy
plaintext login only if the secure port is blocked. A **padlock in the title
bar** tells you which you got. This point release also lands a clutch of fixes
on top of [Weighted Travel](https://github.com/GenieClient/Genie5/releases/tag/v5.0.0-alpha.6):
the updater no longer freezes at 70%, your config files stay human-readable, and
`#var` stops chattering when scripts set variables.

> **Alpha software.** Expect rough edges. Builds are **unsigned** — Windows
> SmartScreen will warn on first launch (More info → Run anyway). Signing is
> tracked in #33.

## ✨ New since alpha.6

- **Secure (TLS) login.** Genie authenticates over TLS (`eaccess.play.net:7910`,
  certificate-pinned) by default, so your password travels inside an encrypted
  tunnel instead of the old lightly-obfuscated plaintext scheme (#61).
  - **Padlock indicator.** The title bar shows **🔒** with *"Connected over TLS
    (encrypted)"* on a secure login, or **🔓** with *"login was obfuscated, not
    encrypted"* when it had to fall back.
  - **Automatic fallback.** If the secure port is blocked (firewall, network
    filter), Genie doesn't fail the login — it drops to the legacy plaintext
    path and shows the 🔓 so you know what happened.
- **Clearer connection failures.** A failed or refused login now surfaces the
  actual reason in the game window (bad password, already-logged-in, billing,
  timed-out…) instead of a generic error.
- **`#config conndebug`** — opt-in connect trace. Turn it on and the next login
  prints each protocol step (TLS handshake, key exchange, auth, character list,
  game select) with timings into the game window, so a stalled login can be
  pinned to an exact step and pasted into a bug report. Off by default.
- **Updater: no more frozen 70%** (#60) — the in-app Core updater now reports
  real phase-based progress and has a stall watchdog, so it no longer appears to
  hang partway through an update.
- **Quieter `#var` / `#tvar`** — variables echo *"Variable set:"* only when you
  type the command yourself, not every time a script, trigger, or alias sets one
  (community report).
- **Readable config files** (#78) — settings are written with a relaxed JSON
  encoder, so regex patterns and non-ASCII (UTF-8) text in your config stay
  human-readable instead of being escaped into `\u00NN` soup. Thanks to VTCifer
  for the report.

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

**Full changelog:** https://github.com/GenieClient/Genie5/compare/v5.0.0-alpha.6...v5.0.0-alpha.6.1
