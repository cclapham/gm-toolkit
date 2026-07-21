# Roadmap

Milestones are sequential and each has an exit criterion — a thing that is either true or not. Milestones M0–M4 constitute the [MVP](MVP.md); M5+ are post-MVP and deliberately vague until the MVP ships.

| # | Milestone | Exit criterion |
| --- | --- | --- |
| M0 | Foundations | A fresh clone builds and tests pass for a stranger; CI green |
| M1 | Domain & data layer | A campaign with PCs and NPCs round-trips to SQLite, proven by tests |
| M2 | Campaign & character management | A GM can manage campaigns and a PC roster through the UI |
| M3 | NPC generator | Generate → reroll a field → save to campaign, in under 10 seconds |
| M4 | MVP release | `v0.1.0` tagged and installable on Windows, Linux (desktop and Raspberry Pi 4) and Android |
| M5 | Import / export | A campaign survives a round-trip through a JSON file |
| M6 | Custom tables | A GM adds their own generator table without touching source |
| M7 | At-the-table tools | Initiative tracker and session log usable during a live game |
| M8 | Sync & sharing | Same campaign on two devices, and a player-facing read-only view |

---

## M0 — Foundations

Scaffolding, so that every later milestone is boring.

.NET solution structure (`GmToolkit.Core`, `GmToolkit.Data`, `GmToolkit.UI`, `GmToolkit.Desktop`, `GmToolkit.Android` as separate projects), shared `.editorconfig`, `.gitignore` for .NET/Avalonia, MIT licence, an Avalonia `App` bootstrapping the UI from `GmToolkit.UI`, xUnit test projects for `Core` and `Data`, and a GitHub Actions workflow that builds and tests across `win-x64`, `linux-x64`, `linux-arm64` and Android.

**Exit:** a fresh clone runs `dotnet restore && dotnet build`, builds, and tests pass in CI and locally.

## M1 — Domain & data layer

`Campaign`, `PlayerCharacter`, `Npc` and the generator table models in `Core`. sqlite-net-pcl repository implementations in `Data`, DB creation on first run at the platform app-data path, and seeding of the generator tables from embedded JSON (`Resources/GeneratorTables`).

No UI in this milestone. Tests are the UI.

**Exit:** integration tests create a campaign, add PCs and NPCs, close and reopen the database, and read them back.

## M2 — Campaign & character management

The first milestone you can show someone. App shell and navigation (Avalonia, MVVM), campaign list / create / edit / delete, campaign selection context, PC roster, PC create/edit form with validation, notes with markdown rendering, and empty states that tell a new user what to do.

**Exit:** a GM can set up a real campaign and its party without touching the database.

## M3 — NPC generator

The differentiator, so it gets its own milestone. Weighted-table generator engine in `Core` with a seedable RNG, generator registry, NPC list and manual CRUD, generator UI with per-field reroll and lock, constraints (name culture, occupation category), edit-before-save, and save into the active campaign.

**Exit:** the ten-second test in [MVP.md](MVP.md) passes.

## M4 — MVP release

Polish and ship. Light/dark theme pass, consistent validation and error handling, search across NPCs, performance check on cold start (Raspberry Pi 4 included), Windows/Linux packaging (including Raspberry Pi 4 / ARM64), Android APK/AAB produced by CI, manual QA pass on real devices across Windows, Ubuntu, Raspberry Pi 4 and Android, README screenshots, `v0.1.0` tag and release notes.

**Exit:** someone who isn't you installs it and runs a session with it.

---

## Post-MVP (M5+)

Sequenced by what MVP users actually complain about, so treat the order below as a guess rather than a plan.

- **M5 — Import / export.** JSON export of a whole campaign; import with conflict handling. Unblocks backups and sharing prep, and it's the cheapest insurance against schema changes.
- **M6 — Custom tables.** In-app table editor, import/export of table packs, community table sharing via plain files.
- **M7 — At-the-table tools.** Initiative tracker, session log with timestamped notes, quick-reference pinning.
- **M8 — Sync & sharing.** Optional account, cross-device sync, read-only player view. Last, because it's the only thing here that requires a server and therefore ongoing cost and a privacy policy.

Other candidates with no milestone yet: relationship/faction graph, encounter builder, localisation, plugin model for rules systems, macOS/iOS support if there's ever a reason to chase it, an Avalonia Browser (WASM) head for browser play (tradeoff: sandboxed storage is a weaker fit for local-first data).
