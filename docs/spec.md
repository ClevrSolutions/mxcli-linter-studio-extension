# CLEVR ACR Shell — functional specification & data contract

Goal: a CLEVR Studio Pro extension that aggregates and presents lint violations like the old ACR —
same categories, exclusions with reason, and an ACR-style report.

This document is the COMPASS for whoever builds the extension (Claude Code / Codex).
The builder codes against this contract; they do not invent formats or categories themselves.
The human (Michel/CLEVR) maintains the functional standard: categories, reporting,
exclusions, and the rule "a rule only counts once verified".

---

## ⚠️ FINAL STATE (June 2026) — read this first; it supersedes the historical sections below

This document was written during the **two-engine phase** (mxcli .star + mxlint .rego). **The final state is different.**
The **data contract is still valid and authoritative**; only the engine/phase descriptions are historical.

**WHAT STILL APPLIES (use this as the contract):**
- **§1** ACR categories (six) & severities (Minor<Major<Critical<Blocker).
- **§2** The normalised `Violation` format — `kind: "acr" | "generic"`, all fields, `documentType` canonicalisation.
- **§3** Exclusions + the fingerprint strategy (`sha1(ruleId|documentQualifiedName|elementName)`).
- **§4** Rule registry concept + the golden rule ("one ruleId = one source") + the **"only counts once verified"** discipline + cross-engine deduplication (claim table).
- **§5** Report (per-rule grouping, origin badge, display mapping of mxcli category → ACR category).

**WHAT IS OUTDATED (historical — see `CLEVR-ACR-overdracht.md` + `clevr-acr-shell-status.md` for the actual state):**
- **mxlint/.rego has been COMPLETELY removed.** One engine: **mxcli** (Apache-2.0, v0.12.0). No second engine, no Rego, no `modelsource/` YAML export, no mxlint binary/download/bootstrap.
- **`source` values:** in practice only `"clevr-acr"` (ACR rules) and `"mxcli"` (generic). `"mxlint"` no longer occurs (the UI branches for it remain as dead, harmless guards).
- **`packs.json`** with an mxlint pack is outdated; only mxcli's bundled rules are the generic source.
- **The overlap/suppression table (§4, 6 mxlint twins)** has been replaced by the **claim table + two tripwire tests** (mxcli-vs-CLEVR deduplication, internal).
- **The data sources (§3-historical, §7, §9)** are now all mxcli: `lint --format json`, `CATALOG.*` (SQLite), `describe <type>`, `describe projectsecurity`/`userrole`, `project-tree`. NO modelsource/YAML.
- **The MVP/phase build order (§6, §9)** has been completed; the product is live.
- **Rules:** 12 migrated (7 catalog + 5 describe), 4 deferred to mxcli's own rules, security via describe. Two scan modes (fast/deep) with **progressive streaming** of findings. Three parked reactivations (SEC-006; MAINT-015/REL-003; severity calibration). Details in the handover + status.

---

## 0. Architecture (summary) — HYBRID C# + TypeScript

CORRECTED after exploration, PROVEN by the C# spike (June 2026): a pure
web/TypeScript extension CANNOT start an external process (the web UI runs in a
sandboxed webview without a process execution API). Running an external CLI process must
go through a C# backend component (.NET 10, Process.Start). The C# backend also HOSTS
the web UI (Studio Pro's built-in web server, WebServerExtension) and communicates
with it via the WebView message bus (IWebView.PostMessage ⇄ window.chrome.webview),
NOT via studioPro.ui.messagePassing. The extension is therefore hybrid:

    CLEVR ACR Shell (Studio Pro extension — C# host + web UI in the webview)
       │
       ├─ C# backend (.NET 10 .dll, in-process)       ← process execution + UI hosting
       │    ├─ DockablePaneExtension  → registers the pane (Id + Open())
       │    ├─ MenuExtension          → opens the pane via IDockingWindowService.OpenPane
       │    ├─ WebServerExtension     → serves the ui-render-bundle from wwwroot
       │    ├─ engine: mxcli   → Process.Start "mxcli lint --format json" (.star)
       │    ├─ engine: mxlint  → Process.Start "mxlint-cli lint" (.rego, OPA)
       │    │                     output via mxlint.yaml (jsonFile / xunitReport), NO SARIF
       │    └─ sends violations to the webview via the WebView message bus
       │       (IWebView.PostMessage  ⇄  window.chrome.webview)
       │
       └─ web UI (the Phase 1 ui-render-bundle, in the C#-hosted webview)  ← the UI
            ├─ normalizer      → maps engine output to ONE Violation format (section 2)
            ├─ rule registry   → enforces: every ruleId to one engine (section 4)
            ├─ exclusions      → suppress with reason + fingerprint (section 3)
            └─ report          → ACR-style HTML/CSV/JSON (section 5)

TWO SEPARATE MESSAGING BRIDGES (do not confuse — this determines the architecture):
- WebView message bus (window.chrome.webview ⇄ IWebView): C# ↔ web content IN a
  C#-hosted webview. THIS is the bridge between engine output and the UI.
- studioPro.ui.messagePassing (web ↔ web): only between web entry points WITHIN a
  web extension. C# CANNOT participate here.
Consequence: C# CANNOT communicate with the standalone Phase 1 TS pane (studioPro.ui.panes).
The violations pane is therefore C#-HOSTED in Phase 2; it serves the existing Phase 1
ui-render-bundle as content. The render layer (consumes Violation[] via a
ViolationSource) does not need to change — only the data source changes.

Do not depend on the MxLint UI; use only the engine/CLI, show everything in
the CLEVR pane.

NOTE two engines, two tools (do not confuse):
- mxcli (mendixlabs/mxcli): the .star/Starlark engine, supports --format sarif.
- mxlint-cli (github.com/mxlint/mxlint-cli): the .rego/OPA engine, output via
  mxlint.yaml (jsonFile + xunitReport XML), NO SARIF and NO --format flag.

PROVEN by the spike (Studio Pro 11.10, June 2026): a C# extension started via
Process.Start the command `cmd /c echo test` (exitCode 0, stdout "test") and sent
the output via the WebView message bus to the pane, which displayed it as raw text.
Process execution and message passing both work. Confirmed facts (see csharp-spike/):
- Runtime: .NET 10 (the ExtensionsAPI 11.10.0 requires net10.0; net8.0 is NOT
  supported — NU1202).
- Opening the pane: there is NO ViewMenuCaption in 11.10; the route is a MenuExtension that
  calls IDockingWindowService.OpenPane(paneId) (pane appears under Extensions).
Phase 1 (shell + hardcoded JSON) did not depend on this and is already done.

---

## 1. ACR categories & severities (fixed — do not invent your own)

Categories (exactly these six, as ACR displays them):
- Project hygiene
- Maintainability
- Performance
- Architecture
- Reliability
- Security

Severities (ascending): Minor < Major < Critical < Blocker.
(Mapping from engine severity: a .star/.rego rule declares its ACR severity
in metadata; the shell does NOT translate it itself, but reads it from the rule metadata.)

NOTE — scope: these six categories and four severities apply to ACR rules
(`kind: "acr"`, section 2). Generic best-practice rules (`kind: "generic"`,
bundled mxcli / mxlint.com) retain their OWN engine category and severity and
are NOT restricted to this list.

---

## 2. Normalised Violation format (the data contract)

Both engines are mapped to exactly this JSON object. This is the only form that
the UI, exclusions, and the report know.

There are TWO kinds of violations, distinguished by `kind` (the shell shows ALL
available rules, but keeps the ACR identity sharply separated):
- `kind: "acr"`     — a CLEVR ACR rule. Has `acrCode`; `category`/`severity`
                      are exactly one from section 1 and come FROM the rule registry
                      (section 4), not from the engine.
- `kind: "generic"` — a generic best-practice rule from an engine pack (bundled
                      mxcli or mxlint.com). NO `acrCode`; `category` and `severity`
                      are the engine's OWN values (free text, NOT restricted
                      to section 1). `source` shows the origin in the UI.

Required (both kinds): ruleId, kind, source, category, severity, documentType,
documentQualifiedName, reason, fingerprint. ACR-only required: acrCode.
Optional: elementName, suggestion, documentationUrl, documentId.

`documentId` is the stable Mendix document GUID (from the engine, e.g. mxcli). Useful
for navigation (open the document) and as a more robust fingerprint basis later. Optional
because not every engine provides it.

`source` values: `"clevr-acr"` | `"mxcli"` (bundled best practices) | `"mxlint"`
(mxlint.com). The `engine` property (`"star"`|`"rego"`) is kept ONLY for debug; the
UI never displays it. For generic rules the UI shows `source` instead, so that
the user does not mistake an MPR rule for an ACR rule.

ACR rule (`kind: "acr"`):
```json
{
  "ruleId": "CLEVR-PERF-014",          // CLEVR's own stable id (section 4)
  "kind": "acr",
  "source": "clevr-acr",
  "acrCode": "MicroflowDbActionsAtEnd",// original ACR rule name (traceability)
  "engine": "star",                    // debug ONLY
  "category": "Performance",           // exactly one from section 1 (from registry)
  "severity": "Major",                 // exactly one from section 1 (from registry)
  "documentType": "Microflow",
  "documentQualifiedName": "TRB.SUB_X",
  "elementName": "",
  "reason": "Database action is not at the end of the microflow.",
  "suggestion": "Move commit/change actions to the end.",
  "fingerprint": "sha1:...",           // stable hash (section 3)
  "documentationUrl": "https://.../CLEVR-PERF-014",
  "documentId": "c0ffee00-1234-5678-9abc-def012345678" // optional: Mendix document GUID
}
```

Generic rule (`kind: "generic"`, bundled mxcli or mxlint):
```json
{
  "ruleId": "mxcli:MPR-0042",          // stable ENGINE rule id (no CLEVR id)
  "kind": "generic",
  "source": "mxcli",                   // displayed in the UI (origin)
  "engine": "star",                    // debug ONLY
  "category": "MPR",                   // OWN engine category (free text, not section 1)
  "severity": "warning",               // OWN engine severity (free text, not section 1)
  "documentType": "Microflow",
  "documentQualifiedName": "TRB.SUB_Y",
  "elementName": "",
  "reason": "...",
  "fingerprint": "sha1:...",
  "documentId": "..."                  // optional: Mendix document GUID
  // NO acrCode; suggestion/documentationUrl optional
}
```

Exclusions (section 3) and the fingerprint work identically for BOTH kinds
(the fingerprint uses ruleId — for generic the engine rule id).

### documentType — canonical values (BOTH engines map to these)
The normaliser canonicalises the engine `documentType` to exactly this PascalCase form,
so that the UI groups/filters consistently and .star/.rego converge to one form
(e.g. mxcli `"entity"` → `"Entity"`, `"microflow"` → `"Microflow"`). Canonical list:

    Entity, Association, Microflow, Nanoflow, Page, Snippet, Layout, Module,
    Enumeration, ProjectSecurity, ModuleSecurity, Rule, Constant, ScheduledEvent,
    PublishedRestService, ConsumedRestService, MessageDefinition, Image, Document,
    JavaAction, JavaScriptAction

The list is extensible. An ENGINE value not present here (case-insensitive) is passed
through with an initial capital (e.g. `"widget"` → `"Widget"`) and can be flagged as a
deviation, so that a new/unknown type does not silently break grouping.

---

## 3. Exclusions — suppress with reason (critical design decision)

Storage: `$project/.clevr-acr/exclusions.json` (in the project directory, to be committed
so the team shares the same exclusions).

```json
{
  "exclusions": [
    {
      "ruleId": "CLEVR-PERF-014",
      "fingerprint": "sha1:ab12...",
      "reason": "Deliberate bulk migration, handled separately in EPIC-123.",
      "excludedBy": "michel@clevr.nl",
      "date": "2026-06-09",
      "expiry": "2026-12-31"               // optional; active again after this date
    }
  ]
}
```

### Fingerprint strategy (the subtle part — think carefully)
Goal: an excluded violation remains excluded after irrelevant model changes,
but a NEW/DIFFERENT violation is NOT accidentally suppressed along with it.

Recommended fingerprint = sha1 of the stable identity, NOT of volatile text:
    fingerprint = sha1( ruleId + "|" + documentQualifiedName + "|" + elementName )

Deliberately INCLUDED: ruleId, the qualified document, and the sub-element
(widget/attribute) — that is the identity of "what" is being flagged.
Deliberately EXCLUDED: the `reason` text, threshold numbers, or counts — these
change with every model modification and would cause the exclusion to "drift away".

Consequences the builder must explicitly handle:
- RENAMING an element changes documentQualifiedName → fingerprint shifts
  → exclusion lapses and the violation reappears. This is CORRECT (it is effectively
  a different element), but must be visible in the UI as "exclusion no longer
  matches" rather than silently disappearing.
- An exclusion that no longer matches ANYTHING (stale) must be shown by the UI as a
  "stale exclusion" so the team can clean it up — do not silently ignore it.
- Model drift (element added after a scan) is NOT an exclusion matter but a
  new violation; just show it. (cf. the Tabsnippet_Editable_AdL case.)

---

## 4. Rule registry — enforce ACR identity, allow generic rules

The registry has two roles:
- **(A) ACR rules** — explicitly registered, with the golden rule enforced. Become
  `kind: "acr"`.
- **(B) Generic packs** — enabled best-practice packs (bundled mxcli, mxlint.com)
  whose rules are passed through UNCHANGED (with their own category/severity) as
  `kind: "generic"`.

### (A) ACR rules — `$project/.clevr-acr/rules.json`
One CLEVR ruleId has exactly ONE source of truth (one engine). This is ENFORCED.
Each entry binds an engine rule to ACR metadata. `engineRuleKey` is the identifier
with which the engine reports that rule — this is how the normaliser recognises which
engine violation is an ACR rule.
```json
{
  "rules": [
    { "ruleId": "CLEVR-MAINT-001", "acrCode": "EntityAmountAttributes",
      "engine": "star", "engineRuleKey": "ACR_ENT_ATTRS",
      "file": "ACR_ENT_ATTRS.star",
      "category": "Maintainability", "severity": "Minor", "status": "verified" },
    { "ruleId": "CLEVR-PERF-014", "acrCode": "MicroflowDbActionsAtEnd",
      "engine": "rego", "engineRuleKey": "005_db_actions_at_end",
      "file": "005_db_actions_at_end.rego",
      "category": "Performance", "severity": "Major", "status": "todo" }
  ]
}
```

The shell MUST validate on load (the golden rule, unchanged):
- no two entries with the same ruleId,
- no ruleId that has both an active .star and .rego,
- no two ACR entries claiming the same `engineRuleKey`,
- only rules with `"status": "verified"` count in the main ACR report;
  `todo`/`approximate` are shown separately (not mixed in as hard violations).

`status` values: verified | needs-threshold | approximate | todo | out-of-reach.
(This is the "a rule only counts once verified" discipline, anchored in the data model.)

`ruleId` schema: `CLEVR-<CAT>-<NNN>`, where CAT encodes the ACR category
(MAINT = Maintainability, HYG = Project hygiene, SEC = Security, PERF = Performance,
ARCH = Architecture, REL = Reliability), numbered ascending from 001 per category.
`acrCode` is the original rule name (traceability); for our own .star rules this is
the .star rule id (= engineRuleKey), except where a descriptive name exists
(e.g. ACR_ENT_ATTRS → "EntityAmountAttributes").
Severity source: the ACR ground truth (count export) overrides earlier assumptions — e.g.
ACR_ENT_ATTRS is Minor (Maintainability), not Major. A rule without a baseline row
(0 violations on TRB, such as the four Security rules) gets severity `"TODO-confirm"`:
do not invent one, but mark as `verified` (the rule works).

### (B) Generic packs — `$project/.clevr-acr/packs.json`
List of enabled packs; their rules arrive UNCHANGED as `kind: "generic"`
and are NOT registered individually in advance (there are too many). On/off per pack:
```json
{
  "packs": [
    { "source": "mxcli",  "enabled": true, "label": "mxcli best practices" },
    { "source": "mxlint", "enabled": true, "label": "mxlint.com" }
  ]
}
```

### Normaliser mapping (both kinds) — runs in C# (section 9)
For EVERY incoming engine violation:
1. Look up the engine rule key in the ACR registry (A):
   - **MATCH + status = verified** → `kind: "acr"`: take `acrCode` + ACR `category`/
     `severity` FROM the registry (not from the engine), `source: "clevr-acr"`,
     `ruleId` = the CLEVR id.
   - **MATCH + status ≠ verified** → ACR rule "in development": show separately
     (section 5), do NOT count as a hard ACR violation.
   - **NO match** (rule from an enabled pack) → `kind: "generic"`: retain the
     engine's OWN `category` and `severity`, `source` = the pack (`"mxcli"`/`"mxlint"`),
     `ruleId` = the engine rule id. NO acrCode.
2. Calculate `fingerprint` (section 3) and apply exclusions — for BOTH kinds.

PRECEDENCE / DEDUPLICATION (confirmed decision): the ACR registry CLAIMS the check.
If an `engineRuleKey` is in the ACR registry, that violation is NEVER also added as a
generic rule — even if the bundled mxcli/mxlint pack contains the same check.
A claimed-and-verified check appears once as an ACR rule; a claimed-but-not-verified
check appears once in the "in development" section.
Only UNCLAIMED pack rules become generic. This prevents double counting and duplicate
work: every check appears exactly once, with ACR as the authoritative source.

### Engine precedence on overlap (Phase 3B — multiple engines in one overview)
Order: **mxcli > ACR > mxlint**. If more than one engine checks (approximately) the same thing,
the highest in this order wins — mxcli (Mendix's own direction) first, then the
calibrated ACR rule, mxlint last. The losing variant is suppressed.
DELIBERATE CHOICE: on overlap the overview shows the metadata of the WINNING source, not
a blend — this may replace the calibrated ACR metadata with that of the winner.

OVERLAP TABLE (suppressed on the mxlint side; these 6 mxlint Rego rules check
the same thing as existing ACR rules, so ACR wins and the mxlint variant is dropped):

| mxlint rule (rulenumber) | ACR rule | subject |
|---|---|---|
| AnonymousDisabled (001_0001)        | ACR_SEC_GUEST      | anonymous/guest access |
| DemoUsersDisabled (001_0002)        | ACR_SEC_DEMOUSERS  | demo users |
| SecurityChecks (001_0003)           | ACR_SEC_CHECKED    | security check on |
| StrongPasswordPolicy (001_0004)     | ACR_SEC_PWPOLICY   | password policy |
| NumberOfAttributes (002_0002)       | ACR_ENT_ATTRS      | number of attributes |
| AvoidUsingValidationRules (002_0007)| ACR_ENT_VALRULES   | validation rules |

The suppression happens deterministically in the mxlint normaliser (by rulenumber); the
remaining mxlint rules pass through normally as `source: "mxlint"`.

---

## 5. Report (ACR style)

ONE overview: ALL improvements (ACR + generic) distributed across the SIX ACR categories
(section 1). The ORIGIN remains always visible (badge per rule + breakdown in the
count card), so that a developer never mistakes a calibrated ACR rule for an
engine-generic rule — the separation is now in the badge, not in a separate section.

1. App info (project name, date, Mendix version, # scanned documents).
2. Count card — counts ALL improvements:
   - per category (the six, section 1),
   - per severity (see severity display below),
   - per ORIGIN: ACR (calibrated) / MxCLI Mxlint / Mxlint.com.
3. Improvements — grouped by the six ACR categories (section 1) and WITHIN each
   category by rule (grouping model below). Each rule shows an
   ORIGIN badge (ACR / MxCLI / Mxlint.com); ACR rules also show their `acrCode`.
4. Exclusions separately visible — what is suppressed, by whom, why, since when.
   Applies to both kinds; `kind` remains visible.

### Category of generic rules — DISPLAY mapping (per-rule mxcli category)
Generic rules retain INTERNALLY their own engine category (section 2: `Violation.category`
= the mxcli prefix, unchanged). For the report they are placed in one of the six
ACR categories. The mapping lives in the render layer; the Violation contract does not change.

CORRECTED (mapping bug): map on the **REAL per-rule mxcli category**, NOT on the
ruleId prefix. mxcli provides a category per rule (style/quality/correctness/performance/
…) — queryable via `mxcli lint --list-rules` (which we already fetch for rule names;
each ruleId has a `Category:` line). The prefix was too coarse: the `CONV` prefix contains
e.g. performance (CONV011 NoCommitInLoop), naming (CONV001) and quality rules mixed together,
causing Performance to be empty (fed only by the non-existent prefix `PERF`) and
Reliability to have no mapping.

| mxcli category  | ACR category     | reason |
|-----------------|------------------|--------|
| security        | Security         | security |
| naming          | Project hygiene  | naming conventions → clean project |
| style           | Project hygiene  | style/convention → clean project |
| quality         | Maintainability  | code quality → maintainability |
| complexity      | Maintainability  | (McCabe) complexity → maintainability |
| maintainability | Maintainability  | direct |
| design          | Architecture     | design/structure → architecture |
| architecture    | Architecture     | architecture |
| **correctness** | **Reliability**  | **deliberate translation: runtime correctness (crashes, empty validation) ≈ reliability** |
| performance     | Performance      | performance |

Unknown/new mxcli category → **Maintainability** (fallback, to be revisited). If the
mxcli category is missing (e.g. `--list-rules` could not be loaded), a coarse
prefix fallback applies (SEC→Security, ARCH/DESIGN→Architecture, PERF→Performance, otherwise
Maintainability). ACR rules (`kind: "acr"`) use their registry category (section 4),
not this table.

Verified on TRB (2625 improvements): with this mapping **Reliability = 388** and
**Performance = 11** (both previously empty); the remaining distribution Project hygiene 491 /
Maintainability 1080 / Architecture 252 / Security 403.

### Category of mxlint rules — DISPLAY mapping (Phase 3B)
mxlint (Rego) delivers its own category LITERALLY in the failure message (section 2:
`Violation.category` remains unchanged). Those categories differ from the six ACR names
and are mapped for the report — again purely in the render layer, contract unchanged:

| mxlint category  | ACR category    | reason |
|------------------|-----------------|--------|
| Security         | Security        | security |
| Maintainability  | Maintainability | direct |
| Performance      | Performance     | performance |
| **Accessibility**| **Maintainability** | **deliberate choice: no own ACR category → maintainability** |
| Microflows       | Maintainability | microflow structure → maintainability |
| Complexity       | Maintainability | complexity → maintainability |
| Error            | Reliability     | runtime errors ≈ reliability |

Unknown mxlint category → **Maintainability** (fallback). Present on TRB:
Maintainability, Accessibility (→ Maintainability), Security.

### Severity display
- ACR rules: their ACR severity (Minor/Major/Critical/Blocker, from the registry, section 4).
- Generic rules: LITERALLY the mxcli engine severity (error/warning/info/hint) —
  NOT translated to an ACR severity (no invented severity). Both are shown as
  severity chips in the same way.
- An ACR severity outside the four (e.g. `TODO-confirm`) falls under "To be confirmed".

### GROUPING MODEL — PER RULE, not per instance (like ACR's "Summary per rule"):
- Each rule appears EXACTLY ONCE as a rule entry (rule id/acrCode,
  origin, severity, total number of improvements).
- Below it, expandable/nested, ALL instances matching that rule
  (document + elementName + reason per instance).
- A rule with 12 instances is one rule entry with 12 nested cases —
  NOT 12 separate rule entries. A rule with no instances is not shown.

Exports: HTML (primary, ACR look), CSV and JSON (for CI/pipeline). The count card counts
all improvements; the origin breakdown keeps visible what is calibrated ACR
(`verified`) and what is engine-generic — so the numbers are both honest and complete.

---

## 6. MVP build order (contract first, then integrations)

1. Extension skeleton in Studio Pro (TypeScript / Web Extensibility API).
2. CLEVR ACR pane with HARDCODED example JSON in ACR layout
   (proves the UI/report before any engine integration).
3. Integrate engine 1: `mxcli lint --format json` → normaliser → pane.
   FIRST VERIFY that this JSON output exists and is stable (see section 7).
4. Integrate engine 2: MxLint/Rego CLI → normaliser → pane.
   FIRST VERIFY that a Rego flow-graph rule is feasible (see section 7).
5. Exclusions (read/write .clevr-acr/exclusions.json + fingerprint logic).
6. Report export (HTML/CSV/JSON).
7. Only THEN: migrate rules according to Green/Blue/Orange/Red.

---

## 7. Assumptions — status after exploration (June 2026)

1. mxcli JSON output: STILL TO BE CONFIRMED empirically. We know mxcli has `--format sarif`;
   whether `--format json` exists and is usable, determine by running it on
   TRB. Establish the exact schema BEFORE the normaliser.
2. MxLint/Rego headless CLI: CONFIRMED. `mxlint-cli lint` (Go CLI, OPA/Rego), output
   via mxlint.yaml (jsonFile + xunitReport XML). NO SARIF, NO --format flag. Exact
   JSON schema still to be established empirically BEFORE the normaliser.
3. Rego flow-graph rule (complexity / db-actions-at-end) feasible + calibratable against
   ground truth (315 / 31): STILL TO BE PROVEN. Validates the "two engines" rationale, but
   does not block the MVP (the mxcli engine alone delivers a working product).
4. Own pane + process execution: PROVEN (spike, Studio Pro 11.10, June 2026).
   - Own dockable pane: CONFIRMED. In Phase 1 via studioPro.ui.panes (web); for
     the final product C#-hosted via DockablePaneExtension + IDockingWindowService.OpenPane.
   - Process execution + message passing: PROVEN. C# extension ran `cmd /c echo test`
     via Process.Start (exitCode 0, stdout "test") and delivered the output via the
     WebView message bus (IWebView.PostMessage ⇄ window.chrome.webview) to the pane,
     which displayed it as raw text. No longer a loose assumption; see csharp-spike/.
   - Runtime: .NET 10 (ExtensionsAPI 11.10.0 requires net10.0; net8.0 → NU1202).

Node.js/npm: CONFIRMED present (v24 / npm 11).

Order consequence: Phase 1 (shell + hardcoded JSON in the pane) is done. The
C# process execution spike — the gateway to Phase 2 — has SUCCEEDED. Assumption 1 (mxcli
JSON schema) is now the FIRST Phase 2 step: it gates the normaliser and therefore the
entire engine integration until it is resolved. Assumption 3 (Rego flow-graph) remains
open but is only relevant for engine 2. See the Phase 2 plan in section 9.

---

## 8. What is already done (Blue tier, verified)

11 verified .star rules (see acr-mxlint-voortgang.md) are the first
`verified` entries in the registry. The shell does not need to rebuild those;
they fill the Blue column and immediately prove the mxcli engine integration with real data.

---

## 9. Phase 2 — plan (after successful spike)

Architecture decision (follows from section 0 + the spike): the violations pane will be
C#-HOSTED. The C# backend runs the engines via Process.Start and serves the existing
Phase 1 ui-render-bundle as content (WebServerExtension → wwwroot).

DECISION — NORMALISATION RUNS IN C# (not in the web layer):
Both engines arrive via Process.Start in the C# layer (mxcli → JSON,
mxlint-cli → XML/JSON). C# maps them there to one `Violation[]` (section 2), so that the
web layer remains ENGINE-UNAWARE — a hard spec requirement (the user must not know whether
a rule comes from .star or .rego). Furthermore `fingerprint` (section 3) and applying
exclusions belong to the normalised Violation object; C# calculates the
fingerprint and applies exclusions BEFORE the data goes to the webview. Consequence:
- C# = run engines + normalise + fingerprint + exclusions + registry validation.
- web layer = PURE presentation. The render layer remains unchanged and consumes
  `Violation[]` via a ViolationSource; in Phase 2 a new ViolationSource
  delivers the (already normalised) data from the C# backend — via the WebView message bus or an
  HTTP route from the built-in web server.

Build incrementally — each step is independently verifiable before the next starts.
Step 0 (`cmd /c echo test` → pane) is ✅ DONE via the spike (process execution + message
passing proven end-to-end). The Phase 2 steps:

1. FIRST PHASE 2 STEP — establish the REAL JSON schema of `mxcli lint --format json`
   on TRB. This precedes everything else: the normaliser (step 3) can only be
   written once this schema is known, and it resolves assumption 1 (we know that
   `--format sarif` exists; `--format json` + the exact schema must be confirmed empirically).
   Precondition: `mxcli` is callable from the C# extension (PATH or
   full path — if necessary first check briefly with `mxcli --version`).
2. Run `mxcli lint --format json` on TRB from C# and display the raw JSON in the pane
   (proves the real engine call + that the schema matches step 1).
3. Normaliser IN C# (engine JSON → `Violation[]` per section 2): map mxcli JSON to
   the data contract. Determine `kind` per violation via the ACR registry match (section 4):
   claimed+verified → `kind: "acr"` with ACR metadata; unclaimed pack rule →
   `kind: "generic"` with own category/severity + `source`. Calculate `fingerprint` and
   apply exclusions (section 3) — all in C#, for both kinds.
4. Violations in the C#-hosted pane: replace HardcodedViolationSource with the
   backend source, so that the real (normalised) mxcli violations appear in the ACR layout.
   The web layer does not change.
5. Only THEN: engine 2 (mxlint-cli, .rego — assumption 3) through the same normaliser,
   exclusions UI (section 3), report export (section 5), rule migration
   (Green/Blue/Orange/Red).

Open assumptions affecting Phase 2:
- Assumption 1 (mxcli `--format json` schema): is literally step 1 — blocks the
  normaliser and therefore the entire engine integration until resolved.
- Assumption 3 (Rego flow-graph rule calibrates against 315/31): only relevant for engine 2
  (step 5); the mxcli engine alone already delivers a working product.
