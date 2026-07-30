# Typed character/NPC systems

RFC output of [#82](https://github.com/cclapham/gm-toolkit/issues/82). Defines the stat-field schema JSON shape and the derived-formula grammar that [#83](https://github.com/cclapham/gm-toolkit/issues/83) (the engine) and [#84](https://github.com/cclapham/gm-toolkit/issues/84)–[#87](https://github.com/cclapham/gm-toolkit/issues/87) (the four built-in system profiles) build against. Mirrors [CONTRIBUTING.md](CONTRIBUTING.md)'s "Generator tables" section in spirit: this is where the data shape lives and what the rules around it are.

**Deferred, not covered here:** the client-side distribution API contract (`GET {baseUrl}/systems`, the pack `downloadUrl` response) and the sha256 download-verification flow, both originally scoped in #82. Per [the comment on #82](https://github.com/cclapham/gm-toolkit/issues/82#issuecomment-5131472307), network distribution ([#91](https://github.com/cclapham/gm-toolkit/issues/91)/[#92](https://github.com/cclapham/gm-toolkit/issues/92)/[#93](https://github.com/cclapham/gm-toolkit/issues/93)) is paused until a separate approval/distribution service project exists — designing the wire format against a service that doesn't exist yet risks designing against the wrong shape. Everything below (the schema format itself) is active and unblocked, and is written so that format never has to change to accommodate that later work — a system pack is a JSON file whether it arrives in-box or downloaded.

**A note on how "safe" this document claims to be:** a literal implementation of an earlier draft of this grammar was adversarially tested — hostile formulas, hostile regexes, pathological nesting — against a from-scratch implementation of exactly what's written below. That review is why the "resource limits" subsection exists and why the numbers in it are specific rather than left to #83 to guess at. The distinction that came out of it, stated up front: "this grammar can't execute arbitrary code" and "this grammar can't be abused to exhaust memory/CPU/stack" are two separate claims. This document makes both, but only the first one is true by construction alone — the second needed explicit bounds, which are now part of the format's contract, not an implementation detail.

## Attachment point

A `CharacterSystem` attaches to a `Campaign` via `Campaign.CharacterSystemId` — a nullable `string?`, not a `Guid`: the pack's own slug-shaped `id` (see the envelope section below) is already a stable, natural identifier, so there's no reason for a synthetic id layer on top of it. Shared by every `PlayerCharacter` and every `Npc` in that campaign; there is no per-character or per-NPC override — one campaign, one system, or none at all (`CharacterSystemId: null`). A campaign with no system attached keeps today's freeform `Dictionary<string, string>` behavior unchanged (the "generic" system, see #83); typed systems are additive, not a breaking migration.

**Independent of `Campaign.GameSystem`.** The existing free-text `GameSystem` field is untouched by any of this — it stays exactly what it is today, a display label the GM can set to anything ("D&D 5e," "homebrew," blank), regardless of whether a typed `CharacterSystemId` is attached or which pack it points to. The two are deliberately independent, not kept in sync: a GM can set `GameSystem` to `"GURPS"` while `CharacterSystemId` stays `null` (fully freeform stats), or leave `GameSystem` blank while running the typed GURPS profile. Neither is derived from, or validated against, the other.

**NPC stats storage doesn't exist yet.** `Npc.cs` has no `Stats` dictionary today, so the entire `npcFields`/monster-block half of this format has nowhere to persist to until #88 ships `NpcRow.StatsJson`/`Npc.Stats`, mirroring the pattern `PlayerCharacterRow.StatsJson`/`PlayerCharacter.Stats` already established. That's #88's own scoped task, not duplicated here — this section just cross-references it so this RFC isn't mistaken for having left a gap.

**Where typed values actually live, once #88 ships:** `PlayerCharacter.Stats` and the future `Npc.Stats` — both still a plain `Dictionary<string, string>`, unchanged in type — remain the storage. Every field's value, including a `repeating-group`'s rows (serialized as a JSON array under that field's key), is stored as its string-serialized form in that same dictionary; the schema engine is a typed *view* over a string bag, not a new storage mechanism layered beside it. **A `derived` field's value is never persisted** — only its input fields are. A `derived` value is always recomputed at load/display time by the memoized evaluator (see "Resource limits" below). This isn't an oversight: persisting a computed value risks staleness the moment an input changes (editing a GURPS `hpAdjustment` shouldn't require also finding and rewriting a now-stale stored `hp`), and "never player-entered directly" already means a `derived` field has no state of its own to persist in the first place.

**Fixed roster fields stay fixed roster fields.** `PlayerCharacter.Ancestry`, `.Class`, and `.Level` (a plain `int`) are the app's own roster-display fields — used by the PC list/roster view regardless of which system, if any, is attached — and are independent of a system's own schema fields of similar meaning. A D&D pack's `pcFields` defining its own top-level `level` field (see below) is not a conflict or a duplication bug: they're two different things that happen to share a name and, usually, a value. #83/#89 should not attempt to sync, dedupe, or single-source these — a GM entering "Level 5" in both the roster field and the schema's own `level` field is an accepted redundancy, not a gap this RFC needs to close.

## Field types

Every field in a `CharacterSystem` is a `StatFieldDefinition`. All seven types below share three properties:

| Property | Type | Notes |
| --- | --- | --- |
| `key` | string | Stable identifier. `^[a-zA-Z_][a-zA-Z0-9_]*$`. This is both the dictionary key stats are stored under and the identifier derived formulas reference. Never renamed once a pack ships — renaming is a new field. |
| `label` | string | Display name shown to the GM/player. |
| `type` | string | Discriminator: `number`, `text`, `boolean`, `enum`, `derived`, `repeating-group`, `free-text-block`. |

`helpText` (string, optional) is also common — a short GM-facing description of what the field means in that system — but isn't load-bearing for validation.

### `number`

```json
{ "key": "st", "label": "Strength (ST)", "type": "number", "min": 1, "max": 20, "precision": 0 }
```

- `min` / `max` (optional) — an inclusive **validation range**: a value outside it is rejected as invalid input, the same way an out-of-range value typed into any form field would be. This is a different operation from `derived`'s `min`/`max` below (a clamp, not a rejection) even though the property names and JSON shape are identical — see the `derived` field type below for the distinction stated from that side too.
- `precision` (optional, default `0`) — decimal places the value is stored/displayed at.
- `step` (optional) — a UI increment hint (e.g. spinner step); not a validation rule.
- `default` (optional) — the value a freshly-created character starts with, before the GM edits it. This is what backs "the adjustment field idiom" below being able to say a field "defaults to `0`" as an actual schema property, not just a convention asserted in prose.

### `text`

```json
{ "key": "playerName", "label": "Player", "type": "text", "maxLength": 100 }
```

- `maxLength` (optional, default `500` — distinct from `free-text-block`'s default of `4000`, since `text` is for short labels/names and `free-text-block` is where prose belongs; except see below).
- `pattern` (optional) — a regex the value must match (e.g. constraining a code or short ID field). Most `text` fields set neither and just take `maxLength`. **`pattern` must be written in the `RegexOptions.NonBacktracking` dialect** (.NET 7+), not the default backtracking regex engine, and **a field that sets `pattern` must also set `maxLength`** — see "Resource limits" under the derived-formula grammar section below for why (short version: a full backtracking engine plus unbounded input is a catastrophic-backtracking / ReDoS vector, and this format gave community packs exactly that combination in an earlier draft).

### `boolean`

```json
{ "key": "isProficientInPerception", "label": "Proficient (Perception)", "type": "boolean" }
```

No extra validation properties — a checkbox is always valid.

### `enum`

```json
{
  "key": "perceptionProficiencyRank",
  "label": "Perception Proficiency",
  "type": "enum",
  "options": ["Untrained", "Trained", "Expert", "Master", "Legendary"]
}
```

- `options` (required, non-empty) — the fixed, closed list of valid values, in display order. A stored value not in `options` is invalid, full stop; there's no "other, please specify."

### `derived`

A value computed from other named fields via an arithmetic formula — never player-entered directly.

```json
{
  "key": "basicSpeed",
  "label": "Basic Speed",
  "type": "derived",
  "formula": "(ht + dx) / 4 + speedAdjustment",
  "precision": 2
}
```

- `formula` (required) — see [the grammar](#the-derived-formula-grammar) below.
- `precision`, `rounding` (both optional) — same numeric-presentation properties as `number`, plus `rounding`: `none` (default) | `floor` | `ceiling` | `round` | `truncate` — see "Rounding is field metadata" below for exactly what each does.
- `min` / `max` (optional) — a **clamp** on the computed result: a value outside the range is pulled to the nearest bound, not rejected. This is deliberately a different operation from `number`'s `min`/`max` above, despite sharing a property name and JSON shape — a `derived` field has no "invalid input" to reject in the first place, since nothing was ever typed into it; the clamp is a sanity bound on a formula's output, not input validation.

### `repeating-group`

A variable-length list of structured rows — not a flat key/value pair. Used for a monster's attack list, GURPS's individually-bought skills, a PC's per-skill proficiencies, anything where "how many" varies per character.

```json
{
  "key": "traits",
  "label": "Advantages, Disadvantages & Quirks",
  "type": "repeating-group",
  "itemFields": [
    { "key": "name", "label": "Name", "type": "text", "maxLength": 100 },
    { "key": "category", "label": "Category", "type": "enum", "options": ["Advantage", "Disadvantage", "Quirk", "Perk"] },
    { "key": "pointCost", "label": "Point Cost", "type": "number", "min": -50, "max": 50 },
    { "key": "description", "label": "Description", "type": "free-text-block", "maxLength": 1000 }
  ]
}
```

- `itemFields` (required, non-empty) — a `StatFieldDefinition[]` describing one row. May contain `number`/`text`/`boolean`/`enum`/`free-text-block` fields. May **not** contain another `repeating-group` — no nested lists, no exceptions. May **not** contain a `derived` field either: `derived` is a top-level-only field type (see "Scope resolution" under the derived-formula grammar) — every one of the four validation sketches below that tried a row-scoped derived value hit the same wall (see the GURPS section's "Where derived can't reach"), so the format doesn't carry the feature at all rather than keeping untested machinery around for it. One level of `repeating-group` is sufficient for every system sketched below and keeps the evaluator and the UI form generator simple; anything that would otherwise want a nested list (an advantage with its own sub-modifiers, say) gets a `free-text-block` description instead — prose is the standing answer for anything genuinely nesting-shaped, not a reason to add a second level to the format.
- `minItems` / `maxItems` (optional, engine default `100` if omitted) — row-count bounds; both usually omitted (an empty list is valid — a character with no advantages yet). Whatever a pack declares (or omits), the engine enforces its own hard ceiling regardless — see "Resource limits" below.
- Row keys (the `key`s inside one group's `itemFields`) only have to be unique *within that group* — two different `repeating-group`s can both have a field called `name` (this doc's own D&D sketch below has `name` in `skills`, `traits`, `actions` and `legendaryActions` independently) because each group's rows are their own naming scope. Since a row can't contain a `derived` field, a row's fields never need to resolve a name against anything outside their own row at all — see "Scope resolution" under the derived-formula grammar for the (now simpler) full rule.

### `free-text-block`

Prose, not a structured value — a monster's trait description, a quirk's flavor text.

```json
{ "key": "notes", "label": "Notes", "type": "free-text-block", "maxLength": 4000 }
```

- `maxLength` (optional, default 4000) — the only validation; no `pattern`, this is prose.

## The `CharacterSystem` envelope

```json
{
  "formatVersion": 1,
  "id": "gurps-4e",
  "name": "GURPS Fourth Edition",
  "version": "1.0.0",
  "author": "GM Toolkit",
  "description": "Point-buy attributes, derived secondary characteristics, traits and skills.",
  "pcFields": [ /* StatFieldDefinition[] */ ],
  "npcFields": [ /* StatFieldDefinition[] */ ]
}
```

`pcFields` and `npcFields` are independent `StatFieldDefinition[]` (per #83's note that monster blocks need more structure than PC sheets — actions/traits/legendary-actions repeating groups that a PC sheet has no use for). They may share `key`s with the same meaning (both sides might have `st`), but there's no requirement that they do.

- `formatVersion` (integer, required) — versions *this document's own JSON shape*, distinct from `version` (the pack's content version, e.g. semver, bumped when GURPS errata changes a formula, not when this RFC changes). A client that doesn't recognize a pack's `formatVersion` refuses to load it rather than silently misinterpreting a shape it's never seen. This document defines `formatVersion: 1`.
- `id` (string, required) — format `^[a-z0-9][a-z0-9-]*$`, max 64 characters. Deliberately **not** the same regex as a field `key`: an `id` is never a formula identifier, but it is (per the deferred #91 work) a cache filename and part of a URL path, so its charset is restricted specifically to exclude `.`, `/`, `\`, and whitespace entirely — `"../../../etc/passwd"` doesn't even parse under this charset, so path traversal is closed off by construction rather than left to whatever consumes `id` later to sanitize. (`"gurps-4e"` is valid under this rule; it just isn't valid under the field-`key` rule, which is why the two need separate regexes rather than reusing one.)
- **Collision rule:** `id` must be unique among all installed systems, and **built-ins always win** a collision — a pack (today: only the four in-box profiles; later, once #91 exists: a downloaded one too) declaring an `id` that matches an already-installed system is rejected at install time. An `id` collision must never silently rebind an existing campaign's `CharacterSystemId` to a different pack's fields out from under it. Stated now so the envelope shape doesn't need to change again once #91 exists to actually enforce it.

## The derived-formula grammar

Arithmetic only, over named-field references. This is a strict security requirement, not a design nicety: a downloaded community system pack (paused for now, see the scope note above, but the format must never need to change when that work resumes) is untrusted data, forever. The grammar has to be **provably incapable of executing arbitrary code** — not "sandboxed," not "carefully reviewed," structurally incapable. (That claim is specifically about code execution. Whether the grammar can be abused to *exhaust resources* — stack, CPU, memory — without ever executing anything is a separate property, addressed explicitly in "Resource limits" below rather than assumed to follow for free.)

### Allowed

```
formula    := expression EOF
expression := term (("+" | "-") term)*
term       := factor (("*" | "/") factor)*
factor     := number-literal | field-reference | "(" expression ")" | "-" factor
number-literal  := digit+ ["." digit+]
field-reference := key of another field visible in scope (see below)
```

That's the entire grammar. `(ht + dx) / 4 + speedAdjustment` and `(str - 10) / 2` are the only kind of thing a formula can say. Two things worth calling out explicitly because they're easy to get wrong in a literal implementation:

- **The top-level `formula := expression EOF` production is load-bearing.** A parser that parses a valid `expression` prefix and stops, discarding whatever's left over, will happily evaluate `"1 + 2 THIS IS NOT ARITHMETIC"` as `3`. Parsing a formula means parsing the *entire* string as one `expression` with nothing left over; any leftover input, however innocuous-looking, is a parse failure, full stop.
- **`number-literal` has no exponent notation** — no `e`/`E`, and no sign (unary `-factor` in the grammar already covers negation, so the literal itself is always non-negative digits with an optional single decimal point). This isn't just lexical minimalism: without exponent syntax, a formula literally cannot write `1e400`, which is exactly the input that turns "silently becomes `Infinity`" from a live concern into something the grammar can't express in the first place.

### Numeric type

The evaluator's numeric type is `System.Decimal`, not `double`. This is a specific, load-bearing choice, not an implementation detail left to #83: `decimal` throws `OverflowException` on overflow and `DivideByZeroException` on division by zero, instead of `double`'s behavior of silently producing `Infinity` or `NaN`. That matters because `NaN` defeats a `min`/`max` clamp outright (every comparison with `NaN` is `false`) and defeats `rounding` (`Math.Floor(NaN)` is `NaN`) — a poisoned value sails straight through both and out the other side looking like an ordinary number, right up until it reaches a `JsonSerializer.Serialize` call somewhere downstream and throws there instead, on a totally different, unrelated character. `decimal` has no such value to smuggle: an evaluation either produces a real, finite number, or it throws immediately at the point of failure, where "Runtime failure semantics" below says exactly what happens next.

A courtesy bound on top of that, for pack authors rather than as the actual security boundary: a `number-literal`'s value should stay within ±10^15. The actual boundary is that `decimal` throws on overflow rather than wrapping or going infinite, and the runtime contract requires that exception to fail the field closed — a formula can still be legally shaped to overflow `decimal` outright (deeply nested multiplication, say), and the format doesn't try to statically rule that out, because it doesn't need to: it degrades to "field shows blank," never a hang or a crash.

All numeric parsing and formatting — formula literals, and any stat value stored or displayed as a number — is done with `CultureInfo.InvariantCulture`, explicitly and always. This isn't a style preference: `double.Parse("5.25")` (or `decimal.Parse`) under a culture that uses `,` as the decimal separator parses that string as `525`, silently, with no exception — an ordinary GURPS Basic Speed value turning into a wildly wrong number purely because of the host machine's locale, with nothing hostile involved at all. There's no existing precedent to follow here either way (the repo has no current `InvariantCulture` usage anywhere), so this is stated explicitly rather than left to whatever the first implementation happens to default to.

### Explicitly disallowed, and why

- **No function calls** — no `floor()`, `min()`, `max()`, `abs()`, nothing. A function-call syntax is an open door to "just one more built-in" and eventually to a plugin/extension mechanism; the grammar has no call syntax at all, so there's nothing to close.
- **No conditionals, ever** — no ternary, no `if`, no comparison operators (`<`, `>`, `==`). Anything that looks like it needs a condition (see "Where derived can't reach," below) is handled by restructuring the data, not by teaching the grammar to branch.
- **No loops or aggregation** — no sum-across-rows, no count, no for-each over a `repeating-group`. A formula only ever touches a fixed, statically-named set of fields.
- **No string operations** beyond a bare field reference — no concatenation, no formatting.
- **No assignment, no side effects** — a formula is pure; evaluating it twice always gives the same answer for the same inputs.

A hand-rolled recursive-descent parser over this grammar produces an AST with exactly four node kinds (`Literal`, `FieldRef`, `BinaryOp`, `UnaryNegate`) and cannot be coerced into doing anything a four-function calculator couldn't — that's the code-execution claim, and it holds. It is **not**, on its own, "trivially terminating" or cheap to evaluate — see "Resource limits" immediately below, which an earlier draft of this document didn't state and which a literal implementation of that draft failed under adversarial input. Malformed or hostile input (unknown field key, unbalanced parens, a cycle, anything over the bounds in "Resource limits") fails validation at schema-load time; it never throws uncaught or gets partially evaluated (per #83's acceptance criteria).

### Resource limits

Arithmetic-only doesn't imply cheap-to-evaluate, and a from-scratch implementation of an earlier draft of this grammar demonstrated exactly that under adversarial input. These bounds are part of the format's contract — every conforming implementation enforces all of them, at load time wherever the check can be made statically:

- **Formula string length: 500 characters, maximum.** Checked, and failed closed, before parsing begins. (The longest formula in any of the four worked sketches below is well under 100 characters.)
- **Formula nesting depth: 32 levels, maximum**, tracked by a counter incremented *before* each recursive descent into `"(" expression ")"` or unary `-factor`, checked against the limit at that same point, and failed closed **before** the next recursive call is made — not after, and not by relying on the call eventually returning. An implementation that only checks depth on the way back out, or checks but recurses anyway "just this once," still overflows the stack. A from-scratch implementation of an earlier, unbounded version of this grammar crashed with an uncatchable `StackOverflowException` at roughly 20,000 levels of parenthesis nesting — a ~40KB formula string, unremarkable as JSON content and nowhere close to the 500-character ceiling above, which alone would already have rejected it.
- **Derived-field dependency-chain depth: 64 fields, maximum** (see "Scope resolution" below for what a "chain" means here), **and evaluation must be single-pass and memoized** — every `derived` field involved in evaluating a character is computed **at most once**, in an order fixed by a topological sort of the (already cycle-checked) dependency graph, with each field's result cached for reuse by every other field that references it. This is a format requirement, not an optimization left for #83 to discover: a naive evaluator that resolves a field reference by *re-running that field's own formula from scratch* is exponential in how many times a field gets referenced along a chain — a chain of 20 fields, each formula referencing the previous field twice, measured at roughly 2 million evaluations (40ms); 28 fields, ~537 million (8.8s); around 40 fields was an effective permanent hang. That's a ~2KB pack. Single-pass memoized evaluation is O(number of derived fields) regardless of reference count, which is also why the topological-sort step itself has to be iterative or explicitly depth-bounded — a naive recursive depth-first cycle check has the same stack-depth problem as formula nesting does, just one level up.
- **`pattern` regex dialect: `RegexOptions.NonBacktracking` (.NET 7+), never the default backtracking engine.** `NonBacktracking` runs in time linear in input length by construction, which is what actually closes the ReDoS hole rather than just making it less likely: `^(a+)+$` under the default engine measured 97ms at 21 characters of input, 23 seconds at 29, and was still running past a 120-second timeout at 33 — `NonBacktracking` handles a 5,000-character adversarial input against the same shape of pattern in 41ms. The tradeoff is real and is the accepted one, but it's a blunter tradeoff than "rejects only what's actually dangerous": `NonBacktracking` refuses some constructs outright (backreferences, some lookaround, certain nested-quantifier shapes) because it doesn't support them structurally, not because each one specifically enables catastrophic backtracking — a few of the rejected constructs are perfectly backtracking-safe on their own, just unsupported by this engine. #84–#87's authors should expect the accepted dialect to be narrower than "everything that's actually safe" and occasionally need to reach for a simpler pattern than the one they'd write first. A `pattern` that fails to compile under `NonBacktracking` throws `System.Text.RegularExpressions.NotSupportedException` specifically — the load-time validator has to catch that exact type (see the checklist below), not a generic parse exception, or a rejected pattern escapes as an unhandled exception instead of an ordinary validation failure. A field with `pattern` set must also set `maxLength`: a linear-time matcher against unbounded input is still an unbounded matcher.
- **Hard ceilings, enforced by the engine regardless of what a pack declares:** `maxLength` ≤ 10,000 characters (`text`/`free-text-block`); `pattern` string itself ≤ 200 characters; `repeating-group` row count (whatever `maxItems` a pack sets, or the engine's own default of `100` if it sets none) ≤ 1,000; `itemFields` per group ≤ 50; top-level field *definitions* per `pcFields`/`npcFields` ≤ 200; and a separate aggregate ceiling of ≤ 10,000 total field *instances* across `pcFields` + `npcFields` combined (every flat field counts once; a `repeating-group` counts as `maxItems × itemFields.length`, using the `100` default when `maxItems` is unset — this is what actually closes the multiplicative blowup that a naive "≤200 definitions" count alone doesn't: 200 groups × 50 `itemFields` × 1,000 `maxItems` is up to 10 million field instances per character, and that's the number this ceiling exists to catch). **A pack exceeding any of these is rejected outright at load time** — same as every other rule in this section and in the checklist below. None of these ceilings are ever silently clamped down to fit; "the value gets capped" and "the pack gets rejected" are two different behaviors and this format only ever does the second one.

One more distinction worth stating plainly: `System.Text.Json`'s own default `MaxDepth` (64) already bounds how deeply the *pack's JSON document itself* can nest objects/arrays — but a `formula` is a single JSON *string value*, and parsing its contents is a wholly separate recursive-descent pass with no relationship to JSON's own nesting limit. The formula-nesting-depth bound above exists because JSON's `MaxDepth` doesn't and can't cover it.

### Rounding is field metadata, not a grammar function

D&D's ability modifier is conventionally written `floor((score - 10) / 2)`. There is deliberately no `floor()` in the grammar. Instead, `rounding` (see the `derived` field type above) is a presentation property of the *field*, applied to the formula's plain real-number result after evaluation — identical in kind to `precision` on an ordinary `number` field, not a new grammar feature:

```json
{ "key": "strMod", "label": "STR Modifier", "type": "derived", "formula": "(str - 10) / 2", "precision": 0, "rounding": "floor" }
```

This is why `/` in the grammar is ordinary real-valued division, not floor/integer division: GURPS's `basicSpeed` genuinely needs the fractional remainder (`5.25`, not `5`), while D&D's ability modifier needs it floored. Same operator, different field-level rounding — the grammar stays uniform and the systems differ only in metadata.

**`precision` always determines the stored/displayed decimal places — it isn't a no-op that `rounding: none` can skip.** Every one of the five `rounding` values rounds the value to `precision` decimal places; they differ only in *how* the rounding is done at that precision:

| `rounding` | Behavior at `precision` decimal places |
| --- | --- |
| `none` (default) | Round to nearest, ties resolved to even (`MidpointRounding.ToEven`, .NET's own default — chosen so an implementation gets this one for free without special-casing anything). |
| `round` | Round to nearest, ties resolved away from zero (`MidpointRounding.AwayFromZero`) — the tie-break most pack authors intuitively expect ("2.5 rounds to 3"), which is exactly why it's offered as a distinct, explicit option rather than folded into `none`. |
| `floor` | Always rounds down, toward negative infinity. |
| `ceiling` | Always rounds up, toward positive infinity. |
| `truncate` | Rounds toward zero (drops the excess digits without regard to sign). |

(GURPS's `basicSpeed` example below sets `precision: 2` and leaves `rounding` at its `none` default; `(ht + dx) / 4` only ever produces exact quarters — `.00`/`.25`/`.50`/`.75` — so rounding to 2 places is a no-op for every legal input regardless of tie-break rule, and the example's output is unchanged by this clarification. D&D's ability-modifier and proficiency-bonus examples use `precision: 0, rounding: floor`, which was always unambiguous — `floor` doesn't have a tie-break to clarify.)

Order of operations, stated once so no two implementations can disagree: evaluate the formula to a raw `decimal`, **clamp** to `min`/`max` if either is set, **then** round the clamped value to `precision` decimal places per the table above. Whatever a `derived` field's dependents see when they reference it by key is this final, clamped-then-rounded value — never the pre-clamp or pre-round intermediate.

### Runtime failure semantics

Evaluating a formula against an actual character's data can fail three ways that no amount of schema-load-time validation can rule out in advance, because they depend on runtime values, not on the formula's shape: a divisor of zero (an adjustment field a player set to `0` — perfectly ordinary data, not hostile input), an operation that overflows `decimal`'s range, or — defense in depth, should never happen after load-time validation — a referenced key that fails to resolve. In every case the field **fails closed**: its value is treated as invalid/blank for that character. The failure never propagates as an uncaught exception, and it never gets clamped, rounded, or serialized as if the computation had actually succeeded (per #83's own acceptance criteria). This applies transitively through the dependency chain — if `basicSpeed` fails, `basicMove`, which references it, fails closed too, rather than being computed from a garbage or default-substituted upstream value.

The same fail-closed-per-field treatment applies to runtime data that simply doesn't match its own field's schema, which load-time validation can't catch either since it only ever sees the pack's shape, not a character's actual data: a stored `number` value outside its `min`/`max`, a stored `enum` value not in its `options`, or a `repeating-group` with more rows than its `maxItems` (a pack update that tightens a bound after characters already exist is the ordinary way this happens, not hostile data). None of these crash or throw past the field boundary — the offending field shows as invalid/blank, exactly like a formula runtime failure, and every other field on the same character is unaffected.

### Scope resolution: what a formula can reference

`derived` is a **top-level-only** field type — it can appear directly in `pcFields`/`npcFields`, never inside a `repeating-group`'s `itemFields` (see "Resource limits" and the `repeating-group` field type above for why: none of the four systems this format is validated against ended up with a working use for a row-scoped one, so the format doesn't carry the feature). That one restriction is most of what makes this section short:

- A `derived` field may only reference other **top-level** field keys of the same `pcFields`/`npcFields` set. It can never reach into a `repeating-group`'s rows — there's no single row to resolve to, and a row has no formulas of its own to reach back out from anyway.
- A `derived` field may reference another `derived` field, chained arbitrarily deep, provided the dependency graph is acyclic (see below).
- **A formula may only reference field keys that are statically named in the formula's own text** — never a key selected dynamically via another field's value. E.g., a GURPS skill row can't say "add whichever of ST/DX/IQ/HT this row's `controllingAttribute` enum happens to name" — that's indirection/dynamic dispatch, which is exactly as capable of hiding conditional logic as an `if`, and the grammar has no facility for it. See "Where derived can't reach," in the GURPS section below, for how the four sketches actually handle this (and why it's also the reason row-scoped `derived` never got any real use to justify keeping it).
- **No cycles.** The dependency graph of every top-level `derived` field (`pcFields` and `npcFields` checked separately) must be acyclic. A self-reference or a mutual reference is a schema validation error caught at load time, never a runtime failure or infinite evaluation. A "chain" for the purposes of the 64-field dependency-chain-depth ceiling above is the longest path through this graph.
- **Keys must be unique within their scope.** Once among all of a `pcFields` (or `npcFields`) set's top-level entries, and *independently* once among each individual `repeating-group`'s own `itemFields` — two different groups may reuse the same item key (both `actions` and `traits` having a `name` field, say) because their rows are separate scopes. There's no shadowing question to arbitrate between a row and the top level, because a row's fields never resolve a formula at all — they're always flat, directly-entered values.

### The "adjustment field" idiom

A recurring pattern in the sketches below: something is *mostly* a fixed formula but needs an escape hatch for a value the rules let a character buy up or down independently (GURPS letting you buy extra HP above what ST implies; D&D letting proficiency change a save/skill bonus). Rather than adding a conditional to the grammar, the escape hatch is a plain, directly-entered `number` field (`default: 0`, so it contributes nothing until the player sets it) that the `derived` formula references as an ordinary operand:

```json
[
  { "key": "hpAdjustment", "label": "HP Adjustment", "type": "number", "min": -50, "max": 50, "default": 0 },
  { "key": "hp", "label": "Hit Points", "type": "derived", "formula": "st + hpAdjustment" }
]
```

The condition's *outcome* (how much to add) is entered once, as data; the formula never has to branch to decide whether to add it.

## Load-time validation checklist

Everything above that's a *static* rule (true or false from the pack's JSON content alone, without needing an actual character's data) is consolidated here in one place, so #83 has a concrete checklist to implement against rather than reassembling one from scattered prose. A pack — today, only the four in-box profiles; later, once #91 exists, a downloaded one too — is rejected before any character ever sees it if any of the following hold:

1. `formatVersion` is missing or a value this client doesn't recognize.
2. `id` doesn't match `^[a-z0-9][a-z0-9-]*$` (max 64 chars), or collides with an already-installed system's `id` (built-ins win — see "The `CharacterSystem` envelope").
3. Any field's `key` fails `^[a-zA-Z_][a-zA-Z0-9_]*$`, or isn't unique within its scope (top-level `pcFields`/`npcFields`, or a single `repeating-group`'s own `itemFields` — see "Scope resolution").
4. Any `enum` field's `options` list is empty.
5. Any `text`/`free-text-block` field's `maxLength` exceeds the engine's 10,000-character hard ceiling; any `text` field sets `pattern` without also setting `maxLength`; any `pattern` exceeds 200 characters, or fails to compile under `RegexOptions.NonBacktracking` — a rejected pattern throws `System.Text.RegularExpressions.NotSupportedException` at compile time, specifically (not a generic parse exception), and the load-time validator must catch that exact type and turn it into an ordinary rejection, not let it escape as an unhandled exception (see "Resource limits").
6. Any `repeating-group` contains another `repeating-group`, or a `derived` field (see "Scope resolution" — `derived` is top-level-only), in its `itemFields`; its `itemFields` count, or its `minItems`/`maxItems`, exceed the hard ceilings in "Resource limits"; a `pcFields`/`npcFields` set has more than 200 top-level field *definitions*; or the aggregate field-*instance* count (flat fields, plus each `repeating-group`'s `maxItems × itemFields.length`, using the `100` default where `maxItems` is unset) exceeds 10,000 across `pcFields` + `npcFields` combined.
7. Any `formula` exceeds 500 characters or 32 levels of nesting depth, or fails to parse as a full `formula := expression EOF` — trailing unparsed input is a rejection, never a silent truncation (see "Allowed").
8. Any `formula` references a key not visible in its scope per "Scope resolution" — an unknown key, or a key that only exists inside a `repeating-group`'s rows, which a top-level `formula` can never reach.
9. The dependency graph of every top-level `derived` field (`pcFields` and `npcFields` checked separately — `derived` can't appear anywhere else, see "Scope resolution") contains a cycle, or its longest chain exceeds 64 fields.

Two things this checklist deliberately does *not* cover, because they're bounded elsewhere for reasons already stated in context: `System.Text.Json`'s own default `MaxDepth` (64) already bounds the pack *document's* structural JSON nesting — a separate concern from a `formula` string's own internal parse depth (item 7 above), which JSON's own limit doesn't and can't reach. And nothing on this list depends on runtime character data; anything that does (a zero divisor, an overflow, an out-of-range stored value) is "Runtime failure semantics" above, not a load-time rejection.

## Validation against the four target systems

Illustrative snippets only — enough of each system's shape to prove the format fits, not the full system packs (those are #84–#87).

### GURPS Fourth Edition — the acid test

GURPS has no class or level concept anywhere in this sketch, on purpose — that's the whole point of doing it first.

**Primary attributes** (point-buy — the app stores the resulting score, not the point-cost math of raising it):

```json
[
  { "key": "st", "label": "ST", "type": "number", "min": 1, "max": 20 },
  { "key": "dx", "label": "DX", "type": "number", "min": 1, "max": 20 },
  { "key": "iq", "label": "IQ", "type": "number", "min": 1, "max": 20 },
  { "key": "ht", "label": "HT", "type": "number", "min": 1, "max": 20 }
]
```

**Secondary characteristics** — genuinely derived, each using the adjustment-field idiom so a character can still buy them up or down independently of the primary attribute they're nominally based on (this is a real GURPS rule, and it's exactly the case the idiom above exists for):

```json
[
  { "key": "hpAdjustment", "label": "HP Adjustment", "type": "number", "min": -50, "max": 50, "default": 0 },
  { "key": "hp", "label": "HP", "type": "derived", "formula": "st + hpAdjustment" },

  { "key": "willAdjustment", "label": "Will Adjustment", "type": "number", "min": -20, "max": 20, "default": 0 },
  { "key": "will", "label": "Will", "type": "derived", "formula": "iq + willAdjustment" },

  { "key": "perAdjustment", "label": "Perception Adjustment", "type": "number", "min": -20, "max": 20, "default": 0 },
  { "key": "perception", "label": "Perception", "type": "derived", "formula": "iq + perAdjustment" },

  { "key": "fpAdjustment", "label": "FP Adjustment", "type": "number", "min": -50, "max": 50, "default": 0 },
  { "key": "fp", "label": "FP", "type": "derived", "formula": "ht + fpAdjustment" },

  { "key": "speedAdjustment", "label": "Basic Speed Adjustment", "type": "number", "min": -5, "max": 5, "default": 0 },
  { "key": "basicSpeed", "label": "Basic Speed", "type": "derived", "formula": "(ht + dx) / 4 + speedAdjustment", "precision": 2 },

  { "key": "moveAdjustment", "label": "Basic Move Adjustment", "type": "number", "min": -5, "max": 5, "default": 0 },
  { "key": "basicMove", "label": "Basic Move", "type": "derived", "formula": "basicSpeed + moveAdjustment", "precision": 0, "rounding": "floor" }
]
```

`basicMove` referencing `basicSpeed` (a `derived` field referencing another `derived` field) is deliberate — it proves formula chaining works, not just single-hop references to flat attributes.

**Advantages/Disadvantages/Quirks** (repeating-group — shown in full under "Field types" above as the `traits` example) and **individually-bought skills**:

```json
{
  "key": "skills",
  "label": "Skills",
  "type": "repeating-group",
  "itemFields": [
    { "key": "name", "label": "Name", "type": "text", "maxLength": 100 },
    { "key": "controllingAttribute", "label": "Attribute", "type": "enum", "options": ["ST", "DX", "IQ", "HT"] },
    { "key": "relativeLevel", "label": "Relative Level", "type": "number", "min": -10, "max": 10 },
    { "key": "level", "label": "Skill Level", "type": "number", "min": 0, "max": 40 },
    { "key": "pointCost", "label": "Points", "type": "number", "min": 0, "max": 40 }
  ]
}
```

**Where derived can't reach:** the obvious first draft had `level` as a `derived` row field (`controllingAttribute + relativeLevel`), back when the format still allowed a `derived` field inside a `repeating-group` row at all. That doesn't work, and finding out why is the actual value of doing GURPS first: `controllingAttribute` is an *enum value that varies per row* — "add whichever of ST/DX/IQ/HT this row names" is dynamic dispatch on a field's value, indistinguishable in danger from a conditional, and the grammar has no facility for it by design. Separately, GURPS's real points-to-level conversion is a difficulty-based lookup table (Easy/Average/Hard/Very Hard, non-linear in points spent), not arithmetic at all. Both problems have the same resolution: `level` stays a directly-entered `number`, same as it would be on a paper character sheet — the app is a form, not a rules engine, and `controllingAttribute` is kept purely for display/reference.

This is the one real shape change GURPS forced: derived fields are for a fixed, statically-known formula, never a formula whose *shape* depends on another field's value. That rule is stated in the grammar section above and reused verbatim by D&D's saves/skills and Pathfinder's proficiency bonus below — GURPS just found it first, and every other system sketched below hit the identical wall for its own row-scoped case and landed on the identical resolution. Since none of the four systems this format was validated against ended up with a single working row-scoped `derived` field, row-scoped `derived` was cut from the format entirely rather than kept around as untested machinery: `repeating-group`'s `itemFields` may only contain `number`/`text`/`boolean`/`enum`/`free-text-block` (see the `repeating-group` field type and "Scope resolution" above), and `derived` is a top-level-only field type, full stop. That also means `skills` here needing no `derived` field isn't a quirk of this one group — it's the format's rule, not an exception to it.

### D&D 5e (2014)

**Ability scores and modifiers** — the `derived` + `rounding: floor` pattern from "Rounding is field metadata" above, once per ability (only Dexterity shown; Strength/Constitution/Intelligence/Wisdom/Charisma are identical in shape):

```json
[
  { "key": "dex", "label": "Dexterity", "type": "number", "min": 1, "max": 30 },
  { "key": "dexMod", "label": "DEX Modifier", "type": "derived", "formula": "(dex - 10) / 2", "precision": 0, "rounding": "floor" }
]
```

**Proficiency bonus** — also cleanly `derived`, from `level` (`floor((level - 1) / 4) + 2`, itself pure arithmetic once floor is field metadata rather than a call):

```json
[
  { "key": "level", "label": "Level", "type": "number", "min": 1, "max": 20 },
  { "key": "proficiencyBonus", "label": "Proficiency Bonus", "type": "derived", "formula": "(level - 1) / 4 + 2", "precision": 0, "rounding": "floor" }
]
```

**Passive Perception** — the adjustment-field idiom again, this time to sidestep a conditional ("+ proficiency bonus, but only if proficient") rather than an independent purchase: `perceptionProficiencyBonus` is entered once as `0` or a copy of `proficiencyBonus`'s value, not live-linked, so the formula never has to branch:

```json
[
  { "key": "perceptionProficiencyBonus", "label": "Perception Proficiency Bonus", "type": "number", "min": 0, "max": 12, "default": 0 },
  { "key": "passivePerception", "label": "Passive Perception", "type": "derived", "formula": "10 + wisMod + perceptionProficiencyBonus" }
]
```

**Saves and skills** — same lesson as GURPS's skills: "which ability governs this row" varies per row (skills) or the row's contribution is conditional on proficiency (saves), so both are flat, directly-entered final values in a `repeating-group`, not row-scoped `derived` fields:

```json
{
  "key": "skills",
  "label": "Skills",
  "type": "repeating-group",
  "itemFields": [
    { "key": "name", "label": "Skill", "type": "enum", "options": ["Acrobatics", "Animal Handling", "Arcana", "Athletics", "Deception", "History", "Insight", "Intimidation", "Investigation", "Medicine", "Nature", "Perception", "Performance", "Persuasion", "Religion", "Sleight of Hand", "Stealth", "Survival"] },
    { "key": "proficient", "label": "Proficient", "type": "boolean" },
    { "key": "expertise", "label": "Expertise", "type": "boolean" },
    { "key": "bonus", "label": "Bonus", "type": "number", "min": -10, "max": 20 }
  ]
}
```

`ac` and `hp` are plain `number` fields for the same reason `level`/points-to-level are flat in GURPS skills — 5e's actual AC and HP math folds in conditionals (armor type, per-level hit-die rolling) that this grammar deliberately can't and shouldn't express; the player/GM enters the final number, same as they would on paper.

**NPC/monster block** — `actions`, `traits`, and `legendaryActions` as three separate repeating groups, since they're structurally distinct and independently variable-length:

```json
[
  {
    "key": "traits",
    "label": "Traits",
    "type": "repeating-group",
    "itemFields": [
      { "key": "name", "label": "Name", "type": "text", "maxLength": 100 },
      { "key": "description", "label": "Description", "type": "free-text-block", "maxLength": 1000 }
    ]
  },
  {
    "key": "actions",
    "label": "Actions",
    "type": "repeating-group",
    "itemFields": [
      { "key": "name", "label": "Name", "type": "text", "maxLength": 100 },
      { "key": "attackBonus", "label": "Attack Bonus", "type": "number", "min": -5, "max": 20 },
      { "key": "damage", "label": "Damage", "type": "text", "maxLength": 100 },
      { "key": "description", "label": "Description", "type": "free-text-block", "maxLength": 1000 }
    ]
  },
  {
    "key": "legendaryActions",
    "label": "Legendary Actions",
    "type": "repeating-group",
    "itemFields": [
      { "key": "name", "label": "Name", "type": "text", "maxLength": 100 },
      { "key": "cost", "label": "Cost", "type": "number", "min": 1, "max": 3 },
      { "key": "description", "label": "Description", "type": "free-text-block", "maxLength": 1000 }
    ]
  }
]
```

### D&D 5e (2024 revision)

Same shape as 2014 above, verbatim — the schema format doesn't change at all, only the labels and a couple of field names differ to match 2024 terminology:

- `race` becomes `species` (same `text`/`enum` field, renamed key and label).
- 2024 moves the ability-score-increase choice from species to background. That's a character-creation-time decision the app doesn't referee — it only stores the resulting six ability scores, which are already plain `number` fields regardless of *why* a given score ended up where it is. No schema impact; worth a code comment in the #85 system profile, not a schema change here.

### Pathfinder 2e

The one genuinely new thing PF2e needs is proficiency expressed as a *rank*, not a bonus — an `enum`, which the format already supports as a first-class type:

```json
[
  {
    "key": "perceptionProficiencyRank",
    "label": "Perception Proficiency",
    "type": "enum",
    "options": ["Untrained", "Trained", "Expert", "Master", "Legendary"]
  },
  { "key": "perception", "label": "Perception", "type": "number", "min": -5, "max": 30 }
]
```

Per the acceptance criteria, PF2e's Perception is deliberately its own independent field (`perception`, entered directly) rather than derived from Wisdom the way 5e's `passivePerception` is — a proficiency *rank* changes what a d20 roll adds (rank + level, PF2e's actual formula), not what a fixed passive score is, and which rank contributes what bonus is exactly the "varies by enum value" dispatch problem the GURPS section ruled out of the grammar. So `perceptionProficiencyRank` is informational/reference (what the character sheet prints next to Perception) and `perception` is the number the app actually stores and shows, entered directly — same resolution as GURPS skill levels and 5e proficiency-gated bonuses, once more confirming it's a structural rule of the format rather than a one-off.

The same `enum` rank list is reused for every other proficiency-gated stat (saves, skills, weapon/armor proficiencies), each paired with its own directly-entered numeric field, following this same pattern.

## Summary of what changed while validating

The shape did **not** need a new field type or a grammar change to fit all four systems. What it needed, discovered by doing GURPS first as instructed, was one explicit rule about what `derived` is *not* for: a formula must be fixed and statically known, never dispatched on another field's runtime value. That rule shows up four times across the sketches above (GURPS skill levels, D&D saves/skills, D&D passive perception's proficiency gate, Pathfinder's proficiency rank) and is now written into the grammar section rather than left to be independently rediscovered per system profile in #84–#87.

A subsequent security review found a second, separate class of gap in the same document: not in what the four systems needed, but in what an adversarial pack could do to the engine regardless of which system it claimed to be — unbounded formula nesting, naive re-evaluation of a dependency chain, and an unbounded backtracking regex, none of which are code execution, all three of which reproduce a hang or a crash. None of those findings changed a field type or the arithmetic grammar either; they became explicit numeric bounds and evaluation-strategy requirements ("Resource limits," "Runtime failure semantics," the memoized-evaluation and `NonBacktracking` requirements above) plus a few envelope gaps (`formatVersion`, `id`'s own format and collision rule) that were always going to be needed once #91 exists and are cheaper to settle now than to retrofit later.

A third pass, checking this document against #82's own acceptance criteria and against the actual repo rather than against hostile input, found two more things worth fixing rather than leaving implicit. First, row-scoped `derived` fields — the one piece of machinery complex enough to need its own shadowing-precedence rule — turned out to be validated against exactly zero of the four sketches above; every one of them independently landed on a flat, directly-entered value for the same dynamic-dispatch reason. Untested complexity in a format this deliberately minimal was a bigger risk than the feature was worth, so it was cut outright: `derived` is top-level-only now, and "Scope resolution" is shorter and has no shadowing rule to state because there's no longer a row/top-level naming collision to arbitrate. Second, this document had never been checked against the actual `Campaign`/`PlayerCharacter`/`Npc` model it's meant to attach to — "Attachment point" above now says plainly that NPC stats storage doesn't exist until #88, that `CharacterSystemId` is a nullable `string?` keyed on the pack's own `id` rather than a synthetic `Guid`, that `Campaign.GameSystem` and a `derived` field are never persisted, only recomputed. None of that changes the schema shape itself; it closes the gap between what this document assumed the domain model looked like and what it actually looks like today.
