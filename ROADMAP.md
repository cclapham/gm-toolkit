# Roadmap

Milestones are sequential and each has an exit criterion — a thing that is either true or not. Milestones M0–M4 constitute the [MVP](MVP.md); M5+ are post-MVP and deliberately vague until the MVP ships.

| # | Milestone | Exit criterion |
| --- | --- | --- |
| M0 | Foundations | `git clone && dotnet test` works for a stranger; CI green |
| M1 | Domain & data layer | A campaign with PCs and NPCs round-trips to SQLite, proven by tests |
| M2 | Campaign & character management | A GM can manage campaigns and a PC roster through the UI |
| M3 | NPC generator | Generate → reroll a field → save to campaign, in under 10 seconds |
| M4 | MVP release | `v0.1.0` tagged and installable on Windows and Android |
| M5 | Import / export | A campaign survives a round-trip through a JSON file |
| M6 | Custom tables | A GM adds their own generator table without touching source |
| M7 | At-the-table tools | Initiative tracker and session log usable during a live game |
| M8 | Sync & sharing | Same campaign on two devices, and a player-facing read-only view |

---

## M0 — Foundations

Scaffolding, so that every later milestone is boring.

Solution and project structure, `Directory.Build.props` with shared properties and nullable/warnings-as-errors, `.editorconfig`, `.gitignore`, MIT licence, the MAUI Blazor Hybrid host wired to the RCL, xUnit projects, and a GitHub Actions workflow that builds and tests on Windows.

**Exit:** a fresh clone builds and tests green in CI and locally.

## M1 — Domain & data layer

`Campaign`, `PlayerCharacter`, `Npc` and the generator table models in `Core`. EF Core `DbContext`, initial migration, repository implementations in `Data`, DB creation on first run at the platform app-data path, and seeding of the generator tables from embedded JSON.

No UI in this milestone. Tests are the UI.

**Exit:** integration tests create a campaign, add PCs and NPCs, close and reopen the context, and read them back.

## M2 — Campaign & character management

The first milestone you can show someone. App shell and navigation, campaign list / create / edit / delete, campaign selection context, PC roster, PC create/edit form with validation, notes with markdown rendering, and empty states that tell a new user what to do.

**Exit:** a GM can set up a real campaign and its party without touching the database.

## M3 — NPC generator

The differentiator, so it gets its own milestone. Weighted-table generator engine in `Core` with a seedable RNG, generator registry, NPC list and manual CRUD, generator UI with per-field reroll and lock, constraints (name culture, occupation category), edit-before-save, and save into the active campaign.

**Exit:** the ten-second test in [MVP.md](MVP.md) passes.

## M4 — MVP release

Polish and ship. Light/dark theme pass, consistent validation and error handling, search across NPCs, performance check on cold start, Windows packaging, Android APK produced by CI, manual QA pass on a real device, README screenshots, `v0.1.0` tag and release notes.

**Exit:** someone who isn't you installs it and runs a session with it.

---

## Post-MVP (M5+)

Sequenced by what MVP users actually complain about, so treat the order below as a guess rather than a plan.

- **M5 — Import / export.** JSON export of a whole campaign; import with conflict handling. Unblocks backups and sharing prep, and it's the cheapest insurance against schema changes.
- **M6 — Custom tables.** In-app table editor, import/export of table packs, community table sharing via plain files.
- **M7 — At-the-table tools.** Initiative tracker, session log with timestamped notes, quick-reference pinning.
- **M8 — Sync & sharing.** Optional account, cross-device sync, read-only player view. Last, because it's the only thing here that requires a server and therefore ongoing cost and a privacy policy.

Other candidates with no milestone yet: relationship/faction graph, encounter builder, iOS and macOS support, localisation, plugin model for rules systems, a Blazor Web App head reusing `GmToolkit.UI`.
