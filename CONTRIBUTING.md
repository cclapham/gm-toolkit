# Contributing

Thanks for considering it. This project is small and the bar is "does it work and is it tested", not "is it perfect".

## Before you write code

- **Check the [project board](https://github.com/users/cclapham/projects/6)** — anything in *Todo* is fair game.
- **New to the repo?** Filter for [`good first issue`](https://github.com/cclapham/gm-toolkit/labels/good%20first%20issue).
- **Got an idea?** Open an issue first. For anything beyond a small fix, please don't open a surprise PR — the [MVP scope](docs/MVP.md) says no to a lot of good ideas on purpose, and I'd rather tell you that in an issue than after you've spent a weekend on it.
- **Comment on the issue** to claim it, so two people don't do the same work.

## Workflow

1. Fork, then branch from `main`.
2. Branch naming: `type/short-description` — e.g. `feat/npc-reroll-field`, `fix/campaign-delete-cascade`, `docs/readme-android-setup`. Types: `feat`, `fix`, `docs`, `chore`, `test`, `refactor`.
3. Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/): `feat(generator): lock individual fields before reroll`.
4. Open a PR against `main` with `Closes #123` in the description.

## What a PR needs

- [ ] Linked issue
- [ ] Green CI (`dotnet test`)
- [ ] Tests for new logic in `Core` or `Data` — UI changes don't need tests, business logic does
- [ ] No new compiler warnings
- [ ] Scoped to one thing; unrelated cleanup goes in its own PR

## Code conventions

- `.editorconfig` is authoritative.
- Nullable reference types are on. Don't `!` your way out — fix the nullability.
- **Keep `GmToolkit.Core` clean.** No Avalonia types, no SQLite. Domain models, interfaces and pure logic only, in a plain C# class library. This is what makes the project forkable and testable, and it's the rule most likely to be broken by accident.
- Put business logic in `Core`, not in Avalonia views or view models. If a branch of logic is worth testing, it belongs in a service.
- Avoid framework lifecycle types (Avalonia `Window`/`UserControl` code-behind, Android `Activity`) outside `Desktop`/`Android`/`UI` — `Core` and `Data` types should be constructible and testable without a UI runtime.
- Async all the way down for anything touching the database. No blocking waits on async calls.

## Generator tables

Table data lives in `Resources/GeneratorTables/*.json`, embedded and loaded at startup. Adding entries to an existing table is a welcome, zero-risk contribution and doesn't need an issue first — just keep entries generic and setting-neutral, and **don't paste content from published RPG books**. Names, traits and quirks you wrote yourself, or that come from a public-domain or CC-licensed source with attribution, only.

## Typed character/NPC systems

Working on M9 (D&D 5e, Pathfinder 2e, GURPS stat schemas)? See [SYSTEMS.md](SYSTEMS.md) first — it's the RFC for the stat-field schema JSON shape and the derived-formula grammar, and everything in that milestone builds against it rather than re-deciding the shape per issue.

## Reporting bugs

Include: OS and version, app version or commit, what you did, what happened, what you expected. Screenshots help. If it's a data bug, say whether the database was fresh or migrated.

## Code of conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Short version: be decent, assume good faith — this is a hobby project about pretend elves, nothing here is worth being unpleasant over.
