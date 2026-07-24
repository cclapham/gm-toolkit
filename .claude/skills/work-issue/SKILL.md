---
name: work-issue
description: Work a GM Toolkit GitHub issue end-to-end - delegate implementation to the dotnet-avalonia-engineer subagent, re-verify independently, open a PR, get it reviewed by a skeptical AI reviewer on a different model, then get the human's explicit sign-off before merging and closing the issue. Use for any gm-toolkit issue work, not just when explicitly asked to "use the skill".
---

# Work a GitHub issue end-to-end

Repo: `cclapham/gm-toolkit`. Argument is an issue number, or `next` to mean the lowest-numbered open issue that has no linked PR yet.

## 1. Fetch and scope

- `gh issue view <n> --repo cclapham/gm-toolkit --json number,title,body,labels,milestone`
- Read Tasks/Acceptance/Notes carefully. If the issue can't be implemented on its own — e.g. it needs a type or table defined by a different, not-yet-done issue — say so and propose bundling them in one PR, the way #7/#8/#9 (three domain models that only compile together) and #10 (schema + repository mapping split across two issues) were handled. Don't silently implement half of something and call it done.

## 2. Implement

Dispatch to the `dotnet-avalonia-engineer` subagent (`.claude/agents/dotnet-avalonia-engineer.md`, model: sonnet) rather than implementing directly — it carries this repo's architecture rules and "what done looks like" bar as its system prompt, so it doesn't need them re-explained every time. Give it:

- The issue number(s), title(s), and full body (Tasks/Acceptance/Notes).
- Which files/areas are relevant if you already know (e.g. "this only needs `GmToolkit.Data`, look at how `CampaignRepository` is structured").
- An explicit instruction to implement, verify locally (build/format/test — see step 3, it already knows this), and report back what it built and how it verified it, in enough detail that you can relay it to the human without having to re-derive it yourself.

If the issue can't be implemented on its own — e.g. it needs a type or table defined by a different, not-yet-done issue — the subagent should say so rather than silently implementing half of something; if it does, decide whether to bundle issues together (the way #7/#8/#9 and #10 were handled) and re-dispatch with the expanded scope.

Prefer real integration tests against a real temp SQLite file for `Data`-layer work (see `tests/GmToolkit.Data.Tests` for the pattern) over placeholders, covering each of the issue's specific acceptance criteria with at least one test — the subagent's system prompt already says this, but check its report reflects it before moving on.

## 3. Re-verify yourself — don't just relay the subagent's claims

The subagent reports that it built/formatted/tested cleanly. Independently re-run the key commands yourself on the touched projects before trusting that — a report of success is a claim, not a fact, same as it would be from a junior engineer:

- `dotnet build` — 0 warnings. `Directory.Build.props` sets `TreatWarningsAsErrors`, but that only covers C# compiler warnings, not MSBuild-level ones (e.g. `MSB3277` assembly version conflicts) — read the full build output, don't just check the exit code.
- `dotnet format <csproj> --no-restore --verify-no-changes`.
- `dotnet test` on every touched test project.
- If the change touches UI: actually build and run `GmToolkit.Desktop` (`timeout 8 dotnet run --project src/GmToolkit.Desktop`) and ask the user to visually confirm what rendered — there's no screenshot tool in this environment, so this has to be a real question to the user, not an assumption.
- If the change touches CI: don't trust workflow YAML by inspection alone — a real PR run proves it (see step 4).

## 4. Branch, commit, open a PR

- Branch naming: `type/short-description` per CONTRIBUTING.md.
- Commit message ends with `Closes #<n>` (or multiple `Closes #<n>.` lines if bundled), plus the Co-Authored-By/Claude-Session footer already used throughout this repo's history.
- Open the PR via `mcp__github__create_pull_request` with a "Test plan" checklist section.
- Wait for CI: `gh pr checks <n> --watch --interval 15`. All jobs must be green before moving on — don't skip ahead on a pending or partial result.

## 5. Security scan — mandatory, do not skip

Once CI is green and before the skeptical review, invoke the `security-scan` skill in PR mode with this PR's number (`Skill` tool, `skill: "security-scan"`, `args: "<pr-number>"`). It runs deterministic checks (dependency CVEs, secret grep, Dependabot cross-check) plus a skeptical `security-reviewer` subagent pass on a different model, scoped to this diff.

- Any concrete finding from this step gets fixed the same way as a skeptical-review finding (step 6): push a new commit to the same branch, wait for CI again, before moving on. Don't let it slide because it's a separate step from the main review.
- Fold the security-scan's findings into the summary you give the human in step 7 rather than reporting it as a disconnected side-channel.

## 6. Skeptical AI review — mandatory, do not skip

Once CI is green, dispatch a fresh subagent to review the PR before asking the human to look at it:

- `Agent` tool, `subagent_type: "general-purpose"`, `model: "opus"` — deliberately a different/stronger model than whatever implemented the change, so the same reasoning isn't grading its own work.
- Give it the PR number/URL, the issue(s) it claims to close, and an explicit instruction to be skeptical: look for bugs, missed edge cases, scope creep, unjustified design decisions, security issues, and whether the acceptance criteria are genuinely met rather than superficially addressed. Tell it directly not to rubber-stamp.
- Have it report via the `ReportFindings` tool if available to it (severity-ranked, file/line/failure-scenario), otherwise a clear written list.
- Treat any `CONFIRMED` or otherwise credible finding as something to fix — push a new commit to the same branch, wait for CI again — before moving to step 7. Don't merge over a real finding just because it's inconvenient.

For anything that feels like it needs even more scrutiny than one subagent pass (a risky migration, a security-sensitive change, a big architectural pivot), tell the user `/code-review ultra` is available as a heavier, multi-agent cloud review they can trigger themselves — don't try to replicate it yourself.

## 7. Human sign-off — mandatory, do not merge without it

- Summarize for the user: what the issue asked for, what was implemented, the security scan's and AI reviewer's findings and what (if anything) got fixed as a result, and the PR link.
- Explicitly ask the user to confirm before merging. Do not merge on your own judgment alone even if everything looks clean — that's the entire point of this gate. If the user doesn't respond to confirm, don't merge; wait.

## 8. Merge and close

- Squash-merge only after explicit user go-ahead (repo settings already default to squash).
- Pull `main` locally, delete the local and remote feature branch.
- Close the issue(s) with a comment summarizing what was done, referencing the merge commit and CI run — matching the style used for #1 through #11 in this repo's history.
