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
- Windows and Android
- iOS/macOS should compile but are not tested or supported for the MVP

### 6. Non-functional
- Cold start under 2 seconds on a mid-range machine
- All data local in SQLite; no network calls at all
- Core domain + generator logic covered by unit tests

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
- iOS/macOS support
- Theming beyond light/dark

## Definition of done for the MVP

- [ ] Every issue in milestones M0–M4 is closed
- [ ] `dotnet test` green; CI green on Windows
- [ ] A signed-off manual QA pass against the one-sentence test above, on Windows and on a real Android device
- [ ] README screenshots replaced with real ones
- [ ] Tagged `v0.1.0` with release notes and a Windows build attached
