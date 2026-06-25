# CLEVR ACR Shell — status & roadmap

Working document. What is done, what remains, in what order, and what must be PROVEN
per phase before building on it. Discipline throughout the project:
"a rule/feature that runs is not a rule/feature that is correct" — verify against
real data, invent nothing, prove assumptions before building on them.

MXLINT.COM REMOVED FROM THE UI (display only; export UNAFFECTED). Removed from the panel + report:
(1) count-card source "Mxlint.com" — removed from the "per source" array (main.js) + the two hardcoded
breakdown strings (`… / ${oc.mxlint} Mxlint.com` → gone) in panel status and report subtitle;
(2) source-filter-checkbox — `{key:"mxlint",label:"Mxlint.com"}` removed from ORIGINS; (3) report raw block —
the orphan `.acr-mxlint-raw` CSS (227-234) in index.html (the table builder was already gone; CSS was still
embedded in every report via buildReportHtml); (4) panel texts — subtitle "via mxlint + mxcli…"
→ "via mxcli + the CLEVR rules", scan tooltip "mxlint export + lint…" → "exports the model source…".
"MxCLI Mxlint" (source=mxcli) REMAINS visible. The source==="mxlint" branches in originLabel/originBadge
remain as dead, harmless guards (engine no longer emits mxlint violations) — just like the
claim-table entries (tripwires monitor them). index.html now has 0 mxlint refs.
README check: the mxlint extension BOOTSTRAP step (section 5) is NOT obsolete — the EXPORT needs the binary;
already rewritten as "for the model export" (not "extra Rego rules"). Deliberately retained.
VERIFICATION: YAML routes intact (MAINT-010=283, MAINT-015=33, SEC-011=0); build 0/0, tests 200/200
(tripwires green). REPACKAGED: dist\CLEVR-ACR-extension(.zip) refreshed (17:21), TRB check OK, no settings
in payload, index.html in payload 0 mxlint, count card shows "ACR / MxCLI Mxlint / Manual". Scan test: Michel.

REGO ENGINE DISABLED as findings source (export REMAINS). MxlintScanService: only `export`
(refreshes modelsource/), the `lint` call + MxlintNormalizer + lint-results.json reading are gone →
payload source="mxlint-export", regoEngineDisabled=true, violations=[]. Binary UNAFFECTED (shared with
export). RunFullScan order unchanged (export first, then mxcli+CLEVR). main.js: handleMxlintResult
now shows "model export refreshed (Rego engine disabled)" instead of the lint count; replaceOrigin([],["mxlint"])
clears any mxlint origin. Installer had NO mxlint download/gate (only mxcli) → nothing to remove;
README/guide rewritten: mxlint = "model export", no longer "extra Rego rules". mxlintPath setting REMAINS
(export needs the binary). VERIFICATION: YAML routes intact on current modelsource — MAINT-010=283,
MAINT-015=33 (expected); modelsource present (12 domain models, 113 pages); no lint call/Normalize anymore
in MxlintScanService (export only). Claim-table entries remain (no harm, tripwire monitors them).
Build 0/0, tests 200/200. PACKAGE REPACKAGED: Build-Package.ps1 → dist\CLEVR-ACR-extension(.zip) refreshed
(new DLLs + main.js + README), TRB safety check OK, no settings in payload. Local dev settings
(TRB paths) UNAFFECTED. Real Studio Pro scan test: Michel.

mxlint FULLY REMOVED (A-D); PHASE E duration check DEVIATES → stopped before repack.
PHASE A: MAINT-006 → describe sweep (deepscan). Verified live: 104 vs old YAML GT 94. DEVIATION =
METRIC (no loss): describe ⊇ YAML (all 94 + 10 extra), the 10 extras are real redundant boolean
comparisons in VARIABLE ASSIGNMENTS ($Valid/$Valide/$Vrijspraak…) that the old YAML route (only split
conditions + change values) did not extract. 104 = new, more complete GT. Synthetic test added.
PHASE B: SEC-006 deprecated (DetectAnonymousEditRules not wired; ProjectSecurityParser read code =
backup with "reactivate once mxcli String(N) reads back"). No claim-table/tripwire entry (no mxlint twin).
PHASE C: gate check → 0 active modelsource readers (3 active providers: security/catalog/describe, all
describe/.mpr/catalog). ✓
PHASE D: mxlint export call removed from RunFullScan (both modes). Installer: mxlintPath setting gone. README:
section 5 (mxlint extension bootstrap) removed, intro/warning/config/troubleshooting rewritten (no
mxlint, mxcli automatic, Scan+Deepscan). MxlintScanService + YAML readers remain as deprecated backup.
PHASE E: build 0/0, tests 232/232. BUT the duration check DEVIATES: fast scan ≈ ~46s, NOT ~12s. Cause:
the security route (SEC-005/008/010 + MAINT-005) runs in the FAST scan and costs ~32s — primarily the 9
`describe userrole` calls (~20s) that are NOT batchable (MDL has no DESCRIBE USERROLE statement, only
the CLI subcommand) + project tree. The export (19s) disappeared but the security route (~32s) came back in its place.
STOPPED before repack. RECOMMENDATION (mode placement decision for Michel): move the userrole-heavy
rules SEC-010 + MAINT-005 to DEEPSCAN (like the other describe-heavy rules) → fast scan drops to
~26s; or split the security route (SEC-008 projectsecurity + SEC-005 catalog stay cheap/fast, SEC-010+
MAINT-005 to deep) → fast ~14s, close to the target. Only then repack. The mxlint removal itself is correct
and complete; only the fast/deep placement of the security rules needs to be decided before we claim the duration.

STREAMING BUILT (progressive findings, both modes) — PHASES 1/2/3 done, build+tests green, repackaged.
PHASE 1 (orchestration): AcrScanService.RunScanStreaming(deepScan, Action<string> emit) — shared RunFastPhase
(lint+security+catalog+rule catalogue) → emit FAST batch (full metadata + fast findings, final=!deep);
for deep then MxcliDescribeService.StreamViolations(chunkSize, emit) → one batch per chunk with progress.
DescribeStreamChunkSize=30 (empirically: ~11-15s warm / ~25-37s cold per chunk — model loads per chunk again,
deliberately accepted). RunScanAsJson retained (non-streamed, RunAcrScan route) via the same RunFastPhase.
Batch payload: {phase:"fast"|"describe", final, progress:{processed,total,label,requested,returned}, violations,
+ metadata only on fast}. ViewModel.RunFullScan posts every batch as "AcrViolations". VERIFIED with driver
(deep): 21 batches, streamed sum == non-streamed EXACT (2865=2865; MAINT-006=104, REL-001=31, MAINT-008=129,
MAINT-009=1, MAINT-013=1, …), 0 incompleteness warns, sawFinal=True. Rule logic/claim table/tripwires byte-
identical — only when/in how many pieces differs.
PHASE 2 (UI, main.js): handleMxcliResult branches on phase — fast=replaceOrigin (clean slate + metadata),
describe=APPEND (concat, no replace). final=false → scanStreaming=true. streamingBanner() (inline styled,
unmistakable) + totalRow "Total (so far)" + "{n}…" while streaming → interim state NEVER shown as final state. Progress
"microflows X/472". scanIncomplete (returned<requested) → red LOUD warning. Scroll position preserved across
rerenders. Safety net finalizeStreaming() on ScanFinished (also rounds off if final batch is missing, e.g. 0 user
microflows). Backward compat: payload without phase = fast+final (old form).
PHASE 3: build 0/0, tests 232/232, Build-Package repackaged (TRB check green), streaming code in package (main.js).
The real UI test (both buttons, drip-in) is Michel's. Unchanged: rule logic, numbers, claim table/
tripwires, two-button setup.

ROOT INVESTIGATION warm/persistent mxcli mode (June 23) — CONCLUSION: NO root fix in mxcli, streaming is the way.
mxcli is v0.12.0 (installer grabbed 'latest'; "v0.11.0" in context is outdated). v0.12.0 has persistent
modes (REPL `mxcli` without args; `exec script.mdl`; `lsp`; `serve`) BUT these only keep the model warm
within one session = model load ×1 — which our chunk-of-200 already amortises. No bulk describe and no
expression/control flow export (docs + `describe module` tested: that only gives `create module X;`, not the
content). APPLES-TO-APPLES measured (same warmth, back-to-back, all via .NET ProcessRunner, 472 microflows):
  A) chunks-of-200 (3 processes): 301.5s (639 ms/mf)  B) one large -c (1 process, 27510 chars): 310.5s (658
  ms/mf)  C) exec script file (1 process): 346.1s (733 ms/mf). All three ~equal; chunk-200 even marginally
FASTEST. Single session is NOT faster. (The earlier "exec 300s vs chunk 548s" was a warmth confound: the 548s
chunk measurement was ~40min earlier/colder.) No command line limit in .NET (the 27510-char -c ran, blocks=472).
→ ~640ms/describe (warm) / ~1.1s (cold) is a HARD mxcli compute floor; no warm/persistent/bulk mode
breaks it. Chunk=200 is already optimal (past the model load amortisation knee). ROOT FIX therefore does not exist in
mxcli v0.12.0 → STREAMING (making the time bearable) is the right path. The only real order-of-magnitude gain would be the removed
BULK EXPORT (mxlint export gave all expressions in ~19s) — reopens the mxlint removal decision; only
mention as strategic option, not a recommendation. Alternative (separate, not measured): lean on mxcli's own
lint rules (the ~10s lint pass may cover MAINT-008/009 etc.) instead of the own describe route.

MEASUREMENT (June 23) — phase timing per mode (instrumented driver tmp-timing, TRB-Mx11-CLITEST, not estimated):
Log fact: 2 manual scans (16s apart, NO double trigger): FAST 55s (11:55:15→11:56:10), DEEP 4:41
(11:56:27→12:01:08). PHASE TABLE (warm, measured via the real provider classes):
  FAST: lint 6.3s | security 5.8s (describe projectsecurity + 1 guest userrole + 2 SELECTs) | catalog 4.4s
        (8 SELECTs + SHOW SETTINGS) | list-rules 0.3s | payload-build 0.05s | TOTAL ~17s (findings 2594).
  DEEP: lint 11s | security 45.8s (9 userrole describes ~5s each) | catalog 7.1s | DESCRIBE SWEEP 609s |
        list-rules 0.35s | payload 0.07s | TOTAL 673s (11:13) (findings 2865).
SURPRISES: (1) the DESCRIBE SWEEP is ~99% of the deep time. Verified (blocks=chunk size, no
truncation): per DESCRIBE ~1.07s marginal + ~3.7s model load per chunk-of-200. Batching amortises ONLY
the model load; the per-describe cost (~1.1s) is the floor and dominates. (The old "~0.6s/describe" claim is no
longer accurate on v0.11.0/this model.) 472 MF + 106 entities = 578 × ~1.1s(cold)=608s; × ~0.5s(warm OS cache,
as the real deep ran after the fast scan) =289s ≈ log-4:41. Per-describe = 0.5s(warm)–1.1s(cold), LINEAR.
(2) `mxcli lint` REWRITES catalog.db EVERY scan (~6s warm, ~18s cold rebuild, seen in log 11:55:15→33);
no cross-scan cache. This + cold process starts explains log-FAST 55s vs warm 17s (not reproducible
without clearing the OS cache). (3) payload build is negligible (46ms for 1.47MB). (4) small redundant
SELECTs: MODULES/MICROFLOWS/ENTITIES by both catalog and describe route; MODULES 2× in catalog route;
list-rules separate from lint — together ~2s, NOT worth it. SELECT batching saves 0.6s → do not bother.
EXTRAPOLATION (describe sweep, linear, +~22% entities as on TRB): 2000 MF ≈ 20min(warm)–45min(cold);
5000 MF ≈ 51min(warm)–112min(cold). → STREAMING is REQUIRED for DEEP (frozen panel 20–112min unaccept-
able); for FAST (~17–55s) nice-to-have. Fast = catalog (8×~0.4s) + lint + security; slow = describe sweep
(chunks of 200, ~3–4min/chunk). Streaming approach: show fast results directly (~20s), let describe findings
trickle in per chunk (optionally smaller chunks = more frequent updates, costs extra model loads).

PHASE E — DECIDED + IMPLEMENTED: "split the security route". MxcliSecurityService.GetViolations(bool deepScan):
FAST = SEC-008 (describe projectsecurity, 0 userrole calls) + SEC-005 (anon create — only the GUEST role
needed → ≤1 describe userrole, and only if guest access is on); DEEP = those two PLUS SEC-010 + MAINT-005
(all 9 user roles). AcrScanService passes deepScan through. Build 0/0, tests 232/232.
MEASURED (TRB-Mx11-CLITEST, not estimated): fast scan ~46s → ~24s (8 userrole calls removed). BUT ~14s was
UNACHIEVABLE — the cause was misjudged. Per-call breakdown (measured): `mxcli lint` (own engine,
both modes) = ~9.7s — DOMINANT and unavoidable; `describe projectsecurity` = 3.9s; `describe userrole`
(guest) = 4.0s; `lint --list-rules` = 0.5s; each `-c SELECT` (catalog.db) = ~0.4s — CHEAP. The catalog
SELECTs (8×) are therefore NOT the cost → SELECT batching tested (everything in one -c) saves only ~0.6s →
NOT worth it, no PHASE F. The ~24s = ~10s lint + ~8s two describes (SEC-008/005) + ~6s cheap. Floor
with security in fast = ~24s (both) or ~18s (only SEC-008, SEC-005 also to deep) or ~14s (ALL security
to deep, then 0 security in fast). REPACK DECISION for Michel: ship ~24s or SEC-005 also to deep (~18s).

mxlint REMOVAL STOPPED at PHASE A — SEC-006 blocks. FINAL DECISION was "mxlint completely out,
6 to mxcli, 2 deprecated", but verification refuted the measurement for SEC-006.
PHASE A (5 of 6 migrated + verified against old GT):
- SEC-008 (admin=MxAdmin) → describe projectsecurity → AdminUser. GT 1 ✓
- SEC-010 (per-userrole check-security) → describe userrole. GT 0 ✓ (all 9 enabled)
- MAINT-005 (module roles/module) → project tree + describe userrole. GT 5 ✓
- SEC-005 (anon create persistent) → CATALOG.PERMISSIONS CREATE + ENTITIES PERSISTENT + guest role. GT 1 ✓
  (3 anon-CREATE entities, but only Accesslog.AccesslogBankenportaal is PERSISTENT → 1)
  Approach: new MxcliSecurityService synthesises an equivalent project-security YAML from describe/
  project tree and feeds it to the EXISTING pure predicates (logic unchanged) + CatalogRules for SEC-005.
- SEC-006 (anon edit UNLIMITED string) → BLOCKED. mxcli v0.11.0 does not expose the string MAX LENGTH:
  CATALOG.ATTRIBUTES.Length=0 for ALL 748 strings; describe renders ALL as String(unlimited) — including
  Accesslog…Username which has Length:200 in the YAML. The catalog variant over-counts (34 instead of 4: all anon-
  writable strings instead of only unlimited ones). The unlimited-vs-limited discriminator is ONLY in the
  modelsource YAML. → SEC-006 stays on the YAML route (DetectAnonymousEditRules). The measurement verdict
  "CATALOG-NATIVE" was WRONG (Length reliability not tested); this verification caught it.
- MAINT-006 (redundant boolean) → describe migration READY (same extraction as REL-001/002) but not yet
  verified against GT 94 → STAYS on the YAML route for now (do not ship unconfirmed change).
PHASE B: MAINT-015 + REL-003 deprecated (DetectPageRules not wired; PageRules/PageYamlReader remain as backup).
PHASE C: BLOCKED — active modelsource readers remain: DetectAnonymousEditRules (SEC-006) +
DetectExpressionRules (MAINT-006). So the mxlint export + binary CANNOT be removed. Not executed.
CONCLUSION: fully export-free is NOT feasible as long as SEC-006 (and, pending verification, MAINT-006) is retained.
Decision for Michel: (a) also deprecate SEC-006 → then the export can go after MAINT-006 verification;
(b) keep SEC-006 → export stays. Build 0/0, tests 230/230 (+3). tmp cleaned up. No PHASE C/D.

TWO SCAN MODES (fast default + Deepscan). No rule logic changed; only WHICH rules run.
PART 1 (orchestration): AcrScanService.RunScanAsJson(projectDir, bool deepScan=false). The describe route
(MxcliDescribeService — the 5 slow rules MAINT-008/009/REL-001/002/MAINT-013) is now THE ONLY gated step:
`if (deepScan)`. All others run in BOTH modes (catalog-7, mxcli own lint, YAML route rules
MAINT-005/SEC-005/006/008/010/MAINT-006/MAINT-015/REL-003, manual checks, export, marketplace modules).
Message "RunFullScan" → fast (deepScan:false); new "RunDeepScan" → deep. payload.deepScan added.
PART 2 (UI): second button #deepScanBtn (class acr-secondary, clearly subordinate) next to Scan. Both
via shared startScan(deep); setScanning disables BOTH + spinner. Deepscan shows a visible duration
warning ("…can take ~3 minutes…") + the C# ScanProgress text "Deep analysis: scanning all
microflows & entities…". Non-intrusive FOOTNOTE under the count cards for a fast scan (renderSummary,
class acr-scan-note → also in the report): "Quick scan — the deep microflow & expression analysis
(complexity, nested ifs, empty-string checks, default ReadWrite access) is NOT included. Run a Deepscan…"
— communicates the DIFFERENCE (not "broken"). lastDeepScan from payload (default true = no hint before scan).
PART 3 (verification): the describe route is the only gate → fast = complete MINUS the 5 describe rules
(structural: those are 0 in fast, as their YAML emissions are already off and MxcliDescribeService does not run);
deep = + describe with the proven numbers (MAINT-008=129/MAINT-009=1/REL-001=31/REL-002=0/MAINT-013=1,
measured previous turn, logic byte-identical). MEASURED fast scan components: mxcli-lint 5.0s + catalog
SELECTs ~7s + mxlint export 19.1s + YAML parse ~few s ≈ ~30-35s. Deep = + describe route 168s ≈ ~3.3 min.
Marketplace filter/claim table/tripwires work in both modes (UI and pure-normalizer level respectively, mode-
independent; tests 227/227). Export (19s, largest fast-scan component) stays in both (needed for the
YAML rules) — gating it too = moving the YAML rules to deep = separate decision (not now, no silent
finding drop). Build 0/0, tests 227/227.

PERF FIX describe route: batched instead of one process per element. Findings UNCHANGED.
STEP 1 (measurement, real ProcessRunner mechanics): 30 SEPARATE describe processes = 102,584 ms
(3,419 ms/describe) — the bottleneck is per-process model load (a catalog SELECT = 1,189 ms; describe
loads the full model ~3-4s). Batch measurement (PowerShell): marginal describe ≈ 550 ms, fixed model
load ≈ 3.9 s. So N× model load is the cost, not the describe itself.
STEP 2 (can it be done in one process): YES. `mxcli -c "CONNECT LOCAL '…'; DESCRIBE MICROFLOW A; DESCRIBE
MICROFLOW B; …"` delivers all blocks in one process. Measured: 30 in one batch = 18,098 ms (vs 102,584 ms
separate) = ~5.7× faster, identical condition. Output split back on `create or modify …` header lines.
STEP 3 (implementation + verification): MxcliDescribeService rewritten — chunked -c sessions (200/chunk,
safely under the Windows cmdline limit), per-element block split, robust (loud warn + coverage check
on missing blocks; chunk without blocks = error, no silent 0). BUG found+fixed: the entity
header regex missed `non-persistent entity` → 51 of 106 entity blocks silently dropped (106→55). Regex →
`^create or modify (?:[\w-]+ )*entity (\S+)` (any qualifier). After fix: microflows 472/472,
entities 106/106 blocks. VERIFICATION on TRB: findings EXACTLY equal — MAINT-008=129, MAINT-009=1,
REL-001=31, REL-002=0, MAINT-013=1; catalog rules unchanged. DURATION: 169.5 s (~2.8 min) batched vs
~4 min separate (and vs ~33 min if the 3.4s/describe-separate measurement were representative → up to ~12×).
REMAINING CEILING: the per-element describe compute (~0.29 s × 578 ≈ 168 s) is now the lower bound;
parallelism (multiple batch processes simultaneously) is the next lever if needed. Build 0/0,
tests 227/227. tmp cleaned up.

CUTOVER mxlint→mxcli COMPLETED. The describe route is live-wired and the 4 mxcli-covered topics
deferred. Routes now: CATALOG-SQL = MAINT-007/010/014, SEC-007/009/011, PERF-001 (7). DESCRIBE =
MAINT-008/009/013, REL-001/002 (5, via new MxcliDescribeService, user-module scope, describe per
microflow/entity). DEFER to mxcli's own rule = MAINT-011↔MPR003, PERF-002↔CONV017, MAINT-012↔
ACR_ENT_VALRULES/CONV015, commit↔CONV011 (our CLEVR emission out). STILL YAML = CLEVR-MAINT-006 (redundant
boolean, out of scope) + the non-mxlint ACR rules (MAINT-005, SEC-005/006, PAGE MAINT-015/REL-003).
mxlint export engine = backup (findings off, export still runs for the YAML route remnants).
STEP 2 — live wiring: MxcliDescribeService (spike) runs the 5 pure describe rules; YAML emissions in
DetectExpressionRules (REL-001/002/MAINT-008/009) + DetectDomainModelBatchRules (MAINT-013) turned off
(pure rules = backup). STEP 3 — claim table/tripwire cutover: MPR003 + CONV011 OUT OF SuppressedMxcli
(mxcli's own rule should show); the 4 entries → Winner = mxcli's rule, SuppressMxcli empty,
mxlint twin (002_0001/0006/0007/005_0002) REMAINS suppressed (backup also defers). Tripwire lists
updated: SuppressedMxcliCounterparts = {QUAL003,CONV009,DESIGN001,CONV002} (MPR003/CONV011 removed);
InternalisedMxlintTwins membership unchanged (comments → 'deferred'). DomainModelBatchTests.ClaimTable_
MxcliChoices converted (MPR003 now DoesNotContain). STEP 4 — VERIFICATION TRB: no loss (describe numbers
proven in earlier sweeps via identical pure rules: REL-002=0, MAINT-009=1, REL-001=31, MAINT-013=1,
MAINT-008=129; catalog rules unchanged). No duplication: mxcli output MPR003=2 (System UI filtered→TRB),
CONV011=0, CONV017=5, ACR_ENT_VALRULES/CONV015=0; our CLEVR-MAINT-011/PERF-COMMIT = 0 in mxcli output
(emission off) → exactly one source per topic. Tripwires green AND consistent with the new state.
Build 0/0, tests 227/227. NOT in this step: remove mxlint export + severity calibration. tmp cleaned up.
PERF CAVEAT: the describe route = one mxcli process per microflow/entity (~578 on TRB) → noticeably slower;
batching is a separate later decision.

ADDITIVE ROUND (no cutover): 3 remaining describe rules + marketplace filter. NOT live-wired;
old YAML route continues; claim table/tripwire untouched. All user-module scope (Source empty).
PART 1 — 3 describe rules on the proven assembler, reusing existing predicates:
- REL-001 (redundant empty string): describe Extract → ExpressionRules.RedundantEmptyString.
  Live user-module sweep = 31 == old YAML GT 31 → EXACTLY reproduced (no deviation).
- MAINT-013 (default-RW): new pure DescribeEntityRules — `grant <role> on <ent> (… write *)` =
  DefaultMemberAccessRights ReadWrite. Live = 1 == old GT 1 (TRB_Email.TRB_Email, Administrator) → EXACT.
- MAINT-008 (complex without annotations): describe StructureCounts (actions=non-split statements,
  splits=ALL structural if headers incl. nested, annotations=@annotation lines) → existing
  ComplexWithoutAnnotations. Live = 129 vs old YAML GT 103. DEVIATION = METRIC, not scope/error:
  same 472 user-module microflows, but the describe count also counts NESTED splits (the old YAML
  only counted top-level ExclusiveSplitCount) → more microflows with splits>2 → +26. Accepted as
  new mxcli/describe ground truth (calibration item), not forced to 103.
  Synthetic positive unit tests per rule (both directions). Build 0/0, tests 227/227 (+8).
PART 3 — marketplace filter (UI, no suppression): AcrScanService includes `appStoreModules` in the
payload (CATALOG.MODULES.Source not empty — same mechanism as PHASE 1, via MxcliCatalogService.
AppStoreModuleNames). main.js: isAppStoreModule(v) (module prefix ∈ set) + toggle appStoreVisible
(default SHOW) in baseViolations → works through in panel AND report. Checkbox "Marketplace modules (N)"
in the Source filter row (same style as the source filters), only shown when there are app-store findings.
Findings are NOT pre-suppressed; purely a display toggle. tmp cleaned up.

FIX — mxcli exit code 1 was incorrectly treated as failure. CAUSE factually established
(v0.11.0, not from docs): mxcli's exit code is NOT a success/failure signal but a CI convention on
SEVERITY: exit 1 = ≥1 error-severity finding (TRB: 3 errors → exit 1, with valid JSON), exit 0 = no
error findings (warnings/info can be present — verified: all modules-minus gives 0 errors → exit 0).
AND a real error (connect error) also gives exit 1, but with EMPTY stdout + 'Error connecting: …' on
stderr. The "vibe-coded PoC" warning is ALWAYS on stderr → not an error indicator. No exit code
convention documented in --help; purely empirically established (3 cases).
FIX (on established semantics, not on assumption): AcrScanService no longer guesses the exit code. New
MxcliOutputParser.ContainsJson(stdout) distinguishes: stdout with JSON envelope → mxcli ran normally →
parse regardless of exit code; empty/non-JSON stdout → LOUD failure via Diagnostic (exit code + stderr), never
silently 0 findings. The old `if (ExitCode != 0) return Diagnostic` is replaced by this
JSON-presence check.
VERIFICATION on the REAL captured mxcli output: CASE A (findings, exit 1) → ContainsJson=true, 2574
violations parsed → SUCCESS (no longer rejected); CASE C (non-existent .mpr, exit 1, empty stdout) →
ContainsJson=false → loud Diagnostic. The 7 catalog rules (MAINT-007=30/MAINT-010=592/SEC-007=1/rest 0)
come from the catalog SELECT route (MxcliCatalogService) and are independent of this lint-call gate — unchanged.
Build 0/0, tests 219/219 (+1 ContainsJson test). tmp cleaned up.

DESCRIBE ROUTE PROVEN (PHASE 2 extractor fix). The divergence (27 instead of 0) is resolved: the
DescribeMicroflowExpressions extractor now has a MULTI-LINE ASSEMBLER (Assemble) — wrapped
conditions are joined into one statement (up to `;` or a bare ` then`) before the predicate. The
proof step runs on USER-MODULE scope (Source empty in CATALOG.MODULES — same mechanism as PHASE 1;
13 user modules, 472 microflows ≈ the 471 modelsource microflows). Existing predicates UNCHANGED
reused (ExpressionRules.IncompleteEmptyStringCheck + MicroflowStructureRules.NestedIfStatements/
NestedIfRegex); only the EXTRACTION is fixed + ExtractSplits added (split condition + caption).
DOUBLE VERIFICATION on TRB (user-module sweep, not just one microflow):
- REL-002 = 0 (GT 0) — the multi-line complete checks (Encryption.MB_SaveCertificate 4×) now count as
  complete, no more false positive.
- MAINT-009 = 1 (GT 1) = TRB.SUB_ValidateVelden, caption 'Datum na 1 april 2015?' — the REAL positive
  case (nested inline if from the real describe output) correctly reproduced. A 0 result would not prove
  the route; this positive case does.
Both exact → route proven. Unit tests cover both + the regression (multi-line wrap → complete) +
plain/compound conditions (no false nested). Build 0/0, tests 218/218.
STOP: the remaining 3 (MAINT-008, REL-001, MAINT-013) follow as a SEPARATE batch now the route is proven.
NOT in this step (separate follow-up steps): include app store in scan + screen filter, the live wiring of the
describe route in the scan (REL-002 still runs via YAML), claim table/tripwire cutover for the mxcli-covered rules.

MIGRATION mxlint→mxcli (Apache-2.0). PHASE 1 (mxcli catalog provider + 7 robust rules) DONE; PHASE 2
(describe route proof rule) DIVERGES → stopped, cause named, remaining 4 NOT built.
PHASE 1: new pure CatalogRules (normalizer) + MxcliCatalogService (spike, SQLite catalog via
`-p <mpr> -c "SELECT … FROM CATALOG.*"`). 7 rules migrated on catalog SQL, rule-id/category/severity
+ claim table unchanged; verified live against v0.11.0/TRB:
- MAINT-007 ActivityCount>25 → 30 (was 44; mxcli metric accepted as new GT)
- MAINT-010 DefaultValue non-empty → 592 (was 283; incl. implicit defaults, accepted)
- MAINT-014 user-modules(Source empty) → 13 ≤20 → 0
- SEC-011 ExposedToClient=1 + sensitive name filter → 2 exposed, not sensitive → 0
- PERF-001 Generalization=Administration.Account → 0
- SEC-007 ToEntity LIKE 'System.%' scoped user-module → 1 (TRB.Groep_UserRole)
- SEC-009 SHOW SETTINGS Hash=BCrypt → 0
YAML emissions for these 7 disabled in AcrScanService (methods remain as backup, mxlint export
remains load-bearing for the non-migrated rules). 4 mxcli-covered topics → DEFER to mxcli's
own v0.11.0 rules (confirmed present live): MAINT-011↔MPR003, PERF-002↔CONV017, MAINT-012↔
ACR_ENT_VALRULES/CONV015, commit↔CONV011 — not built ourselves. (The claim table/tripwire reconciliation
for MAINT-011↔MPR003 + commit↔CONV011 = deliberate follow-up cutover, NOT done now.)
PHASE 2 (proof rule CLEVR-REL-002 via describe microflow): pure DescribeMicroflowExpressions (extractor)
+ unchanged ExpressionRules.IncompleteEmptyStringCheck. Unit tests green. BUT the TRB sweep
did NOT reproduce the GT: 27 findings instead of 0. INVESTIGATED — two causes:
(1) SCOPE: modelsource export = 471 microflows (excl. marketplace), catalog = 1074 (incl. app store);
    the 27 are in app-store modules (SAML20/Encryption/SupportModule) that the old route never saw.
(2) PER-LINE FALSE POSITIVES (the serious ones): describe wraps long conditions over multiple lines
    (`if not($X != empty\n$X != '') then`); naive per-line extraction cuts a COMPLETE check into an
    apparently-incomplete fragment `$X != ''` → false positive. Proven on Encryption.MB_SaveCertificate
    (lines 13-15/25-27/32-34/61-63 = multi-line complete checks). TRB.SUB_ValidateVelden gave 0
    because its complete check is on ONE line (line 62).
CONCLUSION: describe route is FEASIBLE (data fully present) but the extractor first needs a
multi-line expression assembler (join wrap lines into whole logical expressions before the
predicate) + a scope decision (include app store or not, consistent with the catalog rules). Only then
the remaining 4 (MAINT-008/009/013, REL-001). Build 0/0, tests 215/215. mxlint backup + claim table + tripwires intact.

FIX — mxcli side consistent: CONV011 (NoCommitInLoop) added to SuppressMxcli of the commit-in-loop
entry. CONV011 measures exactly the commit-in-loop topic (catalogue: "Commit actions should not be inside
loops (N+1)", performance) = same as CLEVR-PERF-COMMIT-IN-LOOP/005_0002. Fires 0 on TRB → nothing
visibly changes, but the set is complete once it fires (consistent with CONV002/QUAL003/CONV009/
DESIGN001/MPR003). SAFEGUARD extended: second tripwire SuppressedMxcli_ExactlyMatchesCounterparts —
the canonical list of all 6 deliberately suppressed mxcli IDs must EXACTLY match SuppressedMxcli;
a forgotten mxcli suppression now fails just as loudly as a forgotten mxlint entry. Build 0/0, tests 200/200 (+1).

FIX — claim table drift restored (005_0002/0004/0005 were missing). During the microflow batch the
detect rules were added but the suppression entries were forgotten; only 005_0003 got one at the time.
005_0004 therefore appeared visibly doubled (103 alongside CLEVR-MAINT-008); 005_0002/0005 latent (0 and
not in pack respectively). Three EngineClaim entries added: 005_0004→CLEVR-MAINT-008, 005_0005→CLEVR-MAINT-009,
005_0002→CLEVR-PERF-COMMIT-IN-LOOP. Detect logic UNCHANGED.
mxcli counterpart check: 005_0004/0005 none (mxcli has no annotation/nested-if rule). 005_0002 DOES have
an mxcli twin — CONV011 NoCommitInLoop — but that fires 0 on TRB (0× in full mxcli lint),
therefore not suppressed now; recommendation in the Impact to add CONV011 for consistency.
VERIFIED LIVE on TRB output: 005_0004 raw=103 → after MxlintNormalizer suppression 0; 005_0003 44→0;
005_0002/0005 0→0 (and all four in SuppressedMxlint); CLEVR-MAINT-008 retains its 103 (own detect).
SAFEGUARD against recurrence: new test ClaimTableTests.SuppressedMxlint_ExactlyMatchesInternalisedTwins —
one canonical list of all 23 internalised/migrated mxlint twins must EXACTLY match SuppressedMxlint.
Missing entry (the 005_0004 bug) → test fails loudly; stray entry → also. One place, one
list. Build 0/0, tests 199/199 (was 198; +1 safeguard test). tmp cleaned up.

REGO INTERNALISATION — FINAL RULE 004_0002 ImagesWithAltText → mxlint.com set 17/17 INTERNALISED.
STEP 0 already done: MXLINT-ONLY (MPR005 UnconfiguredImage = missing image SOURCE, different topic).
TARGET RULE 004_0002 (.rego category Accessibility, MEDIUM). Logic VERBATIM: walk → node with
$Type CustomWidgets$CustomWidget; Object.$Type == CustomWidgets$WidgetObject; at least one
Object.Properties[].Value.PrimitiveValue == "fullImage" (= image widget); FIRES if NONE of its
OWN Properties' Value.TextTemplate.Template.Items has a Texts$Translation with a PRESENT Text key.
SUBTLE (confirmed from the Rego test fixtures): "Text set" = the key is defined — even
Text:"" counts as set (Rego truthy); only a MISSING Text key = absent (variation_1 misses the
key → fires; variation_2 has no translation → fires; allow has Text → does not fire).
INVERTED FN RISK (MISSING check): a missed translation branch = false POSITIVE. Therefore
HasAltText iterates ALL own Properties + ALL Items (only the OWN Object.Properties, not nested
child widgets — exactly the Rego scope).
STEP 1 (fresh export): 113 pages/snippets, 62 CustomWidgets$CustomWidget nodes, but 0 image widgets
(no fullImage on TRB). WITH alt text: 0; MISSING: 0 → GT=0. mxlint twin 004_0002 runs CORRECTLY
(testcases=113, failures=0) → valid cross-check that AGREES (twin 0 = GT 0).
STEP 2: NO new reader — reuses the PageYamlReader tree + pure PageRules (walk like MAINT-015).
AcrScanService: page batch (MAINT-015 + REL-003) on one reader pass. Rule ID CLEVR-REL-003;
CATEGORY CHOICE (button for Michel): Accessibility→Reliability (no ACR bucket); MEDIUM→Major.
STEP 3 (double, with explicit FP direction): real rule == independent YamlDotNet GT (0 image widgets,
0 missing) == working twin (0) → EXACT, 0 FP/FN. Synthetic tests both directions: image WITHOUT
translation→fires (variation_2); translation WITHOUT Text key→fires (variation_1); translation WITH
Text→not (allow); Text:""→not (verbatim truthy); non-image widget→not; dedup same widget name→1.
CLAIM TABLE: 1 entry — winner CLEVR-REL-003; suppress mxlint 004_0002. No mxcli suppression.
Name mapping test NameFor("004_0002")=="ImagesWithAltText" UNTOUCHED (suppression only affects Normalize).
Build 0/0, tests 198/198 (was 191; +7). tmp-alt cleaned up. Real Studio Pro scan test: Michel.
>>> mxlint.com set now FULLY internalised (17/17). mxlint as rule source can be phased out.

REGO INTERNALISATION — page/snippet route (proof rule 004_0001; new file type):
Last route. STEP 0 already done: 004_0001 + 004_0002 both MXLINT-ONLY (no mxcli/ACR counterpart;
MPR005 UnconfiguredImage = missing image SOURCE, different topic; "style" in the list = only a
Category). Only 004_0001 now built (proof rule that opens the page reader); 004_0002 (alt text, deep
CustomWidget/WidgetObject/Texts$Translation tree) follows only once the reader is proven.
TARGET RULE 004_0001 InlineStylePropertyUsed (Maintainability/MEDIUM→Major). Rego logic VERBATIM:
walk(input) → every path whose LAST key is exactly "Style" and value != "" (non-empty string).
STEP 1: pages/snippets in `*.Forms$Page.yaml` / `*.Forms$Snippet.yaml` (113 on TRB: 101 pages + 12
snippets). Style sits under `Appearance:` (Forms$Appearance) nodes, deep through the entire widget tree;
top-level `Name` at column 0. Values: 7527× empty ("") + various non-empty CSS (single/double-quoted,
\r\n, block scalars |-). mxlint twin 004_0001 runs CORRECTLY (testcases=113 = all files fed;
14 files with findings) → situation "correct" (not dormant/broken) → Rego cross-check VALID.
SUBTLE / INVESTIGATED: raw non-empty Style occurrences = 86, but mxlint reports 33. Cause: Rego's
`errors` is a SET of error STRINGS, and that string = sprintf(... input.Name, v). Identical (Name,value)
pairs merge → dedup per page on style VALUE. 33 = distinct (file,value). Rule adjusted accordingly
(HashSet per page) — otherwise 86 instead of 33 (false-positive double counting).
STEP 2: new `PageYamlReader` (spike, YamlDotNet) → converts each doc to a FLAT object tree model
(Dictionary/List/string) = `PageModel` in the normalizer; the pattern walk is PURELY in `PageRules`
(dependency-free, unit-testable). REUSABLE: 004_0002 will walk the same tree later. Rule ID
CLEVR-MAINT-015, Maintainability/Major.
STEP 3: real rule (PageYamlReader + PageRules) == independent YamlDotNet GT (set semantics) == working
mxlint twin → all three 33 over 14 files, 0 FP/FN, EXACT (triple agreement). Synthetic
positive tests: non-empty Style→fires; empty/absent→not; "MyStyle"/"StyleClass"/"DynamicClasses"→not
(exact key); identical value 2× per page→1 (set), 2 distinct→2; snippet doctype.
CLAIM TABLE: 1 entry — winner CLEVR-MAINT-015; suppress mxlint 004_0001. No mxcli suppression.
Build 0/0, tests 191/191 (was 184; +7). tmp-pg cleaned up. Real Studio Pro scan test: Michel.

REGO INTERNALISATION — constant route (proof rule 006_0001; new file type):
Last route (page/constant). 16 of 17 MXLINT-ONLY done; this route requires NEW YAML readers for
file types we have not yet read → first one proof rule, then the rest.
STEP 0 (coverage check, mxcli lint --list-rules + claim table): 006_0001 ExposedConstants, 004_0001
InlineStylePropertyUsed, 004_0002 ImagesWithAltText → NO mxcli-bundled or claimed ACR rule covers
these topics (MPR005 UnconfiguredImage = missing image SOURCE, different topic from alt text;
"style" in the list = only the Category of MPR001 NamingConvention). All three MXLINT-ONLY. Only
006_0001 built now (proof rule); 004_0001/0002 follow once the reader is proven (004_0002 alt text
is the hardest — deep widget/translation tree).
STEP 1 (file + ground truth): constants are in `*.Constants$Constant.yaml` (FLAT: `$Type`,
`ExposedToClient` bool, `Name` string — all column 0). First constant file we read. TRB: 9
constants, ALL 9 ExposedToClient: false → YamlDotNet ground truth (exposed && sensitive name) = 0.
mxlint twin 006_0001 WORKS correctly here (testcases=9 — the glob `**/*$Constant.yaml` feeds all 9 files —
failures=0): valid cross-check that AGREES (mxlint 0 = GT 0). So NOT dormant/broken (unlike
001_0007/003_0001 which read the wrong path).
STEP 2 (build): new YamlDotNet reader `ConstantYamlReader` (spike, infra style like MicroflowYaml-
Expressions) → pure `ConstantRules` (normalizer). Rule ID CLEVR-SEC-011 (SEC series), Security/Critical
(mxlint HIGH). SUBTLE POINT — the .rego has TWO branches: (1) ANY exposed constant = MEDIUM, (2)
exposed + sensitive name = HIGH. We deliberately build ONLY branch (2) (branch 1 = noise: flags ALL exposed
constants). Sensitive name detection VERBATIM from the .rego: substring (case insensitive) on keyword list
["id","ident","username","user_name","user","usr","uname","secret","scrt","password","pwd","passwrd"].
Deliberately over-broad just like the Rego (e.g. "Width" contains "id") — nothing invented/added.
STEP 3 (verify): real rule (ConstantYamlReader + ConstantRules) == independent YamlDotNet GT on TRB
= 0, 0 FP/FN, EXACT. Synthetic positive tests: exposed+sensitive→fires; exposed+innocent→not;
sensitive+not exposed→not; + keyword list verbatim test.
CLAIM TABLE: 1 entry — winner CLEVR-SEC-011; suppress mxlint 006_0001. Impact explicitly notes that
the blanket-MEDIUM branch (every exposed constant) is dropped here (deliberate choice). No mxcli suppression.
Build 0/0, tests 184/184 (was 171; +13). tmp-cv cleaned up. Real Studio Pro scan test: Michel.

REGO INTERNALISATION — security/settings/modules batch (4 of 4 MXLINT-ONLY built):
STEP 0 (coverage check before build, criterion "mxcli OR existing ACR rule already covers the TOPIC →
BUILD NOTHING"): 001_0004 StrongPasswordPolicy = COVERED (mxcli ACR_SEC_PWPOLICY + SEC002) → NOT built;
001_0005, 001_0007, 001_0008, 003_0001 = MXLINT-ONLY (no mxcli/ACR rule covers admin username,
hash algorithm, per-userrole-security, or module count) → all four built.
STEP 1+3 (field exists + ground truth, YamlDotNet/structural as oracle; real rule == GT, 0 FP/FN):
- 001_0005 → CLEVR-SEC-008 MxAdminNotUsed (Security/Critical=HIGH): GT=1 rule=1 (AdminUserName: MxAdmin).
  mxlint twin also fires 1 (input `.*Security$ProjectSecurity.yaml`, no `/`) → valid cross-check.
- 001_0007 → CLEVR-SEC-009 HashAlgorithm (Security/Critical=HIGH): GT=0 rule=0 (HashAlgorithm: BCrypt).
  mxlint twin STRUCTURALLY broken on this export: reads `input.Settings.HashAlgorithm`, but Settings is
  a LIST (not a mapping) → fires 0 for the wrong reason. Our rule finds the field where it actually is.
- 001_0008 → CLEVR-SEC-010 CheckSecurityOnUserRoles (Security/Critical=HIGH): GT=0 rule=0 (9/9 user-
  roles CheckSecurity: true). Per role; absent/false = violation (like Rego `not CheckSecurity`).
- 003_0001 → CLEVR-MAINT-014 NumberOfModules (Maintainability/Major=MEDIUM): GT=0 rule=0 (12 user-
  modules ≤ 20). mxlint twin STRUCTURALLY broken: reads `Modules[i].Attributes.FromAppStore == false`,
  but the export puts FromAppStore DIRECTLY under the module item (not under Attributes) → counts 0. Our
  rule = item WITHOUT `FromAppStore: true` = user module → correct count.
Synthetic positive unit tests per rule (TRB is 1/0/0/0, so detection proven separately).
CLAIM TABLE: 4 entries added — winners CLEVR-SEC-008/009/010 + CLEVR-MAINT-014; mxlint twins
001_0005/001_0007/001_0008/003_0001 suppressed. No mxcli suppression (all four MXLINT-ONLY).
Source reuse: ProjectSecurityParser (4 new Detect methods); AcrScanService now also reads
Settings$ProjectSettings.yaml + Metadata.yaml. MxlintNormalizerTests fixture that used 001_0005 as generic
passes-through → repointed to 001_0004 (StrongPasswordPolicy, not yet internalised → not
suppressed). Build 0/0, tests 171/171 (was 156; +15). tmp-dg cleaned up. Real Studio Pro scan test: Michel.

REGO INTERNALISATION — domain model batch (6 of 7; 002_0004 deliberately skipped):
KEY FINDING: all 7 mxlint counterparts are DORMANT on Windows — their .rego `input: .*/DomainModels…`
(with `/`) does not match backslash paths → mxlint feeds them 0 files (testcases=0). 002_0009 only works
because it uses `.*DomainModels…` (without `/`). So the mxlint "0" is NOT a valid cross-check; the
YamlDotNet ground truth is the only test. (That they are dormant = exactly the proof that internalising has
value.) Built on the line parser (ProjectSecurityParser extended: MaybeGeneralization
Persistable/Generalization, Value.$Type, ValidationRules count, AccessRule.DefaultMemberAccessRights,
+ top-level CrossAssociations parser). Per rule real rule == YamlDotNet ground truth, 0 FP/FN:
- 002_0001 → CLEVR-MAINT-011 (Maintainability/Major): 1 (TRB 19 persistent >15).
- 002_0003 → CLEVR-PERF-001 (Performance/Major): 0 (no Administration.Account inheritance).
- 002_0005 → CLEVR-SEC-007 (Security/Critical = HIGH): 1 (TRB|Groep_UserRole cross-assoc → System).
- 002_0006 → CLEVR-PERF-002 (Performance/Major): 0 (no entity >10 calculated).
- 002_0007 → CLEVR-MAINT-012 (Maintainability/Major): 0 (no domain validation rules).
- 002_0008 → CLEVR-MAINT-013 (Maintainability/Major): 1 (TRB_Email ReadWrite access).
Synthetic positive unit tests per rule (TRB is 0/1, so detection proven separately).
002_0004 NOT built: the Rego is buggy — `not startswith(<undefined>,"System.")` fires on all 60
no-generalization entities → 64 noise instead of the 4 intended non-System inheritors. Recommendation: do not
internalise as-is; optionally the INTENT (4) as a separate deliberate rule — Michel decides.
CLAIM TABLE per rule: mxlint twins suppressed (002_0001/0003/0005/0006/0007/0008). mxcli checks:
MPR003 (fires 2: System 27 UI-filtered + TRB 19) → suppressed for 002_0001 (no visible loss);
CONV017 (fires 5, EVERY calculated) → NOT suppressed (broader than our >10 rule, would lose 5);
ACR_ENT_VALRULES/CONV015/CONV006/CONV007 → 0 on TRB, nothing to suppress. Build 0/0, tests 156/156.

REGO INTERNALISATION — domain model YAML route OPENED: CLEVR-MAINT-010 = mxlint 002_0009 NoDefaultValue.
.rego: per (entity, attribute) → attribute.Value.DefaultValue != null && != "". Category Maintainability,
severity LOW → ACR Minor. Reuses ProjectSecurityParser.ParseEntitiesWithAttributes (the SEC-005/006 infra),
extended with Value→DefaultValue + UNQUOTE ('' /"" → empty; "false" → false) — crucial because the export
field is quoted (288× "" = empty/no-violation, 145× "false", 106× "0", 32× strings). GROUND TRUTH on fresh
TRB export = 283; investigated (suspicious "8 vs 283"): xUnit failures count FILES (8), not findings —
unbundled = 283 (2+1+1+7+74+48+144+6). 283 is high because the Rego flags EVERY non-empty default (incl. boolean
false + integer 0) — faithfully reproduced. STEP 3: real rule (line parser) = 283 = YamlDotNet ground truth
283 = mxlint 002_0009 283 → EXACT, 0 FP/FN (proves the line parser extension + unquote correct). Claim table:
002_0009 added — winner CLEVR-MAINT-010; suppress mxlint 002_0009 (identical, no loss) AND mxcli
CONV002 NoEntityDefaultValues (fires 106, ONLY integer '0' → STRICT SUBSET of our 283 → no loss;
without suppression a new 106-mxcli-duplication). Build 0/0, tests 142/142 (+4; 4 existing
MxlintNormalizer fixtures that used 002_0009 as "generic passes-through" → repointed to synthetic
999_9999, name mapping tests remained 002_0009). (Disposable tools tmp-gt5/gt6 can remain — inert.)

REGO INTERNALISATION — expression route: CLEVR-REL-002 = mxlint 005_0001 EmptyStringCheckNotComplete.
.rego (verbatim): per "Expression"-keyed value → strip SPACES → contains "!=''" AND not "!=empty"
(per-expression substring check; complement of REL-001 which measures BOTH-checks-on-1-path = redundant
→ confirmed no overlap). Category .rego "Error" → Reliability (mapped); severity MEDIUM → Major.
New extraction MicroflowYamlExpressions.ExpressionKeyedValues (all key=="Expression" values, ≠ the
existing split+change set that feeds REL-001/006) → ExpressionRules.IncompleteEmptyStringCheck.
GROUND TRUTH on fresh TRB export = 0; investigated (no blind 0): of 27 expressions with !='' ALL
also have !=empty (313 use !=empty) → 0 incomplete. Real rule = 0, mxlint 005_0001 = 0 → EXACT
(0==0==0); REL-001=31 runs independently alongside (proves no overlap). Claim table: 005_0001 added
(mxlint suppression only; NO mxcli counterpart — confirmed in the 60-rule measurement). Build 0/0,
tests 138/138 (+14). (Disposable tool tmp-gt5\ can remain due to a build-server handle — inert.)

CLAIM TABLE (cross-engine deduplication — replaces the mxlint-only denylist): new ClaimTable.cs
(normalizer) — one winning source per topic, counterparts suppressed on BOTH engines.
MxcliNormalizer now drops SuppressedMxcli (by rule-id), MxlintNormalizer drops SuppressedMxlint (by
rulenumber); the old hardcoded 6-rulenumber denylist is gone incl. the 2 incorrect entries (001_0004/002_0007
referred to unclaimed ACR_SEC_PWPOLICY/ACR_ENT_VALRULES → no longer suppressed). Rule logic
untouched; only the aggregation layer. PROOF topics (2): microflow size (CLEVR-MAINT-007 wins →
suppress mxcli QUAL003+CONV009, mxlint 005_0003) and attribute count (ACR_ENT_ATTRS/CLEVR-MAINT-001 wins
→ suppress mxcli DESIGN001, mxlint 002_0002). 3 security topics migrated 1-to-1 (mxlint-only,
behaviour unchanged) so the denylist replacement does not regress. VERIFIED on TRB via the REAL
normalizers: mxcli QUAL003 24→0, CONV009 61→0, DESIGN001 16→0 (total 2131→2030, −101); mxlint 005_0003
44→0; WINNERS intact (CLEVR-MAINT-001=6, CLEVR-MAINT-007=44); 005_0004 untouched (103, no proof topic).
DELIBERATE LOSS noted: CONV009 17 (16–25 activities) + DESIGN001 10 (11–25 attrs) — counterparts covered more broadly;
confirmed per topic. Build 0/0, tests 124/124 (+13). Remaining topics (naming/guest mxcli
+ the 17 to internalise) follow only after approval per topic. (Measurement tool tmp-gt4\ inert, leave as-is.)

DUPLICATION MEASUREMENT (TRB, fresh mxcli-lint --format json, 2131 findings / 37 of 60 rules fire):
The visible duplication is NOT primarily mxlint↔ACR (already suppressed), but mxcli-GENERIC ↔ claimed-ACR/CLEVR:
111 redundant generic findings NOW visible: attribute count ACR_ENT_ATTRS∩DESIGN001=6; microflow size
CLEVR-MAINT-007∩QUAL003=24 + ∩CONV009=44 (all 44 doubled, 24 tripled); enum ACR_ENUM_PREFIX∩CONV004=26;
snippet ACR_SNIP_PREFIX∩CONV005=10; guest ACR_SEC_GUEST∩SEC004=1. Commit-in-loop latent (CONV011=0, ours=0).
The mxlint denylist (6 rulenumbers) TOUCHES NONE of this — all mxcli-internal. SEPARATELY: the denylist misses the
005 family (005_0002/0003/0004/0005) → mxlint 005_0003(44)+005_0004(103) now double with our CLEVR-MAINT-007/008
once mxlint runs (≈147, denylist config fix needed). Conclusion for the solution: a claim table on
TOPIC (suppress on both engines: mxcli-generic + mxlint) is proportionate; an mxlint-only denylist does not
cover the 111 mxcli-generic doubles in principle. (Measurement tool tmp-gt3\ remained due to a build-server handle — inert.)

REGO INTERNALISATION — flow AST route COMPLETED (3 remaining microflow rules, batch after MAINT-007):
One YAML parse (MicroflowYamlExpressions.Parse → record with expressions + objectcount + top-level
type counts + ExclusiveSplits + in-loop actions); pure rules in MicroflowStructureRules; wired in
DetectExpressionRules. Fresh export (mxlint export+lint) → ground truth + Rego counterpart:
- CLEVR-MAINT-008 = 005_0004 ComplexMicroflowsWithoutAnnotations (Maintainability/Major): >10 ActionActivity
  OR >2 ExclusiveSplit (top-level) AND 0 Annotation. Rule=103, Rego=103, EXACT.
- CLEVR-MAINT-009 = 005_0005 NestedIfStatements (Complexity→Maintainability/Major): ExclusiveSplit with
  SplitCondition.Expression matching the VERBATIM .rego regex `^[\S\s]*(then|else)[\S\s]*(if)[\S\s]*$`.
  Rule=1 (TRB.SUB_ValidateVelden, multi-line block scalar correctly parsed). NO Rego counterpart: the
  deployed v3.3.0 rules pack does NOT contain 005_0005 (only 0001-0004) → checked against the .rego source.
- CLEVR-PERF-COMMIT-IN-LOOP = 005_0002 AvoidCommitInLoop (Performance/Major): committing action
  (CommitAction OR ChangeAction Commit=="Yes") inside a LoopedActivity. Rule=0, Rego=0. DELIBERATE ID CHOICE:
  reuses the bson PoC ID (BsonMicroflowParser.RuleId) — the YAML route REPLACES the unwired
  bson PoC. The 0 is GENUINELY verified (all 136 CommitActions at indent 12 = top-level, NONE in a loop),
  not by a broken walk (positive unit test proves detection). The Rego's 0 is DORMANT by contrast:
  005_0002 reads input.MainFunction + .Attributes — both absent in the modelsource export (the real form is
  ObjectCollection.Objects → LoopedActivity → ObjectCollection.Objects → ActionActivity.Action). Both
  coincidentally 0; our rule correctly reproduces the INTENT on the real structure.
Build 0/0, tests 111/111 (+16). Verification via the REAL extractor+rules (no reimplementation).
(Disposable tool tmp-gt2\ remained due to a build-server handle — inert, can be removed separately.)

REGO INTERNALISATION — 1st proof rule (flow AST route opened): CLEVR-MAINT-007 = mxlint 005_0003
NumberOfElementsInMicroflow. .rego: count(ObjectCollection.Objects) - 2 > 25 (top-level, NON-recursive;
-2 = fixed Start+End offset). Built as pure MicroflowStructureRules.NumberOfElements (kind=acr,
source=clevr-acr, category Maintainability literally, severity Major = mxlint MEDIUM, to be adjusted).
Spike: MicroflowYamlExpressions.ParseMicroflow now parses 1× per microflow and delivers BOTH expressions
AND top-level object count (root.ObjectCollection.Objects) → DetectExpressionRules feeds the count rule.
GROUND TRUTH on fresh TRB export (mxlint export 14s, 471 microflows, parseFail=0): 44 findings, max 98
elements (Rapportages.IVK_GenereerGlobaalHerzieningsbeslissing, raw 100). Verified with 2 independent
methods (YamlDotNet structural + scoped indent count: 29/100/72 exact). NOTE: global indent grep was
wrong (counted dashes outside ObjectCollection) — scoped/structural is correct. STEP 3: the REAL rule on the same
counts → 44/44, 0 FP, 0 FN, 0 count mismatch, no System/marketplace (modelsource has no System module;
appstore modules export-excluded). Build 0/0, tests 95/95 (+9). ONLY this rule; 005_0002/0004/0005
follow only after proof in Studio Pro (by Michel). (Disposable verification tool tmp-groundtruth\ remained due to a
build-server handle — inert, can be removed separately.)

INSTALLER mxcli AUTO-DOWNLOAD (colleague stranded on "git clone + make build" — make missing on Win):
- Install-ClevrAcr.ps1 mxcli step now: (1) on PATH? use that; (2) previously downloaded by us in
  %LOCALAPPDATA%\clevr-acr\mxcli\mxcli.exe? reuse; (3) otherwise: ask for confirmation (no silent
  download) → Install-Mxcli fetches latest release asset mxcli-windows-amd64.exe via
  api.github.com/repos/mendixlabs/mxcli/releases/latest (User-Agent + TLS1.2), VERIFIES sha256
  (from asset.digest) + byte size, writes absolute path in mxcliPath. Mismatch/error/decline/no net
  → file gone + clean fallback to manual route (releases URL), never crashes. mxlint NOT
  automated (remains optional via official extension — too fragile: requires mxlint.yaml generation
  + version decoupling, mxlint-CLI latest v3.15.0 ≠ our hardcoded v3.14.2 path convention).
- README: mxcli step rewritten (auto-download with checksum + manual alternative), explicit
  warning "use the release binary, NOT git clone + make build (no make on Windows)".
- Verified: parser syntax OK; real download v0.12.0 (80,809,984 bytes, sha256 OK) → cache;
  `mxcli --version` works from the download; end-to-end installer (cached branch) writes absolute
  mxcliPath in clean settings; fresh zip contains the updated installer+guide, no TRB, no settings.

PACKAGING HARDENING (customer name + settings out of the shared package):
- ROOT CAUSE removed: the csproj copied my local acr-scan-settings.json (machine/customer paths)
  to bin\Debug\net10.0 via CopyToOutputDirectory=Always → when refreshing the payload
  that overwrote the sanitised version. The `<None Update="acr-scan-settings.json">` line is
  REMOVED. The extension needs no settings file (AcrScanSettings.Load → defaults: mxcli via
  PATH + open app). My local csharp-spike\acr-scan-settings.json remains for local
  use (no longer copied along).
- INSTALLER (Install-ClevrAcr.ps1) now owns the settings: asks + validates the project path
  (directory must contain an .mpr; neutral placeholder, no customer example), detects mxcli EXCLUSIVELY
  via Get-Command (PATH) — found → mxcliPath = .Source, not found → clear message +
  Mendix Labs install URL (PLACEHOLDER — still needs filling in) + mxcliPath="" (PATH fallback), and
  writes a clean acr-scan-settings.json at the read location. Upgrade preserves existing valid
  values.
- REPEATABLY SAFE assembly: Build-Package.ps1 (repo root, maintainer only) builds → mirrors
  bin → payload → DEFENSIVELY STRIPS any acr-scan-settings.json → zips → fails-loud on 'TRB'.
- CUSTOMER NAME scrubbed: rules.sample.json _pending ("on TRB" → "on the reference project");
  dist\clevracrshell (old mock with TRB sample data) removed; CLEVR-ACR-extension(.zip) contains
  'TRB' nowhere and no settings file (verified). OPEN: dist\CLEVR-ACR-source.zip is a
  complete source snapshot where 'TRB' is intrinsic in tests/ground-truth-docs/sample-data —
  fully scrubbing falls outside the "do not touch scan logic/tests" boundary; decision for Michel.
- Verified: bin no settings (correct), local settings untouched, package+zip TRB-free,
  installer writes clean settings (temp project test). Real Studio Pro install test: by Michel.

FINISHING ROUND (product made shareable — 6 points):
1. BUTTONS MERGED → one "Scan" button. New C# message `RunFullScan`
   (SpikeDockablePaneViewModel.RunFullScan) orchestrates on one background thread, in
   order: (1) mxlint export+lint (refreshes modelsource/), (2) mxcli + the CLEVR-own
   rules (security export + expression pass) that lean on that FRESH modelsource → solves the
   stale-modelsource pitfall structurally. Both steps run on the SAME project directory
   (ExclusionsProjectDir(), resolved once) so that export and rules point to identical
   modelsource. The loose routes (RunAcrScan/RunMxlintScan) remain internally.
2. LOADING INDICATOR: spinner next to the button + progress text ("Exporting model…/Analyzing…")
   via `ScanProgress` messages; button disabled during scan; `ScanFinished` re-enables.
   CSS pitfall fixed: `.acr-spinner[hidden]{display:none}` (explicit display:inline-block
   otherwise overrides the [hidden] attribute). Verified via static preview harness.
3. RENAMED "CLEVR ACR Spike" → "CLEVR ACR" in every visible place (menu item, pane title,
   log prefix). Internal IDs/DLL/namespace untouched (keep risk-free).
4. CLEVR LOGO: official CLEVR-logo.png in wwwroot/clevr-logo.png. Panel header shows it via
   <img src>; the exported report embeds it as data URI (fetch→FileReader on pane
   open → clevrLogoDataUri) so the report remains standalone.
5. INSTALLATION PACKAGE: dist/CLEVR-ACR-extension/ — clevracr/ (FULL build output incl.
   YamlDotNet.dll), Install-ClevrAcr.ps1 (asks project path, copies to
   <project>/extensions/clevracr, preserves existing settings on upgrade, verifies
   critical files), README.md (enabling Studio Pro extension development, directory location,
   mxcli/mxlint requirements). Script tested against a temp project: copies + verifies OK.
6. MENDIX 11+: requirement notice in the panel footer and in the report; prominent section in
   the README. (Runtime version detection not done: the 11.10 API simply won't load on Mx10,
   so a running extension is by definition 11+.)
Build 0/0, tests 86/86. Still to be done by the user: deploy via the script + verify
scan from Studio Pro (real flow).

Last updated after the session in which: Phase 1 (mapping fix) + Phase 2 (report
export) COMPLETED and verified in Studio Pro; Phase 3 part A (mxlint.com as 2nd
engine in the C# chain) COMPLETED after a tricky async/deadlock debug; and the
full mxcli surface systematically mapped — with the key
correction: the typed flow AST we "missed" IS in mxcli, via
`bson dump --format json`.

---

## WHAT NOW WORKS (proven in Studio Pro 11.10 on TRB)

### Lint rules (verified)
- 11 verified ACR .star rules, calibrated against the ground truth.
  Metadata from the ground truth; 4 security severities on "TODO-confirm".
- Recorded in acr-mxlint-voortgang.md + rule registry (rules.sample.json).

### The extension (hybrid C# + web, in Studio Pro)
- Hybrid architecture PROVEN: C# backend (.NET 10) runs a process via
  Process.Start, output via message bus to a C#-hosted webview pane.
- Working mxcli scan: button -> `mxcli lint --format json` -> parser -> C#-
  normalizer + registry -> Violation[] -> ACR layout in the pane.
- On TRB (mxcli): 2625 improvements (77 ACR / 2548 generic), distributed across the 6
  ACR categories (Performance + Reliability now populated thanks to the mapping fix).
- THREE engines now working: ACR .star rules + bundled mxcli rules + mxlint.com
  (Rego). mxlint runs as 2nd engine via a separate button (part A), results in
  a separate list — NOT yet merged with the mxcli data (= part B).
- UI: everything in 6 ACR categories; per-rule grouping (collapsible); origin
  badges + rule name; 3 count cards; origin filter; text filter; preview text;
  severity literally from the source. Term "Improvements".
- CLEVR-branded HTML report (Phase 2): same Violation[] + render functions as the
  pane, to <project>\.clevr-acr\CLEVR-ACR-report-<timestamp>.html + auto-open.
- Normalizer = pure, tested .NET 10 lib (25 tests green).

---

## NEW FINDINGS THIS SESSION (important — steering the roadmap)

### 1. mxcli can do much MORE than we use (`lint --list-rules`)
mxcli has a large set of bundled rules already running: QUAL001 (McCabe
complexity), CONV011 (commit-in-loop), CONV009/QUAL003 (microflow size),
ARCH001-003, SEC005-009, CONV013/014 (error handling), etc. The "flowgraph"
rules we assumed were Rego-exclusive are thus partly already done by mxcli.

### 2. The empty categories were a MAPPING BUG, not a missing engine
The display mapping maps on letter PREFIX (CONV/MPR/QUAL), but mxcli's CONV
contains naming, performance, quality and architecture rules mixed together.
As a result: Performance was only fed by the (non-existent) prefix PERF ->
always empty; Reliability mapped by nothing -> always empty.
FIX (small, existing data): map per rule on the REAL mxcli category from
`--list-rules` (which we already fetch for names) instead of the prefix. Measured on TRB
that fills both categories: Reliability ~388, Performance ~11.
Implementation note: lint JSON has no category per violation -> join on
`--list-rules`.

### 3. `mxcli report` already exists — almost your entire export phase
`mxcli report -p <mpr> --format html|json|markdown` gives a SCORED best-
practices report (overallScore, per-category scores, topActions/remediations,
all findings). HTML is standalone with embedded CSS. This can largely REPLACE
or FEED the planned export. Caveat: mxcli's own 7-category taxonomy and
styling, not the 6 ACR categories / CLEVR look. trb-report.html = good enough
for a product owner; the CSV export of mxlint is NOT.

### 4. mxlint.com PROVEN working in Studio Pro (own extension installed)
mxlint-cli `export` (model -> YAML in modelsource/) + `lint` (Rego on the YAML)
runs, locally, and is installed as a Studio Pro extension. Proof: 2363 checks,
24 rules, 182 fails on TRB. Adds rules that mxcli does NOT have:
- ComplexMicroflowsWithoutAnnotations (103), NumberOfElementsInMicroflow (44),
  InlineStylePropertyUsed (14), HeadingsInAscendingOrder (11, accessibility),
  NoDefaultValue (8), MxAdminNotUsed (1), OneH1TagPerPage (1).
HONEST NUANCE: AvoidCommitInLoop gave 0 on TRB; the REALLY deep ACR-Performance
rules (Non-indexed attr in XPath, CRUD too early in flow, XPath ordering — the
~804 ACR-Performance violations) are also NOT in mxlint.com. Those remain the
domain of ACR/SDK + the Studio Pro Best Practice Recommender.
CONCLUSION: mxcli + mxlint.com together = a large, valuable part of ACR — NOT
"everything". Communicate honestly: not "ACR fully replaced".

### 5. The mxlint sources are OPEN (no need to reinvent the wheel)
- mxlint-cli (Go) — source code obtained (mxlint-cli-main.zip).
- mxlint-extension (Studio Pro) — source code obtained (mxlint-extension-main.zip).
- Rego rules — mxlint-rego-inventaris.md (28 rules, metadata in # METADATA block).
Claude Code can reuse their approach instead of starting from scratch. NOTE CRLF (\r) in
the Rego metadata when parsing.

### 6. FULL mxcli inventory — the missed depth is in `bson dump`
Systematically gone through every mxcli command + subhelp (no sampling anymore). Key
correction on an earlier conclusion: we thought only mxlint could provide the typed
flow AST (describe only gave MDL TEXT). WRONG — `mxcli bson dump
--type microflow --object <name> --format json` gives the FULL typed
model AST: $Type nodes with LoopedActivity, CommitAction, CreateChangeAction,
ExclusiveSplit, expressions — the same structure as mxlint's YAML (both read
the same BSON from the .mpr v2).
CONSEQUENCE: a deterministic flow rule (e.g. commit-in-loop) is possible in mxcli ALONE
by traversing that tree — without mxlint, without Rego, without
parsing MDL text. This is the "third way" (see below).
NUANCE: bson dump = RAW BSON-as-JSON (verbose, {Key,Value} form, alpha
"inspection" tool), per element (--object; --list to enumerate). mxlint gives
a cleaned-up tree + ready-made Rego engine. So: for INDIVIDUAL own checks
in C# bson dump is realistic; for a BROAD rule package mxlint remains more efficient.
OTHER useful, not yet used commands (all --json): impact, refs,
callers/callees (call graph), context (relations), and the MDL CATALOG query
(`SELECT ... FROM CATALOG.ENTITIES --json` → AttributeCount, AccessRuleCount,
HasEventHandlers, Generalization, QualifiedName...). `eval` = fixed check set
(entity_exists, lint_passes, mx_check_passes...) — NO place for own flow
logic, only an acceptance/regression harness. structure/show/project-tree =
name/signature level (no flow internals).
SO: we now have the FULL surface mapped; the depth is accessible
via mxcli itself (bson dump). Earlier "only mxlint can give the tree" conclusion =
corrected.

### 7. Lessons learned from the Phase 3 part A debug (for future engines)
- PIPE DEADLOCK: a process with lots of stdout/stderr (mxlint lint = hundreds
  of lines) deadlocks if you read the streams SEQUENTIALLY (first all stdout,
  then stderr). Fix: drain both streams PARALLEL async before WaitForExit,
  + a timeout safety net. ProcessRunner now does this for all calls.
- ASYNC/UI THREAD: WebView2 PostMessage MUST be on the UI thread. The MessageReceived
  thread has a WPF DispatcherSynchronizationContext (verified: present);
  heavy work via Task.Run, result marshalled back via that context, and
  GUARANTEED to post in every outcome (otherwise the pane stays silent on "Busy...").
- DIAGNOSTICS: ILogService writes to Studio Pro's internal log (Help -> Open
  Log File Directory), NOT to %LOCALAPPDATA%\Mendix. Therefore the
  extension now also writes to <project>\.clevr-acr\mxlint-debug.log (findable). LESSON: when
  stuck in a closed box first MAKE VISIBLE what is happening, then fix
  — that broke the deadlock after several guess-rounds.
- mxlint EXIT 1 = findings (not an error): read the jsonFile regardless of exit code.
- mxlint BUNDLES multiple violations of the same rule per document into 1
  failure.message, separated by the [SEVERITY, CATEGORY, rulenumber] marker.
  Normalizer now splits on that marker: 182 "failures" -> 480 individual violations.
- FINGERPRINT LIMITATION: split violations of the same rule on the same
  document now share 1 fingerprint (the attribute is only in the reason, and the
  spec prohibits reason in the fingerprint). Consequence for Phase 6: exclusion works at
  rule+document level, not per attribute. Per-attribute would require reason-parsing in
  elementName — deliberately deferred.

---

## OPEN PHASES (REVISED order after the findings)

### Phase 1 — Display mapping per rule  ✅ COMPLETED (verified on TRB)
Generic rules now map on the real mxcli category from `--list-rules` instead of
the prefix. Verified in Studio Pro: Performance (CONV016/017) and Reliability
no longer empty; CONV011→Performance and CONV001→Project hygiene (same prefix,
now different category = bug gone). Mapping table in spec §5; "mxcli
correctness → ACR Reliability" explicitly recorded. Display mapping in the render
layer; internal Violation.category unchanged.

### Phase 2 — Report export  ✅ COMPLETED (verified: report looks good)
Option B chosen: own CLEVR-branded HTML from the same Violation[] + render functions
as the pane (consistent with what the developer sees), instead of mxcli's own HTML.
Storage: <project>\.clevr-acr\CLEVR-ACR-report-<timestamp>.html + auto-open + path
in the status line (no native save dialog in the Studio Pro API). mxcli report
remains a later option as a data source for scores/remediations.

### Phase 3 part A — mxlint.com as 2nd engine (C# chain)  ✅ COMPLETED
MxlintScanService (export+lint via Process.Start, async, parallel stream drain,
exit≠0=findings), MxlintNormalizer (split on marker → 480 violations, source=
mxlint, CRLF trim), separate button + separate list. Verified on TRB: 480 individual
violations. See finding 7 for lessons learned. NOT yet merged with the
mxcli data (= part B).

### Phase 3 part B — merge mxlint into the main panel  ✅ COMPLETED
mxlint violations are now merged with the mxcli data in the 6 ACR categories
(separate list gone). Two separate buttons, one overview; replaceOrigin() only replaces
the violations of the scanned origin (you can run mxcli and mxlint and
see them together). Origin filter Mxlint.com now populated; 3 count cards count the merged
set. 26/26 tests. The 3 choices made:
1. PRECEDENCE = mxcli > ACR > mxlint (option A; Mendix is backing mxcli → front,
   even above the calibrated ACR rules). The 6 ACR↔mxlint overlaps
   (AnonymousDisabled↔ACR_SEC_GUEST, DemoUsersDisabled↔ACR_SEC_DEMOUSERS,
   SecurityChecks↔ACR_SEC_CHECKED, StrongPasswordPolicy↔ACR_SEC_PWPOLICY,
   NumberOfAttributes↔ACR_ENT_ATTRS, AvoidUsingValidationRules↔ACR_ENT_VALRULES)
   are suppressed on the mxlint side (spec §4).
2. ACCESSIBILITY → Maintainability via display mapping (spec §5; internally unchanged).
3. TWO buttons, one screen — functional: mxcli works on Mx11, mxlint also on
   Mx10, so usable across both versions. mxlint async, mxcli synchronous.

>> NEWLY DISCOVERED, NOT YET RESOLVED — ACR↔mxcli overlap (own follow-up task):
   there is ALSO overlap between your ACR .star rules and mxcli's bundled rules — e.g.
   ACR_SEC_STRICT ↔ SEC005 StrictModeDisabled: both report "strict mode off" on
   the same document, slightly differently worded → duplicate in the report. According to
   precedence A mxcli wins (SEC005 also has the CVE-2023-23835 reference);
   ACR_SEC_STRICT should be suppressed. FOLLOW-UP TASK (fresh session): systematically go through all
   11 ACR rules against the mxcli-bundled rule list and decide per rule:
   suppress (mxcli wins) OR entirely DROP the ACR rule because mxcli already covers it.
   Suspected candidates: the 4 ACR_SEC_* (vs mxcli SEC001-009). Cleanup pass,
   not a quick fix — requires the inventory first.

### Phase 4 — Clickable object: navigate to element + docs  [BUILT]
Turned out to be more broadly feasible than just docs. Verified against the REAL 11.10 assembly
(reflection). BUILT, compiles (0/0), tests 26/26:
(a) OPEN DOCUMENT IN STUDIO PRO — click on the document line of an improvement
    opens the document. C# resolves the unit: first via stable GUID
    IModel.TryGetAbstractUnitById(documentId) (mxcli/ACR have documentId);
    fallback name walk Root.GetModules()->module->DomainModel/folders/GetDocuments()
    (mxlint has NO GUID). Navigation at DOCUMENT LEVEL via
    IDockingWindowService.TryOpenEditor(unit, null) — just like the mxlint extension.
(b) DOC URL IN BROWSER — click "Documentation" opens the URL via
    Process.Start{UseShellExecute=true} (same pattern as opening report).
Injection: SpikeDockablePaneExtension now imports IDockingWindowService and passes
() => CurrentApp (IModel) + the service to the VM. Handlers: "OpenDocument",
"OpenUrl". Data/UI separation intact: Violation unchanged; JS posts only
existing fields; the HTML report remains static (interactive=false) with a
normal working doc href.
NAVIGATION PER DOCUMENT TYPE (verified against 11.10 via net10 reflection):
- microflow/page/enumeration: GUID route (TryGetAbstractUnitById) → opens. ✓
- entity: NO unit GUID → name route → OPEN DOMAIN MODEL AND FOCUS the entity
  (IEntity is an IElement; IDomainModel.GetEntities() → match on name →
  TryOpenEditor(domainModel, entity)). Fallback = domain model without focus. ✓ [BUILT]
- subfolder docs (microflow/page etc.): name walk is RECURSIVE (module → folders →
  documents); works. Pages/microflows usually already open via the GUID route. ✓
- SNIPPETS = API BOUNDARY (definitive, via net10 reflection): the 11.10 ExtensionsAPI knows
  NO snippet type. The complete IDocument/IAbstractUnit set is: IConstant, IEnumeration,
  IJavaAction, IMicroflow(+Rule/ServerSide/Base), IPage, IDomainModel. No ISnippet.
  Moreover mxcli gives snippets an EMPTY documentId (while pages/microflows/enums/
  entities DO get a GUID) → the SDK also assigns no unit identity to snippets;
  and GetDocuments() does not return them (recursive walk misses them, confirmed in the log).
  Conclusion: snippets cannot be directly opened via this API. Click shows an
  honest message instead of "not found". (Earlier double-module diagnosis was NOT the
  cause — that was a real separate bug and has been fixed.)
SYSTEM MODULE FILTER (render layer, main.js): violations from the System module are COMPLETELY
hidden in the display (list, 3 count cards, total, exported report) — System is not
modifiable by a developer → noise. Determined by the qualified name (prefix "System." or exact
"System"). Data remains complete (data/UI separation); purely a display filter, on top of the existing
category/severity/origin/text filters + reset. On TRB: 78 of 2625 raw violations are System.
- ENUMERATIONS = HOST BOUNDARY: IEnumeration is a unit, TryOpenEditor technically succeeds,
  but Studio Pro shows enums as a DIALOG (not always visible from extension). No
  alternative show API. We still open, but show an HONEST message.
- PROJECT SECURITY = API BOUNDARY: project-level artefact, NO module document. 11.10 has
  no ISecurity type/open method; IProjectDocument has no Name to match on;
  INavigationManagerService only handles web menus. Click shows honest message:
  "Project security cannot be opened directly via the Extensibility API (11.10)."
All routes log their choice + outcome in mxlint-debug.log.

MXLINT RULE NAMES: mxlint rules now show a descriptive name next to their number
(002_0009 → NoDefaultValue), just like mxcli. Source: the # METADATA `rulename` from the .rego files
→ fixed map MxlintRuleNames (25 rules). The lint-results.json itself has NO name, only
the .rego/.js file path. MxlintNormalizer.BuildRuleNames(json) builds rulenumber→name per
testsuite: fixed map, otherwise PascalCase of the filename slug — so even rules not
(yet) in the map get a name (e.g. the .js accessibility rules 004_0003 one_h1 → OneH1,
004_0004 headings → Headings; the reference ruleset is older than what runs in TRB). The service
puts this in payload.ruleNames (same form as mxcli); main.js merges both engines in
lastRuleNames → render layer shows it via ruleName() (no render change).

UI LANGUAGE: the full extension UI is now consistently ENGLISH (buttons, count card headers,
status/error messages, tooltips, placeholder, report header). The ACR category names
(Project hygiene/Maintainability/Performance/Architecture/Reliability/Security) are
unchanged — they belong to the data contract. Debug log texts deliberately remain in Dutch (internal).

### Phase 5 — "Ask Maia" prompt (PASTE variant)  [BUILT — paste variant]
Render layer (main.js): "Copy Maia prompt" button at TWO levels:
- RULE header: prompt for the entire rule with all its points (capped at 50, "... and N more"
  so large rules like the 283-point default-value rule don't explode).
- INDIVIDUAL point: prompt focused on that one case.
Prompt is ENGLISH and contains ruleId+name, category (displayCategory), severity, origin
engine, document(s), reason(s) and suggestion(s). Copy via navigator.clipboard with
fallback on textarea+execCommand (WebView2 sometimes blocks clipboard). Confirmation:
"Maia prompt copied — paste it into Maia". Not in the exported report (interactive=false).
Data/UI separation intact. DIRECT injection into Maia remains unproven → not built.

### FIRST OWN RULE on the project security export — ACR #12  [BUILT]
"Project role should have at most one module role per module" (CLEVR-MAINT-005). No
mxcli/mxlint — own pure parser ProjectSecurityParser (csharp-normalizer, mirror of
BsonMicroflowParser; 6 tests). Source: modelsource/Security$ProjectSecurity.yaml (UserRoles[]
→ {Name, ModuleRoles:["Module.Role"]}); group per user role on the module part; >1 =
violation. Identity: ruleId CLEVR-MAINT-005, acrCode ProjectRoleMaxOneModuleRolePerModule,
engineRuleKey CLEVR_SEC_ONE_MODULEROLE_PER_MODULE (self-produced, NOT claimed by mxcli →
deliberately not in rules.sample.json). Category Maintainability (ACR: Performance — deliberate choice,
one constant to adjust), severity Critical. Origin: kind=acr/source=clevr-acr →
ACR badge. Integration: attached to the mxcli "Scan for improvements" (AcrScanService reads the YAML
from the project directory → Violations in the AcrViolations payload). Verified against TRB ground truth:
exactly 5 violations / 2 roles (Administrator on Accesslog/Administration/SupportModule/UserCommons
+ Behandelaar on TRB), 7 roles clean — no false positives/negatives. NB: MDL CATALOG does not expose this
mapping → the YAML export is the source.

### TWO SECURITY RULES on the export — ACR #7 + #10  [BUILT]
Both kind=acr/source=clevr-acr (ACR badge), Security/Blocker (like ACR), integrated in the
mxcli "Scan for improvements" (AcrScanService), pure tested parser extensions on
ProjectSecurityParser. Anonymous role set = ModuleRoles of GuestUserRole IF EnableGuestAccess
is true (otherwise 0). On TRB now guest ON with GuestUserRole=WebserviceUser (set: System.User,
Administration.User, Integratie.Admin, Accesslog.Admin). NOTE: the modelsource export can be stale
— run `mxlint export` first for fresh YAML (TRB modelsource was 4 days old).
- ACR #7 (CLEVR-SEC-005, AnonymousCreatePersistentEntity): persistent entity with AccessRule
  AllowCreate:true + an anonymous AllowedModuleRole. Persistable from CATALOG.entities.EntityType
  (most reliable source — YAML puts Persistable nested under MaybeGeneralization + INHERITS via
  generalization). Access rules from the domain model YAML. TRB ground truth (verified, FP/FN-free):
  1 violation = Accesslog.AccesslogBankenportaal (via Accesslog.Admin). Integratie.Melder/Melding
  have anon-create but are NON_PERSISTENT → correctly not flagged.
- ACR #10 (CLEVR-SEC-006, AnonymousEditableUnlimitedString): unlimited string attribute (Length 0)
  that is ReadWrite for the anonymous role (MemberAccess under an anonymous AllowedModuleRole).
  Length from the YAML (StringAttributeType.Length); CATALOG.attributes.Length is UNRELIABLE
  (0 for all 748 strings). No persistable filter. TRB ground truth (verified, FP/FN-free):
  4 violations = Accesslog.AccesslogBankenportaal.Message, Integratie.Melder.Overige_Gegevens,
  Integratie.Melding.BeschrijvingGedrag + .Overige_Gegevens.
Finding: modelsource contains only 67/283 entities (only app-own modules, just like mxlint);
System/marketplace not exported → these rules cover the app scope (consistent with mxlint).

### FIRST EXPRESSION ROUTE RULE — redundant empty string check (CLEVR-REL-001)  [BUILT]
New route: parse Mendix expression STRINGS from the bson AST (expressions are flat, not as
sub-AST). Source: ExpressionSplitCondition.Expression (split conditions) + ChangeActionItem.Value
(assignments). Pure ExpressionAnalysis.RedundantEmptyStringPaths (regex) + bson extraction
(BsonMicroflowParser.DetectRedundantEmptyStringChecks + generic VisitNodes walker). CONSERVATIVE:
flag only if ON THE SAME path ($x/Attr) both an empty check (=/!= empty) AND an empty-string
check (=/!= ''/"") are present in the same expression; standalone != empty (396 idiomatic) and standalone != ''
are NOT flagged. Category Reliability (leans toward correctness; one const, adjustable to
Maintainability), severity Major (proposal), kind=acr/clevr-acr, engineRuleKey
CLEVR_REL_REDUNDANT_EMPTY_STRING. TRB ground truth (verified, FP/FN-free): 19 distinct
(microflow,path) over 8 microflows; FP check on a microflow without empty-string literal = 0. NB:
more than the exploration's rough 15 — that missed the "= empty or = ''" form (IVK_SaveDossier) +
YAML quoting; the conservative bson detection is more accurate. 13 tests. NOT hung in the live scan:
that requires per-microflow bson-dump (~1592×, too slow synchronously) — shared orchestration, to be decided
when batching more expression rules (the extraction is then reusable; 2nd rule ~rule-logic + tests).

### EXPRESSION ROUTE LIVE — orchestration + REL-001 + rule D (CLEVR-MAINT-006)  [BUILT]
SOURCE DECISION (with numbers): YAML beats bson. Speed: YAML one-pass over 471 microflow
YAMLs = 0.89s (2891 expressions); bson = ~2s/dump × 471 ≈ 16 min. Reliability: 110 expressions
are block scalars (multi-line) + quoting → therefore a REAL YAML parser (YamlDotNet, in the spike;
normalizer remains dependency-free). Proven: YAML reproduces the bson expressions exactly (cross-check
against bson on the new microflows, incl. block scalar $ZoekObject/Voornaam). Shared infra:
AcrScanService.DetectExpressionRules → MicroflowYamlExpressions.Extract (YamlDotNet) → (mf,expr) pairs
→ ExpressionRules. Pure rule layer in the normalizer: ExpressionAnalysis (string predicates) +
ExpressionRules (Violation build from pairs); bson and YAML route share the same rule layer.
- CLEVR-REL-001 (redundant empty string): now LIVE in "Scan for improvements". VERIFIED
  ground truth = 31 distinct (microflow,path) over 14 microflows — NOT 19. The earlier 19 came from
  an incomplete grep candidate selection that missed block-scalar microflows; the full YAML scan
  is more accurate (bson cross-check confirms the extras).
- CLEVR-MAINT-006 (redundant boolean comparison $x = true/false): category Maintainability,
  severity Major (both adjustable), kind=acr/clevr-acr. Conservative: operand must be a $path;
  only the true/false literal (word boundary, no enum/identifier). VERIFIED
  ground truth = 94 distinct (microflow,operand) — NOT ~40 (same block-scalar reason). 13 tests.
SCAN DURATION: the expression pass = 0.89s (negligible next to the mxcli-lint). Scales: a 3rd
expression rule reuses the same pass (pairs in memory) → ~ms. Caveat: reads modelsource
(refreshed by mxlint export) — run that before the scan for fresh expressions.

DEPLOY BUG (found + fixed): YamlDotNet.dll was NOT deployed → in Studio Pro
MicroflowYamlExpressions.Extract failed on assembly load → the try/catch in DetectExpressionRules swallowed
it (to ILogService, not findable) → 0 expression violations (the other rules worked because they
don't touch YamlDotNet). CAUSE: a class library does not copy NuGet runtime deps to the output.
FIX in Clevr.AcrSpike.csproj: <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies> +
ExtensionsAPI with ExcludeAssets="runtime" (Studio Pro provides those itself — do not copy along). Verified:
YamlDotNet.dll is now in bin/Debug/net10.0, ExtensionsAPI.dll not. DEPLOY the ENTIRE output directory
(incl. YamlDotNet.dll), not individual DLLs. DIAGNOSTIC LOGGING (DebugLog → mxlint-debug.log) is now in
DetectExpressionRules: projectDir + modelsource path + #YAMLs + #expressions + #violations + full
exception; plus the path comparison (mxcli/rules projectDir vs _getProjectDir of the open app) to
make a possible second cause (export refreshes path B, scan reads path A) visible.

### Phase 6 — Exclusions with mandatory reason  [BUILT]
An improvement can ONLY be excluded with a reason (no silent exclusion). Stored in
$project/.clevr-acr/exclusions.json (included in version control → team shares). Match on the
fingerprint sha1(ruleId|documentQualifiedName|elementName), already on every Violation.
- C# (pure, tested): Exclusion record + ExclusionsJson (parse/serialize/upsert/remove,
  4 tests). IO in ExclusionStore (csharp-spike); ViewModel handlers RequestExclusions/
  AddExclusion/RemoveExclusion → writes the file, posts "Exclusions" back. excludedBy=
  Environment.UserName, date=today. Server-side safeguard: empty reason → ExclusionError.
  Exclusions land in the SAME project directory as the scan (settings.projectPath, otherwise CurrentApp).
- Render layer (main.js): "Exclude" button per point → modal dialog that REQUIRES a reason
  (Exclude button disabled until text is entered). Excluded improvements disappear from list +
  all count cards + total + report (activeViolations() = not-System and not-excluded),
  on top of the existing category/severity/origin/text filters. "Show excluded (N)" toggle
  shows the Excluded section with reason + excludedBy/date + "Remove exclusion".
- FINGERPRINT LIMITATION (finding 7) HONESTLY handled: bundled mxlint violations share
  one fingerprint → the dialog WARNS in advance ("shares one fingerprint with N findings ...
  will hide all N") and the Excluded card shows "applies to N findings". Not rebuilt.
- STALE exclusions (fingerprint matches no current violation) visibly marked
  ("stale — no longer matches"), with Remove. REPORT: separate "Excluded improvements" section
  (matched + stale, with reason), static (no buttons). Toast feedback on exclude/remove.
- EXCLUDE RULE (extension): "Exclude rule" button on the rule header (next to Copy Maia prompt)
  excludes ALL points under the rule with the SAME reason. Reuses the mandatory-reason
  dialog (openReasonDialog). Writes one exclusion per UNIQUE fingerprint via a batch
  ("AddExclusions" → ExclusionStore.AddMany, one file write, upsert=dedup) — bundled
  mxlint points with shared fingerprint therefore NOT duplicated. Dialog count = number of unique
  fingerprints; with bundling a note explains the difference ("N findings map to M entries").
  Per-point Remove keeps working.
- EXCLUDED SECTION PER RULE (extension): the "Show excluded" section is grouped PER RULE
  (rule header = ruleId + name + count, like the main list) with the excluded points below;
  stale entries are in their rule group with "stale" marking. "Remove rule exclusion" button
  on the rule header restores ALL entries of that rule (incl. stale) at once via a
  light confirmation dialog ("This will restore all N excluded findings for this rule.") →
  batch "RemoveExclusions" → ExclusionStore.RemoveMany (one file write). Per-point Remove
  remains available. Report: same grouping, static (no buttons).

### Manual checks — control questions the developer answers themselves  [BUILT]
Generic + extensible mechanism (first question: Performance/Major about the Best Practice
Recommender). A manual check is not a model violation but a fixed question that appears as a normal
improvement until validly answered, and must be rechecked after 30 days.
- DEFINITIONS + expiry logic in the render layer (main.js: MANUAL_CHECKS, MANUAL_CHECK_EXPIRY_DAYS).
  C# is GENERIC: only stores the answer per id (ManualCheckStore → $project/.clevr-acr/
  manual-checks.json, included in version control, NOT gitignored). Pure model+json in the normalizer
  (ManualChecksJson, tested). Handlers: RequestManualChecks/AnswerManualCheck/ClearManualCheck.
- STATE: unanswered / "no" (+reason) / "yes" (+note). Mandatory note via the REUSED
  reason dialog (extended with Yes/No buttons). Stamped with Environment.UserName + date
  (server-side safeguard on empty note). Valid "yes" (<30d) → DISAPPEARS from open list/count →
  "Answered manual checks" section (toggle, analogous to Show excluded) + in the report, with
  answer/date/who/recheck date. Expired "yes" (≥30d) or "no"/unanswered → counts as open.
- INTEGRATION: open checks become synthetic violations (kind="manual", origin "manual" — 4th
  origin in the filter + count card + status + report header) and flow through the existing
  pipeline: category (Performance), counts, filters, System filter, Exclude + Ask-Maia.
  Data/UI separation intact; exclusions infra reused (store/handlers/dialog/sections).

### Phase 5 (old design) — "Ask Maia" prompt (PASTE variant)  [feasible; injection NOT]
Split the idea in two:
- FEASIBLE: an "Ask Maia" button on an improvement that GENERATES a context-rich PROMPT
  (improvement + rule + document + remediation) that the developer pastes into Maia
  themselves. This is purely assembling text in your own panel -> certainly possible.
- UNPROVEN: inject the prompt DIRECTLY into Maia. Requires a Maia API
  for extensions whose existence is NOT known. Do not build on this until
  proven. Start with the paste variant; that delivers almost all the value.

### Phase 6 — Exclusions UI  [valuable once in use]
Suppress improvements with reason. Spec section 3 is ready (fingerprint,
$project/.clevr-acr/exclusions.json, show stale exclusions visibly). NOTE the
mxlint fingerprint limitation from finding 7 (rule+document level, not per
attribute, unless you add reason-parsing).
DESIGN REFINEMENT (Michel): a user MUST give a reason to exclude an
improvement (no silent exclusion — always accountability). The
exclusion + reason is recorded in $project/.clevr-acr/exclusions.json (sits
IN the project directory → goes with version control). On commit + next pull the
next developer sees the exclusion + reason. The excluded improvements + reason
must be VISIBLE in the CLEVR report (transparent to product owner /
next developer: "deliberately not resolved, because ..."). NB: per-point exclusion
for mxlint collides with the fingerprint limitation (finding 7) — weigh deliberately.

### Phase 7 (OPTIONAL/EXPLORATION) — "third way": own deep rules on `bson dump`
Now that it is proven that `mxcli bson dump --format json` gives the typed flow AST, you can
build own deterministic Orange-tier rules (flow/expression) in C# on
mxcli ALONE — without mxlint. Trade-off: raw/verbose BSON + enumerate per element
vs. mxlint's clean tree + Rego. DO NOT build now; relevant if you want to stay single-
engine OR if Mendix ever cleans up the BSON output to clean JSON
(plausible given their AI direction). Many flow checks already exist in mxcli lint
(CONV011/QUAL001/CONV009) — those you definitely don't need to build yourself.

---

## CLEANUP / DECISION POINTS (not urgent, but be aware)
- SPIKE -> PRODUCT: the "spike" codebase is effectively the product basis. Decide
  deliberately: finalise (rename/clean up) or rebuild cleanly.
- Old echo/RunCommand handler = dead code -> clean up.
- 4 security severities (ACR_SEC_*) on "TODO-confirm" -> from ACR Java source.
- DISTRIBUTION: colleagues must NOT need npm/build. Look into Mendix extension
  packaging (Marketplace / zip / installer). Tip: see how the
  mxlint-extension (open source) does its distribution.
- STRATEGIC (discuss with CLEVR colleagues): Mendix is developing mxcli as
  AI access to Mendix. Position the extension as the CLEVR AGGREGATOR +
  CLEVR context (categories, calibration, report-for-client, Ask-Maia) on top of
  the engines — NOT as "ACR rebuilt". Actual state after the inventory:
  * mxcli + mxlint cover the BROAD Blue tier (structure, naming, security,
    accessibility, complexity-as-count) well.
  * The DEEP flow/XPath rules (ACR's ~804 Performance set) are ready-made in
    NEITHER. BUT: the typed structure to build them yourself is
    accessible via mxcli bson dump (Phase 7) — so the extension is NOT
    pinned to the Blue tier; you have options and grow with mxcli.
  * SDK route (running ACR's Java/SDK in the extension) = discouraged: order of magnitude
    heavier, reproduces ACR, rows against Mendix's direction.

---

## DOCUMENTS (the "memories" of this project)
- clevr-acr-shell-spec.md ......... data contract + architecture (authoritative)
- clevr-acr-shell-status.md ....... this document (compass)
- acr-mxlint-voortgang.md ......... the 11 verified rules + API facts
- acr-mxlint-indeling.md .......... feasibility map (Green/Blue/Orange/Red)
- mxlint-rego-inventaris.md ....... the 28 Rego rules for Phase 3
- acr-rule-counts-groundtruth.json  the ACR ground truth (authoritative source)

## REFERENCE SOURCES (open source, unpacked in _reference/ — NOT your own code)
Relevant from PHASE 3 (mxlint.com integration) and PHASE 4 (clickable docs) onward.
Not needed for Phase 1/2. Point Claude Code SPECIFICALLY to the relevant part,
do not let it plough through the entire repo.
- _reference/mxlint-cli ........... how `export` (model->YAML) and `lint` work
- _reference/mxlint-extension ..... how they do it in Studio Pro (incl. clickable docs)
- _reference/mxlint-rules ......... the Rego rules + metadata (# METADATA block; NOTE CRLF)

## IDEAS PARKING LOT (weigh later, don't forget)
- Ask-Maia DIRECT injection (if a Maia extension API ever turns out to exist).
- (Add new ideas here so they don't interrupt the current phase but are
  also not lost.)
