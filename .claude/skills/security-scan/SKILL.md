---
name: security-scan
description: Security scan for GM Toolkit — audits either a PR's diff or the whole repo for real, exploitable issues (dependency CVEs, SQL/path-handling bugs, secrets, insecure deserialization), using deterministic tooling plus a skeptical AI reviewer on a different model. Use for a PR security gate or a periodic whole-repo sweep — not a substitute for `/security-review` or `/code-review ultra`, which are heavier general-purpose options.
---

# Security scan

Repo: `cclapham/gm-toolkit`. Argument is a PR number (scan that PR's diff) or empty/`repo` (scan the whole repo at the current branch's HEAD).

This is a lightweight, repo-tuned security gate — cheap deterministic checks plus one skeptical model pass, scoped to what's actually exploitable in a local-first, networkless, single-user .NET/Avalonia app. For anything higher-stakes (a risky migration, something that adds a network layer or auth, or you just want more eyes), tell the user `/code-review ultra` is available as a heavier multi-agent cloud review — don't try to replicate that here.

## 1. Scope the scan

- **PR mode** (arg is a number): `gh pr diff <n> --repo cclapham/gm-toolkit` for the changed lines, `gh pr view <n> --repo cclapham/gm-toolkit --json title,body,files` for context. Only the touched projects need the dependency-vulnerability check in step 2, but read surrounding (unchanged) code for context same as the `work-issue` skeptical review does.
- **Repo mode** (no arg, or `repo`/`full`): scan the working tree as checked out. Note the current branch and latest commit in your findings so results are reproducible.

## 2. Deterministic checks first — cheap, don't waste the model pass on what a tool already answers

Run these yourself before dispatching the subagent:

- **Dependency CVEs**: `dotnet list <path-to-csproj> package --vulnerable --include-transitive` for every project in scope (all of them in repo mode; only touched ones plus their direct dependents in PR mode). Requires a prior `dotnet restore` if not already restored.
- **Secret scanning**: grep the diff (PR mode) or full tree (repo mode) for common secret patterns — API keys, connection strings, private key headers (`-----BEGIN`), tokens matching known vendor formats. If `mcp__github__run_secret_scanning` or `mcp__github__get_me`-adjacent GitHub secret-scanning is available for this repo, check its results too rather than only relying on grep.
- **GitHub Dependabot alerts**: if reachable via the `gh` CLI or MCP GitHub tools, check for open Dependabot alerts on the repo as a cross-check against the local `dotnet list package --vulnerable` output — the two can disagree (e.g. a transitive package pinned centrally that Dependabot doesn't resolve the same way).

Note any findings from this step plainly — they're already concrete, don't need the subagent to "confirm" them, just to have the context if related to something it finds.

## 3. Dispatch the skeptical reviewer

Dispatch the `security-reviewer` subagent (`.claude/agents/security-reviewer.md`, model: opus — deliberately a different/stronger model than whatever wrote the code under review). Give it:

- The scan mode (PR diff or whole repo) and the scope from step 1.
- The results of step 2's deterministic checks, so it doesn't re-derive them.
- An explicit instruction to focus on this app's real attack surface (it already has this as its system prompt, but reiterate the specific area if you already suspect something — e.g. "this PR touches the corrupt-file recovery path in `GmToolkitDatabase`, look hard at the path-handling there").
- An instruction to report which categories it checked and found clean, not only findings — so the summary you give the user isn't just a list of problems with no indication of what was actually covered.

## 4. Consolidate and report — do not silently fix

Combine step 2's deterministic findings with the subagent's report into one severity-ranked list for the user. This skill does not fix anything itself:

- If nothing turned up: say so plainly, list what was checked, and stop.
- If something did: report it to the user with file/line and a concrete failure/exploit scenario per finding. Point out that fixes should go through the normal path — the `work-issue` skill's dotnet-avalonia-engineer subagent for anything requiring a code change, followed by re-verification and a fresh CI run, the same as any other change to this repo. Don't push a fix directly from this skill without the user asking for one.
- If a finding is disputed or low-confidence, say so rather than presenting it with the same weight as a confirmed one — the point of this gate is signal, not a maximal-length list.
