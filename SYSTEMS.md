# Typed character/NPC systems

RFC output of [#82](https://github.com/cclapham/gm-toolkit/issues/82). Defines the stat-field schema JSON shape and the derived-formula grammar that [#83](https://github.com/cclapham/gm-toolkit/issues/83) (the engine) and [#84](https://github.com/cclapham/gm-toolkit/issues/84)–[#87](https://github.com/cclapham/gm-toolkit/issues/87) (the four built-in system profiles) build against. Mirrors [CONTRIBUTING.md](CONTRIBUTING.md)'s "Generator tables" section in spirit: this is where the data shape lives and what the rules around it are.

**Deferred, not covered here:** the client-side distribution API contract (`GET {baseUrl}/systems`, the pack `downloadUrl` response) and the sha256 download-verification flow, both originally scoped in #82. Per [the comment on #82](https://github.com/cclapham/gm-toolkit/issues/82#issuecomment-5131472307), network distribution ([#91](https://github.com/cclapham/gm-toolkit/issues/91)/[#92](https://github.com/cclapham/gm-toolkit/issues/92)/[#93](https://github.com/cclapham/gm-toolkit/issues/93)) is paused until a separate approval/distribution service project exists — designing the wire format against a service that doesn't exist yet risks designing against the wrong shape. Everything below (the schema format itself) is active and unblocked, and is written so that format never has to change to accommodate that later work — a system pack is a JSON file whether it arrives in-box or downloaded.

## Attachment point

A `CharacterSystem` attaches to a `Campaign` via `Campaign.CharacterSystemId`, shared by every `PlayerCharacter` and every `Npc` in that campaign. There is no per-character or per-NPC override — one campaign, one system. A campaign with no system attached keeps today's freeform `Dictionary<string, string>` behavior unchanged (the "generic" system, see #83); typed systems are additive, not a breaking migration.

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

- `min` / `max` (optional) — inclusive range.
- `precision` (optional, default `0`) — decimal places the value is stored/displayed at.
- `step` (optional) — a UI increment hint (e.g. spinner step); not a validation rule.

### `text`

```json
{ "key": "playerName", "label": "Player", "type": "text", "maxLength": 100 }
```

- `maxLength` (optional).
- `pattern` (optional) — a regex the value must match (e.g. constraining a code or short ID field). Most `text` fields set neither and just take `maxLength`.

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
- `precision`, `rounding` (both optional) — same numeric-presentation properties as `number`, plus `rounding`: `none` (default) | `floor` | `ceiling` | `round` | `truncate`.
- `min` / `max` (optional) — an optional sanity clamp on the computed result, same semantics as `number`.

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

- `itemFields` (required, non-empty) — a `StatFieldDefinition[]` describing one row. May contain `number`/`text`/`boolean`/`enum`/`derived`/`free-text-block` fields. May **not** contain another `repeating-group` — no nested lists. One level is sufficient for every system sketched below and keeps the evaluator and the UI form generator simple.
- `minItems` / `maxItems` (optional) — row-count bounds; both usually omitted (an empty list is valid — a character with no advantages yet).

### `free-text-block`

Prose, not a structured value — a monster's trait description, a quirk's flavor text.

```json
{ "key": "notes", "label": "Notes", "type": "free-text-block", "maxLength": 4000 }
```

- `maxLength` (optional, default 4000) — the only validation; no `pattern`, this is prose.

## The `CharacterSystem` envelope

```json
{
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

## The derived-formula grammar

Arithmetic only, over named-field references. This is a strict security requirement, not a design nicety: a downloaded community system pack (paused for now, see the scope note above, but the format must never need to change when that work resumes) is untrusted data, forever. The grammar has to be **provably** incapable of executing anything beyond arithmetic — not "sandboxed," not "carefully reviewed," structurally incapable.

### Allowed

```
expression := term (("+" | "-") term)*
term       := factor (("*" | "/") factor)*
factor     := number-literal | field-reference | "(" expression ")" | "-" factor
field-reference := key of another field visible in scope (see below)
```

That's the entire grammar. `(ht + dx) / 4 + speedAdjustment` and `(str - 10) / 2` are the only kind of thing a formula can say.

### Explicitly disallowed, and why

- **No function calls** — no `floor()`, `min()`, `max()`, `abs()`, nothing. A function-call syntax is an open door to "just one more built-in" and eventually to a plugin/extension mechanism; the grammar has no call syntax at all, so there's nothing to close.
- **No conditionals, ever** — no ternary, no `if`, no comparison operators (`<`, `>`, `==`). Anything that looks like it needs a condition (see "Where derived can't reach," below) is handled by restructuring the data, not by teaching the grammar to branch.
- **No loops or aggregation** — no sum-across-rows, no count, no for-each over a `repeating-group`. A formula only ever touches a fixed, statically-named set of fields.
- **No string operations** beyond a bare field reference — no concatenation, no formatting.
- **No assignment, no side effects** — a formula is pure; evaluating it twice always gives the same answer for the same inputs.

A hand-rolled recursive-descent parser over this grammar produces an AST with exactly four node kinds (`Literal`, `FieldRef`, `BinaryOp`, `UnaryNegate`), is trivially terminating (bounded by the formula's own text length — there's no construct that can recurse or loop independent of input size), and cannot be coerced into doing anything a four-function calculator couldn't. Malformed or hostile input (unknown field key, unbalanced parens, a cycle) fails validation at schema-load time; it never throws uncaught or gets partially evaluated (per #83's acceptance criteria).

### Rounding is field metadata, not a grammar function

D&D's ability modifier is conventionally written `floor((score - 10) / 2)`. There is deliberately no `floor()` in the grammar. Instead, `rounding` (see the `derived` field type above) is a presentation property of the *field*, applied to the formula's plain real-number result after evaluation — identical in kind to `precision` on an ordinary `number` field, not a new grammar feature:

```json
{ "key": "strMod", "label": "STR Modifier", "type": "derived", "formula": "(str - 10) / 2", "precision": 0, "rounding": "floor" }
```

This is why `/` in the grammar is ordinary real-valued division, not floor/integer division: GURPS's `basicSpeed` genuinely needs the fractional remainder (`5.25`, not `5`), while D&D's ability modifier needs it floored. Same operator, different field-level rounding — the grammar stays uniform and the systems differ only in metadata.

### Scope resolution: what a formula can reference

- A `derived` field at the top level (directly in `pcFields`/`npcFields`) may only reference other **top-level** field keys of the same set. It can never reach into a `repeating-group`'s rows — there's no single row to resolve to.
- A `derived` field that lives inside a `repeating-group`'s `itemFields` may reference (a) sibling field keys within the **same row**, and (b) any top-level field key of the same `pcFields`/`npcFields` set. It may **not** reference another row's fields, a different `repeating-group`'s rows, or aggregate across rows.
- A `derived` field may reference another `derived` field (top-level or row-scoped), chained arbitrarily deep, provided the dependency graph is acyclic (see below).
- **A formula may only reference field keys that are statically named in the formula's own text** — never a key selected dynamically via another field's value. E.g., a GURPS skill row can't say "add whichever of ST/DX/IQ/HT this row's `controllingAttribute` enum happens to name" — that's indirection/dynamic dispatch, which is exactly as capable of hiding conditional logic as an `if`, and the grammar has no facility for it. See "Where derived can't reach," in the GURPS section below, for how the four sketches actually handle this.
- **No cycles.** The combined dependency graph of every `derived` field (top-level and row-scoped, both `pcFields` and `npcFields` separately) must be acyclic. A self-reference or a mutual reference is a schema validation error caught at load time, never a runtime failure or infinite evaluation.

### The "adjustment field" idiom

A recurring pattern in the sketches below: something is *mostly* a fixed formula but needs an escape hatch for a value the rules let a character buy up or down independently (GURPS letting you buy extra HP above what ST implies; D&D letting proficiency change a save/skill bonus). Rather than adding a conditional to the grammar, the escape hatch is a plain, directly-entered `number` field (defaulting to `0`) that the `derived` formula references as an ordinary operand:

```json
[
  { "key": "hpAdjustment", "label": "HP Adjustment", "type": "number", "min": -50, "max": 50 },
  { "key": "hp", "label": "Hit Points", "type": "derived", "formula": "st + hpAdjustment" }
]
```

The condition's *outcome* (how much to add) is entered once, as data; the formula never has to branch to decide whether to add it.

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
  { "key": "hpAdjustment", "label": "HP Adjustment", "type": "number", "min": -50, "max": 50 },
  { "key": "hp", "label": "HP", "type": "derived", "formula": "st + hpAdjustment" },

  { "key": "willAdjustment", "label": "Will Adjustment", "type": "number", "min": -20, "max": 20 },
  { "key": "will", "label": "Will", "type": "derived", "formula": "iq + willAdjustment" },

  { "key": "perAdjustment", "label": "Perception Adjustment", "type": "number", "min": -20, "max": 20 },
  { "key": "perception", "label": "Perception", "type": "derived", "formula": "iq + perAdjustment" },

  { "key": "fpAdjustment", "label": "FP Adjustment", "type": "number", "min": -50, "max": 50 },
  { "key": "fp", "label": "FP", "type": "derived", "formula": "ht + fpAdjustment" },

  { "key": "speedAdjustment", "label": "Basic Speed Adjustment", "type": "number", "min": -5, "max": 5 },
  { "key": "basicSpeed", "label": "Basic Speed", "type": "derived", "formula": "(ht + dx) / 4 + speedAdjustment", "precision": 2 },

  { "key": "moveAdjustment", "label": "Basic Move Adjustment", "type": "number", "min": -5, "max": 5 },
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

**Where derived can't reach:** the obvious first draft was to make `level` a `derived` row field (`controllingAttribute + relativeLevel`) instead of a flat entered one. That doesn't work, and finding out why is the actual value of doing GURPS first: `controllingAttribute` is an *enum value that varies per row* — "add whichever of ST/DX/IQ/HT this row names" is dynamic dispatch on a field's value, indistinguishable in danger from a conditional, and the grammar has no facility for it by design. Separately, GURPS's real points-to-level conversion is a difficulty-based lookup table (Easy/Average/Hard/Very Hard, non-linear in points spent), not arithmetic at all. Both problems have the same resolution: `level` stays a directly-entered `number`, same as it would be on a paper character sheet — the app is a form, not a rules engine, and `controllingAttribute` is kept purely for display/reference. This is also why `skills` has no `derived` field in it at all, unlike `traits`' plain data-only rows.

This is the one real shape change GURPS forced: derived fields are for a fixed, statically-known formula, never a formula whose *shape* depends on another field's value. That rule is stated in the grammar section above and reused verbatim by D&D's saves/skills and Pathfinder's proficiency bonus below — GURPS just found it first.

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
  { "key": "perceptionProficiencyBonus", "label": "Perception Proficiency Bonus", "type": "number", "min": 0, "max": 12 },
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
