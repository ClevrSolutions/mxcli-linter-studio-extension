# CLEVR Lint — Rule Inventory

## Categories and severities

Six Lint categories (fixed):

| Category | Covers |
|----------|--------|
| Security | Access control, encryption, anonymous exposure |
| Reliability | Null checks, error-prone patterns |
| Performance | Expensive queries, large entity graphs |
| Architecture | Structural violations, module boundaries |
| Maintainability | Complexity, naming, size limits |
| Project hygiene | Configuration, module setup |

Four severities (ascending): `Minor` < `Major` < `Critical` < `Blocker`

## 12 active CLEVR rules

These rules are implemented in `Clevr.Lint.Normalizer` and run via `LintScanService`. All verified on the test reference project.

### Catalog route (7 rules)
Sourced from `mxcli -c "SELECT … FROM CATALOG.*"` (fast, ~0.4s/query).

| Rule ID | Name | Notes |
|---------|------|-------|
| MAINT-007 | Microflow size | High-volume on large projects (30 on Test) |
| MAINT-010 | Default value missing | High-volume (592 on Test) |
| MAINT-014 | Number of modules | Per-project |
| SEC-011 | Exposed constants | |
| PERF-001 | Inherits Administration.Account | |
| SEC-007 | System association | |
| SEC-009 | Hash algorithm | |

### Describe route (5 rules + 1 deepscan-only)
Sourced from `mxcli describe microflow/entity <name>`. Deepscan only (~1s per element).

| Rule ID | Name | Notes |
|---------|------|-------|
| MAINT-008 | Complexity without annotation | High-volume (129 on Test) |
| MAINT-009 | Nested ifs | |
| REL-001 | Redundant empty-string check | |
| REL-002 | Incomplete empty-string check | |
| MAINT-013 | Default ReadWrite access | |
| MAINT-006 | Redundant boolean | Catches variable assignments (104 on Test) |

### Security route (4 rules)
Synthesized from `describe projectsecurity` + `describe userrole` + `project-tree`.

| Rule ID | Name | Scan mode |
|---------|------|-----------|
| SEC-008 | Admin = MxAdmin | Fast |
| SEC-005 | Anonymous create on persistent entity | Fast |
| SEC-010 | Per-userrole check-security | Deepscan |
| MAINT-005 | ≤1 module role per module | Deepscan |

SEC-004 (guest access on) is also present in the security route.

## 4 topics deferred to mxcli's own rules

These are covered by mxcli's built-in lint and deliberately not emitted by CLEVR rules to avoid double-counting. Suppression is enforced by a claim table + two tripwire unit tests.

| Topic | mxcli Rule |
|-------|-----------|
| Too many persistent entities (MAINT-011) | MPR003 |
| Too many virtual attributes (PERF-002) | CONV017 |
| Validation rules (MAINT-012) | ACR_ENT_VALRULES / CONV015 |
| Commit in loop (PERF) | CONV011 |

## 3 parked reactivations

Rules that are implemented but disabled pending mxcli capabilities.

**SEC-006 — Unlimited string editable by anonymous**
Blocked: `CATALOG.ATTRIBUTES.Length = 0` for all strings. `describe entity` also returns `String(unlimited)` regardless of actual limit. Code exists in `CatalogRules.AnonymousEditableUnlimitedString`.
Reactivate when: mxcli reliably returns `String(N)` vs `String(unlimited)`.

**MAINT-015 — Inline style + REL-003 — Alt text missing**
Blocked: mxcli does not expose `WIDGETS.Style` or alt-text; `describe page` output is lossy.
Code exists in `PageRules` + `PageYamlReader`.
Reactivate when: mxcli exposes WIDGETS style/alt-text.

**Severity calibration for MAINT-007, MAINT-008, MAINT-010**
These three rules produce high volumes on large projects and have not been calibrated against noise. Current numbers are real (verified on Test) but the severity/default suppression thresholds need tuning before customer rollout.
