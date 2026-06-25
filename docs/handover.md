# CLEVR ACR — Project Handover (final state June 2026)

> **How to use:** paste this document into a new conversation with Claude (or hand it to Claude Code) to seamlessly continue work on the CLEVR ACR extension. It describes what the project is, where it stands, how it works, and the working practices that underpin it. Read it alongside **`clevr-acr-shell-status.md`** (the living compass, with all measurement and decision details) and **`clevr-acr-shell-spec.md`** (the data contract).

---

## 1. What is this project?

The **CLEVR ACR** extension is a Mendix Studio Pro 11 extension (an "ACR shell") that displays lint findings ("improvements") in ACR style: six categories (Security, Reliability, Performance, Architecture, Maintainability, Project hygiene), severities Minor < Major < Critical < Blocker, clickable filters, exclusions with reason, manual checks, and an exportable HTML report.

**Core idea:** an *aggregator* of model-readable findings — mxcli's own lint + CLEVR's own calibrated rules + CLEVR context — **not a replacement** for ACR or the live Studio Pro analysis.

**Requirement:** Mendix 11+ (ExtensionsAPI 11.10 / .NET 10). Does not work on Mendix 10.

---

## 2. MOST IMPORTANT CHANGE vs. earlier handovers: mxlint has been FULLY REMOVED

The project once ran on **two** engines (mxcli + mxlint.com/Rego) and read a **modelsource YAML export**. All of that is gone. **Final state: one engine, Apache-2.0 — mxcli.**

- **No mxlint** anymore: no binary, no download, no bootstrap step, no Rego, no `modelsource/` export. All rules read via **mxcli** (catalog/describe/lint/.mpr). Verified: **0 active `modelsource/` readers**.
- **mxcli** is now **v0.12.0** (the installer fetches 'latest'; older docs mention v0.11.0 — outdated).
- Data sources (all mxcli):
  - `mxcli lint -p "<app>.mpr" --format json` — mxcli's own ~60 rules (the generic findings).
  - **CATALOG** (SQLite, `mxcli -c "SELECT … FROM CATALOG.*"`) — entity/attribute/module/microflow/constants/associations metadata. Fast (prebuilt `catalog.db`, ~0.4s/query). **Known limitations:** `ATTRIBUTES.Length=0` for all strings (length not readable), user-role↔module-role not available as a table.
  - **`describe <type> <name>`** — renders an element as MDL (the only source for microflow control flow/expressions). **~1s per describe** (see §6).
  - `describe projectsecurity` / `describe userrole <r>` / `project-tree` — security config (replacing the YAML export).

---

## 3. Architecture (hybrid C# + web, unchanged in structure)

- **C# backend** (.NET 10 DLL, in-process in Studio Pro) runs mxcli via `Process.Start`, normalises to a single `Violation` contract (see spec §2), and sends via the WebView2 message bus to:
- **Web render layer** (JS + HTML/CSS in `wwwroot/`): pure presentation — filters, category display mapping, count cards, report HTML.
- **Split:** rule logic + normalisation in C#; the web layer only displays. UI term = "Improvements", internal type = "Violation".
- **WebView2 detail:** `PostMessage` on the UI thread; heavy work via `Task.Run`, marshalled back via the captured `SynchronizationContext`.
- **Debug log:** `<project>\.clevr-acr\mxlint-debug.log` (internal name unchanged).

### Codebase (`C:\Apps\clevr-acr-shell`)
- **`csharp-spike/`** — the extension (I/O + wiring): `SpikeDockablePaneViewModel` (buttons + message bus), `AcrScanService` (orchestration + streaming), `MxcliCatalogService` / `MxcliDescribeService` / `MxcliSecurityService` (the three providers), `ProcessRunner`, `ReportExporter`, `ExclusionStore`, `ManualCheckStore`, `wwwroot/` (index.html + main.js), `rules.json`, `manifest.json`. NuGet: YamlDotNet (16.2.0, still used by deprecated backup code), Mendix.StudioPro.ExtensionsAPI (11.10, not deployed alongside). `MxlintScanService` + the YAML/Page readers remain as **deprecated backup code**, never called.
- **`csharp-normalizer/`** — pure, dependency-free .NET library: `Violation`, `RuleRegistry`, `Fingerprint`, the normalizers, `CatalogRules`, `ProjectSecurityParser`, `ExpressionRules`/`MicroflowStructureRules`/`DescribeEntityRules`/`DescribeMicroflowExpressions`, + **232 unit tests** (green).
- **`dist/CLEVR-ACR-extension/`** — the shareable end-user package (installer + README + `clevracr/` payload). Built by `Build-Package.ps1`.
- **`_reference/`** — unpacked mxlint source code (historical; mxlint is out of the product).

---

## 4. The rules — final state (all live-verified on TRB)

**12 rules migrated to mxcli (7 catalog + 5 describe), NO export anymore:**

- **Catalog route (7)** — `MxcliCatalogService` → pure `CatalogRules`, from `CATALOG.*`:
  MAINT-007 (microflow size), MAINT-010 (default value), MAINT-014 (number of modules), SEC-011 (exposed constants), PERF-001 (inherit Administration.Account), SEC-007 (System association), SEC-009 (hash algorithm).
- **Describe route (5)** — `MxcliDescribeService` → pure rules, from `describe microflow/entity`:
  MAINT-008 (complexity without annotation), MAINT-009 (nested ifs), REL-001 (redundant empty-string check), REL-002 (incomplete empty-string check), MAINT-013 (default ReadWrite access). **MAINT-006** (redundant boolean) also moved from the YAML route to this describe sweep (deepscan); ground truth TRB = **104** (more complete than the old 94 — now also catches variable assignments).
- **Security route** — `MxcliSecurityService` (synthesises an equivalent security YAML from `describe projectsecurity`/`userrole` + `project-tree`, feeding the existing pure `ProjectSecurityParser`):
  SEC-008 (admin = MxAdmin), SEC-005 (anon create on persistent entity), SEC-010 (per-userrole check-security), MAINT-005 (≤1 module role/module), + SEC-004 (guest access on). SEC-008 + SEC-005 run in the **fast** scan (≤1 `describe userrole`); SEC-010 + MAINT-005 in the **deepscan** (all 9 userrole describes).

**4 topics DEFERRED to mxcli's own rules** (mxcli already covers them; our emission suppressed to avoid double-counting):
- MAINT-011 (too many persistent entities) → mxcli **MPR003**
- PERF-002 (too many virtual attributes) → mxcli **CONV017**
- MAINT-012 (validation rules) → mxcli **ACR_ENT_VALRULES / CONV015**
- commit-in-loop (PERF) → mxcli **CONV011**

The claim table + two tripwire tests guard the cross-engine deduplication (no check appears twice).

---

## 5. Two scan modes + streaming

- **Fast scan (Scan button):** mxcli lint + catalog route + security (SEC-008/005) + manual checks. ~17s warm / ~55s cold (first scan = mxcli lint rebuilds `catalog.db`). NO describe sweep.
- **Deepscan button:** everything from the fast scan **+** the describe sweep (MAINT-008/009, REL-001/002, MAINT-013, MAINT-006) **+** security SEC-010/MAINT-005. Minutes to, for large apps, an hour — see §6.
- **Streaming (both modes):** `AcrScanService.RunScanStreaming(deepScan, emit)` posts findings in **batches** — first the FAST batch (lint+catalog+security, ~seconds), then during deepscan the describe findings **per chunk** (`DescribeStreamChunkSize=30` elements, ~11-15s warm / ~25-37s cold per chunk) with progress ("microflows 80/472"). The UI (`main.js`) shows a **progress banner** + marks the totals as **"Total (so far)"** until the final batch (`final=true`) — an intermediate state can never be read as a final state. A chunk that returned fewer results than requested → **red LOUD warning** (no silent fewer-findings). **Proven:** the sum of all batches is byte-identical to the non-streamed scan (2865 = 2865 on TRB).

---

## 6. The deepscan slowness (investigated to the root) — streaming is the mitigation, not a warm mxcli mode

`describe` costs **~1s per element** (warm ~0.5-0.64s, cold ~1.1s), measured and verified. This is a **hard mxcli compute floor**, not a model-load artefact: within a single `-c` session (model already loaded) each describe still costs ~1s. Measured apples-to-apples (same warmth, .NET): chunks-of-200 (301s) ≈ single large `-c` (310s) ≈ `exec` script file (346s) — **one session is NOT faster**. mxcli v0.12.0 has persistent modes (REPL, `exec`, `lsp`, `serve`) but these only keep the model warm within a session = the model load that chunking already amortises; they do not break the per-describe floor. There is **no bulk-describe and no expression export** in mxcli.

**Extrapolation (linear):** TRB (472 microflows) = ~4:41 warm / ~10 min cold; 2000 microflows ≈ 20-45 min; 5000 ≈ 51-112 min. → **streaming is essential for the deepscan** (making the time bearable). The only real order-of-magnitude gain would be the removed bulk export (~19s for everything) — reopens the mxlint decision; not recommended.

---

## 7. THREE PARKED REACTIVATIONS (code is ready as backup)

1. **SEC-006** (unlimited string editable by anonymous) — **deprecated**. mxcli does not return the string MAX length (`CATALOG.ATTRIBUTES.Length=0` for all strings; `describe entity` also renders `Length:200` as `String(unlimited)` — confirmed, even after a fresh catalog rebuild). `CatalogRules.AnonymousEditableUnlimitedString` + the read code remain as unused backup. **Reactivate once mxcli reliably returns string Length (String(N) vs String(unlimited)).**
2. **MAINT-015** (inline style) + **REL-003** (alt text) — **deprecated**. mxcli does not expose `WIDGETS.Style`/alt-text and `describe page` is lossy. `PageRules` + `PageYamlReader` remain as backup. **Reactivate once mxcli exposes WIDGETS Style/alt-text.**
3. **Severity calibration** — the high-volume rules on TRB: **MAINT-007 = 30**, **MAINT-008 = 129**, **MAINT-010 = 592**. Their severity/default suppression has not yet been calibrated against noise; to be discussed with Michel (possibly lower default severity or a threshold). The numbers are real (verified), not fabricated.

**Known cosmetic cleanup (no functional impact):** three stale comments still mention "export"/`MxlintViolations` as scan output — `SpikeDockablePaneViewModel.cs` (~l.87-88 and ~l.642) and `main.js` (~l.1366). The associated code is dead/backup and is never called (verified: the buttons only post `RunFullScan`/`RunDeepScan` → `AcrScanService`, never `RunMxlintScan`/`MxlintViolations`). May be cleaned up when convenient; requires one rebuild+repack.

---

## 8. Core value & working practices (IMPORTANT — this is what carries the project)

**"A feature/rule that runs is not necessarily correct."** The discipline that made every rule reliable:

1. **Show the REAL data + the ground truth on the test project FIRST, then build, then verify.** Never fabricate categories/severities/counts.
2. **Suspicious outcomes (a "0", an unexpected number, a 2× faster result) = a signal to dig deeper, not to accept.** Time and again a data-source error turned out to be the cause (empty CATALOG column, stale catalog, a warmth confound in timing) — not the truth.
3. **"Works in my probe" ≠ "passes through the real scan pipeline."** Measure via the real code paths (instrumented driver against the real provider classes), not loose bash experiments — bash argument truncation once produced a false "0 findings".
4. **Stop at a deviation; report per phase.** Don't blindly chase an expected number — if the describe route gives 104 instead of 94, identify whether it is scope/metric/bug.

**Division of roles:** chat Claude = sparring partner/prompts. **Claude Code** (on `C:\Apps\clevr-acr-shell`) = builds & verifies. Michel tests in Studio Pro and pastes results back.

**Deploy:** no more manual copying. `Build-Package.ps1` builds + assembles `dist/CLEVR-ACR-extension(.zip)`; `Install-ClevrAcr.ps1` installs into `<project>\extensions\clevracr` and **automatically fetches mxcli** (GitHub release, sha256-verified, after a single Y/n confirmation). Then restart Studio Pro; fixes apply to NEW scans.

---

## 9. Security inventory (for the security officer)

- **Languages/runtimes:** C# on .NET 10 (backend DLLs, in-process), JavaScript + HTML/CSS (web render layer in WebView2).
- **NuGet:** `YamlDotNet` 16.2.0 (now only used by deprecated backup code), `Mendix.StudioPro.ExtensionsAPI` 11.10 (supplied by Studio Pro, not deployed alongside).
- **External binary:** `mxcli.exe` (v0.12.0, **Apache-2.0**) — **auto-downloaded** by the installer from the official GitHub release, **sha256 + byte size verified** before use, stored in `%LOCALAPPDATA%\clevr-acr\mxcli\`. **No more mxlint** (the previous supply-chain concern about the auto-downloaded mxlint binary is no longer applicable).
- **Recommendation:** periodically run `dotnet list package --vulnerable --include-transitive`; document which mxcli version is downloaded.

---

## 10. Suggested opening prompt for a follow-up session

> I am taking over CLEVR ACR (see the handover + `clevr-acr-shell-status.md` + `clevr-acr-shell-spec.md`). Final state: mxlint fully removed, everything via mxcli (Apache-2.0), 12 rules migrated, two scan modes with streaming. Three parked reactivations (SEC-006 string Length; MAINT-015/REL-003 WIDGETS Style/alt-text; severity calibration MAINT-007/008/010). Before we build anything: confirm the current facts — build + 232 tests green, and `mxcli --version` on the laptop. No build action; verify the state first.
