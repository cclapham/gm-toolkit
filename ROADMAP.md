# Roadmap

Milestones are sequential and each has an exit criterion — a thing that is either true or not. Milestones M0–M4 constitute the [MVP](MVP.md); M5+ are post-MVP and deliberately vague until the MVP ships.

| # | Milestone | Exit criterion | Status |
| --- | --- | --- | --- |
| M0 | Foundations | A fresh clone builds and tests pass for a stranger; CI green | Done |
| M1 | Domain & data layer | A campaign with PCs and NPCs round-trips to SQLite, proven by tests | Done |
| M2 | Campaign & character management | A GM can manage campaigns and a PC roster through the UI | Done |
| M3 | NPC generator | Generate → reroll a field → save to campaign, in under 10 seconds | Done |
| M4 | MVP release | `v0.1.0` tagged and installable on Windows, Linux (desktop and Raspberry Pi 4) and Android | Done |
| M5 | Import / export | A campaign survives a round-trip through a JSON file | Not started |
| M6 | Custom tables | A GM adds their own generator table without touching source | Not started |
| M7 | At-the-table tools | Initiative tracker usable during a live game | Not started |
| M8 | Sync & sharing | Same campaign on two devices, and a player-facing read-only view | Not started |
| M9 | Typed character/NPC systems & distribution | A GM can run a D&D 5e, D&D 5e (2024), Pathfinder 2e, or GURPS campaign with typed, validated PC/NPC stat blocks, and install a community-approved system pack over HTTPS with its integrity verified | Not started |
| M10 | Interactive map drawing | A DM draws a map, saves it against a campaign, and exports it as a usable print-ready file | Not started |
| M11 | Campaign dashboard | Opening a campaign shows party levels, an NPC summary, and current quests at a glance | Not started |
| M12 | Session diary & calendar | A GM schedules, completes, and reviews sessions with planning and summary notes | Not started |

---

## M0 — Foundations

Scaffolding, so that every later milestone is boring.

.NET solution structure (`GmToolkit.Core`, `GmToolkit.Data`, `GmToolkit.UI`, `GmToolkit.Desktop`, `GmToolkit.Android` as separate projects), shared `.editorconfig`, `.gitignore` for .NET/Avalonia, MIT licence, an Avalonia `App` bootstrapping the UI from `GmToolkit.UI`, xUnit test projects for `Core` and `Data`, and a GitHub Actions workflow that builds and tests across `win-x64`, `linux-x64`, `linux-arm64` and Android.

**Exit:** a fresh clone runs `dotnet restore && dotnet build`, builds, and tests pass in CI and locally.

**Status:** done.

## M1 — Domain & data layer

`Campaign`, `PlayerCharacter`, `Npc` and the generator table models in `Core`. sqlite-net-pcl repository implementations in `Data`, DB creation on first run at the platform app-data path, and seeding of the generator tables from embedded JSON (`Resources/GeneratorTables`).

No UI in this milestone. Tests are the UI.

**Exit:** integration tests create a campaign, add PCs and NPCs, close and reopen the database, and read them back.

**Status:** done.

## M2 — Campaign & character management

The first milestone you can show someone. App shell and navigation (Avalonia, MVVM), campaign list / create / edit / delete, campaign selection context, PC roster, PC create/edit form with validation, notes with markdown rendering, and empty states that tell a new user what to do.

**Exit:** a GM can set up a real campaign and its party without touching the database.

**Status:** done.

## M3 — NPC generator

The differentiator, so it gets its own milestone. Weighted-table generator engine in `Core` with a seedable RNG, generator registry, NPC list and manual CRUD, generator UI with per-field reroll and lock, constraints (name culture, occupation category), edit-before-save, and save into the active campaign.

**Exit:** the ten-second test in [MVP.md](MVP.md) passes.

**Status:** done.

## M4 — MVP release

Polish and ship. Light/dark theme pass, consistent validation and error handling, search across NPCs, performance check on cold start (Raspberry Pi 4 included), Windows/Linux packaging (including Raspberry Pi 4 / ARM64), Android APK/AAB produced by CI, manual QA pass on real devices across Windows, Ubuntu, Raspberry Pi 4 and Android, README screenshots, `v0.1.0` tag and release notes.

**Exit:** someone who isn't you installs it and runs a session with it.

**Status:** done. `v0.1.0` is tagged and published: [github.com/cclapham/gm-toolkit/releases/tag/v0.1.0](https://github.com/cclapham/gm-toolkit/releases/tag/v0.1.0).

## M9 — Typed character/NPC systems & distribution

Pluggable, declarative (no code execution, ever) stat schemas for PCs and NPCs, attached at the campaign level so every PC and NPC in a campaign shares the same system. Ships with four worked profiles chosen specifically to stress-test genericity — D&D 5e (2014), D&D 5e (2024 revision), Pathfinder 2e, and GURPS, the last of which has no class/level concept at all and is the real test of whether the schema format is generic rather than secretly D&D-shaped.

Community system-pack distribution (an explicit, opt-in HTTPS client downloading sha256-pinned packs from a separate approval/distribution service) is **paused** until that separate service exists — see [#91](https://github.com/cclapham/gm-toolkit/issues/91)/[#92](https://github.com/cclapham/gm-toolkit/issues/92)/[#93](https://github.com/cclapham/gm-toolkit/issues/93). The schema engine and all four built-in profiles don't depend on it and proceed now.

Tracked as issues [#82](https://github.com/cclapham/gm-toolkit/issues/82)–[#93](https://github.com/cclapham/gm-toolkit/issues/93).

**Exit (active scope):** a GM can run a D&D 5e, D&D 5e (2024), Pathfinder 2e, or GURPS campaign with typed, validated PC/NPC stat blocks, using the four built-in profiles.

**Exit (paused scope):** install a community-approved system pack over HTTPS with its integrity verified — blocked on the separate distribution service.

**Status:** not started.

## M10 — Interactive map drawing

A DM can draw maps interactively within the app (freehand pen, shapes, text, an adjustable square/hex grid), optionally over an imported background image, store them against a campaign, and export them for printing. Prep-time drawing/annotation only — no tokens, no live-play battle-map integration; that stays out of scope, matching MVP.md's original non-goals. A dedicated Raspberry Pi 4 performance check is part of this milestone, not an afterthought, since freehand drawing is a new kind of workload for hardware this app already treats as a hard constraint.

Tracked as issues [#94](https://github.com/cclapham/gm-toolkit/issues/94)–[#101](https://github.com/cclapham/gm-toolkit/issues/101).

**Exit:** a DM draws a map, saves it against a campaign, and exports it as a usable, correctly-scaled print-ready file.

**Status:** not started.

## M11 — Campaign dashboard

A richer at-a-glance view of an active campaign: party roster with levels, an NPC summary, and a curated list of current objectives — a status-based quest/mission-log tracker (active/completed/failed), distinct from the timestamped session-log diary, which is its own milestone (see below).

Tracked as issues [#102](https://github.com/cclapham/gm-toolkit/issues/102)–[#106](https://github.com/cclapham/gm-toolkit/issues/106).

**Exit:** opening a campaign's Dashboard shows party levels, an NPC summary, and current quests at a glance, with sensible empty states for a brand-new campaign.

**Status:** not started.

## M12 — Session diary & calendar

A GM-only planning and review tool: schedule upcoming sessions with planning notes, then write a diary/summary entry once each session actually happens. Starts as a chronological upcoming/past list, not a visual calendar grid. Explicitly excludes any player-facing coordination — invites, availability polling, notifications — which needs M8's sharing infrastructure first, and excludes OS-level reminder notifications, a separate and materially bigger cross-platform undertaking.

Tracked as issues [#107](https://github.com/cclapham/gm-toolkit/issues/107)–[#111](https://github.com/cclapham/gm-toolkit/issues/111).

**Exit:** a GM can schedule a session with planning notes, mark it completed, write up what happened, and review past sessions' diary entries alongside upcoming ones.

**Status:** not started.

---

## Post-MVP (M5+)

Sequenced by what MVP users actually complain about, so treat the order below as a guess rather than a plan.

- **M5 — Import / export.** JSON export of a whole campaign; import with conflict handling. Unblocks backups and sharing prep, and it's the cheapest insurance against schema changes.
- **M6 — Custom tables.** In-app table editor, import/export of table packs, community table sharing via plain files.
- **M7 — At-the-table tools.** Initiative tracker, quick-reference pinning. (The timestamped session log originally sketched here moved to M12 — a session diary deserved its own milestone rather than a line item.)
- **M8 — Sync & sharing.** Optional account, cross-device sync, read-only player view. Last, because it's the only thing here that requires a server and therefore ongoing cost and a privacy policy.
- **M9 — Typed character/NPC systems & distribution.** See above.
- **M10 — Interactive map drawing.** See above.
- **M11 — Campaign dashboard.** See above.
- **M12 — Session diary & calendar.** See above.

Other candidates with no milestone yet: relationship/faction graph, encounter builder, localisation, macOS/iOS support if there's ever a reason to chase it, an Avalonia Browser (WASM) head for browser play (tradeoff: sandboxed storage is a weaker fit for local-first data), Google Play Store distribution for Android (needs an `.aab` build and Play Console/Play App Signing enrollment, additive to the existing sideload APK path, not a replacement for it).
