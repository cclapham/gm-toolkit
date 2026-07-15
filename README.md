# GM Toolkit

> A cross-platform Game Master's companion for tabletop RPGs. Manage campaigns, player characters and NPCs — and generate NPCs on the fly when your players walk into the tavern and ask the barkeep's name.

[![Build](https://github.com/cclapham/gm-toolkit/actions/workflows/build.yml/badge.svg)](https://github.com/cclapham/gm-toolkit/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

**Status:** pre-alpha — working towards the [MVP](docs/MVP.md). Expect breaking changes.

---

## What it does

GM Toolkit is a local-first desktop and mobile app for running tabletop games. It is deliberately **system-agnostic**: it doesn't try to implement any publisher's rules, so it works whether you run D&D, Pathfinder, Call of Cthulhu, Blades in the Dark or something homebrew.

| Area | What you get |
| --- | --- |
| **Campaigns** | Create campaigns, each a container for its own cast, notes and sessions |
| **Player characters** | A roster of PCs with player name, key stats, notes and passive values you look up mid-session |
| **NPCs** | Full CRUD, tagged by role, faction and location, searchable |
| **NPC generator** | Weighted random tables produce a name, appearance, mannerism, motivation and secret — reroll any single field, then save straight into the campaign |

Everything is stored locally in SQLite. No account, no server, no telemetry.

## Why another one?

Most GM tools are either web-only (useless at a table with no wifi), locked to one rules system, or subscription-gated. This one is offline-first, free, MIT-licensed, and built to be forked — see [Forking this project](#forking-this-project).

## Screenshots

_Coming once the MVP UI lands. See [docs/ROADMAP.md](docs/ROADMAP.md)._

---

## Tech stack

- **.NET 10** (LTS)
- **.NET MAUI Blazor Hybrid** — native shell, Razor component UI
- **Razor Class Library** for all UI, so the same components can later run in a Blazor Web App without a rewrite
- **EF Core + SQLite** for local persistence
- **xUnit** for tests
- **GitHub Actions** for CI

### Solution layout

```
gm-toolkit.sln
├── src/
│   ├── GmToolkit.App/          # MAUI Blazor Hybrid host (Android, iOS, macOS, Windows)
│   ├── GmToolkit.UI/           # Razor Class Library — all pages & components
│   ├── GmToolkit.Core/         # Domain models, generator engine, services (no UI, no EF)
│   └── GmToolkit.Data/         # EF Core DbContext, migrations, repositories
├── tests/
│   ├── GmToolkit.Core.Tests/
│   └── GmToolkit.Data.Tests/
├── docs/
│   ├── MVP.md
│   └── ROADMAP.md
└── .github/workflows/
```

The dependency rule is one-way: `App` → `UI` → `Core` ← `Data`. `Core` references nothing of ours. If you find yourself wanting `Core` to know about EF Core or Razor, that's the signal to stop and reconsider.

---

## Getting started

### Prerequisites

1. **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**
2. **MAUI workload:**
   ```bash
   dotnet workload install maui
   ```
   On Linux you can only build/test `Core`, `Data` and `UI` — the MAUI heads need Windows or macOS.
3. **Per-platform extras:**
   - **Windows** — Windows 10 19041+, Developer Mode on, [WebView2 runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
   - **Android** — JDK 17+ and the Android SDK (easiest via the Visual Studio or VS Code MAUI extension installer)
   - **iOS / macOS** — a Mac with current Xcode
4. **An editor** — Visual Studio 2026 with the *.NET Multi-platform App UI development* workload, or VS Code with the *C# Dev Kit* + *.NET MAUI* extensions, or Rider.

### Build and run

```bash
git clone https://github.com/cclapham/gm-toolkit.git
cd gm-toolkit
dotnet restore

# Windows
dotnet build src/GmToolkit.App -f net10.0-windows10.0.19041.0
dotnet run   --project src/GmToolkit.App -f net10.0-windows10.0.19041.0

# Android (emulator or device attached)
dotnet build src/GmToolkit.App -f net10.0-android -t:Run
```

### Tests

```bash
dotnet test
```

`Core` and `Data` tests run on any OS and are what CI gates PRs on. Keep new logic testable by putting it in `Core`.

### Where does my data live?

SQLite file at `FileSystem.AppDataDirectory/gmtoolkit.db`:

| Platform | Path |
| --- | --- |
| Windows | `%LOCALAPPDATA%\Packages\<package-id>\LocalState\` |
| Android | `/data/data/<package-id>/files/` |
| macOS / iOS | `~/Library/Application Support/<bundle-id>/` |

Delete it to reset the app to a clean state.

---

## Forking this project

This repo is set up to be forked and made your own — that's an explicitly supported use, not a grudging one.

**Fork and run:**

1. Click **Fork**, then clone your fork.
2. `git remote add upstream https://github.com/cclapham/gm-toolkit.git` so you can pull in changes later.
3. Follow [Getting started](#getting-started). No secrets or API keys are needed — everything runs locally.

**Rename it to your own thing:** the name appears in the solution/project filenames, the root namespace in `Directory.Build.props`, and the app id + display name in `src/GmToolkit.App/GmToolkit.App.csproj`. A find-and-replace of `GmToolkit` / `gm-toolkit` plus a rename of the folders covers it.

**Common forks we'd expect people to want:**

| You want | Start here |
| --- | --- |
| Your own generator tables (a sci-fi setting, your homebrew world) | `src/GmToolkit.Core/Generation/Tables/*.json` — swap the data, no code changes |
| A new generator category (ships, taverns, factions) | Add a table + register it with `IGeneratorRegistry`; the UI picks it up |
| System-specific character sheets | `GmToolkit.Core` models keep stats as a flexible bag — add a sheet template in `GmToolkit.UI` |
| A web version | `GmToolkit.UI` is already an RCL — add a Blazor Web App project and reference it |
| A different database | Swap `GmToolkit.Data`; the repository interfaces live in `Core` |

**Licence:** MIT. Fork it, sell it, rename it — just keep the copyright notice. Contributions back are welcome but never expected.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Short version: issues labelled [`good first issue`](https://github.com/cclapham/gm-toolkit/labels/good%20first%20issue) are the front door, branch naming is `type/short-description`, and PRs need green CI plus a linked issue.

The work is tracked on the [GM Toolkit project board](https://github.com/users/cclapham/projects/1) and grouped into [milestones](docs/ROADMAP.md).

## Roadmap

- **[MVP](docs/MVP.md)** — campaigns, PCs, NPCs, NPC generator, Windows + Android, local only
- **Post-MVP** — import/export, encounter & initiative tracking, session logs, custom generator tables in-app, optional sync

Full breakdown in [docs/ROADMAP.md](docs/ROADMAP.md).

## Licence

[MIT](LICENSE) © 2026 cclapham
