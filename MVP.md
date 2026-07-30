# MVP scope

## The one-sentence test

> A GM can open the app on their laptop, create a campaign, add their four players' characters, add the NPCs they've prepped, and when a player asks the name of a random guard, generate a usable NPC in under ten seconds and save it — all offline.

If a feature isn't needed for that sentence, it isn't in the MVP.

## In scope

### 1. Campaigns
- Create, rename, delete a campaign (name, game system as free text, description, created date)
- Campaign list on launch; selecting one sets the active context for PCs and NPCs
- Delete requires confirmation and cascades to that campaign's PCs and NPCs

### 2. Player characters
- CRUD within a campaign
- Fields: character name, player name, ancestry/species, class/role, level, a small set of key/value stats (system-agnostic, GM defines the keys), passive notes (perception, languages, etc.), free-text notes
- Roster view showing all PCs at a glance — the thing you actually stare at during a session

### 3. NPCs
- CRUD within a campaign
- Fields: name, role/occupation, faction, location, appearance, mannerism, motivation, secret, free-text notes, "known to players" flag
- List view with text search and filter by faction/location

### 4. NPC generator
- Weighted random tables seeded with the app for: names, appearance, mannerism, motivation, occupation, secret
- Generate produces a complete NPC in one action
- **Reroll any individual field** without losing the rest — this is the feature that makes it useful at the table
- Edit before saving; save into the current campaign
- Optional constraints: pick a culture/name-list, pick an occupation category
- Runs entirely offline, deterministic given a seed (so it's testable)

### 5. Platforms
- Windows, Linux (desktop and Raspberry Pi 4 / ARM64), and Android, built from a single .NET solution with a shared Avalonia UI
- The Raspberry Pi 4 target is a real constraint, not a stretch goal — it rules out heavyweight or webview-based UI runtimes up front

### 6. Non-functional
- Cold start under 2 seconds on a mid-range machine (Raspberry Pi 4 included)
- All data local in SQLite (via sqlite-net-pcl); no network calls at all
- Core domain + generator logic covered by unit tests (xUnit)

## Explicitly out of scope for MVP

Written down so they can be said "no" to quickly:

- Any cloud sync, account, or multi-device support
- Sharing a campaign with players or a player-facing view
- Dice roller
- Initiative / encounter / combat tracker
- Maps, tokens, VTT integration (Roll20, Foundry)
- Session logs and timeline
- Import from PDF, D&D Beyond, or any other tool
- Rules-system-specific stat blocks or validation
- Custom generator tables editable in-app (JSON files are edit-by-hand in the fork for now)
- Portrait/image generation or attachment
- Localisation
- Theming beyond light/dark

## Definition of done for the MVP

- [x] Every issue in milestones M0–M4 is closed
- [x] `dotnet test` (xUnit) green; CI green
- [x] A signed-off manual QA pass against the one-sentence test above, on each of Windows, Ubuntu Linux, Raspberry Pi 4 and Android
- [x] README screenshots replaced with real ones
- [x] Tagged `v0.1.0` with release notes and build artifacts for Windows, Linux x64, Linux ARM64 and Android attached — [github.com/cclapham/gm-toolkit/releases/tag/v0.1.0](https://github.com/cclapham/gm-toolkit/releases/tag/v0.1.0)

**The MVP has shipped.** M5+ (see [ROADMAP.md](ROADMAP.md)) is fair game.
