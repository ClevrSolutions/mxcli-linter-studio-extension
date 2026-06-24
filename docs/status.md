# CLEVR ACR Shell — stand van zaken & routekaart

Werkdocument. Wat is af, wat ligt er nog, in welke volgorde, en wat moet per
fase eerst BEWEZEN worden vóór je erop bouwt. Discipline door het hele project:
"een regel/feature die draait is geen regel/feature die klopt" — verifiëren tegen
echte data, niets verzinnen, aannames bewijzen vóór je erop bouwt.

MXLINT.COM UIT DE UI (alleen weergave; export ONgemoeid). Verwijderd uit het paneel + rapport:
(1) telkaart-bron "Mxlint.com" — uit de "per source"-array (main.js) + de twee hardcoded
breakdown-strings (`… / ${oc.mxlint} Mxlint.com` → weg) in paneel-status én rapport-subtitle;
(2) source-filter-checkbox — `{key:"mxlint",label:"Mxlint.com"}` uit ORIGINS; (3) rapport-raw-blok —
de orphan `.acr-mxlint-raw`-CSS (227-234) in index.html (de tabel-builder was al weg; CSS werd nog
mee-geëmbed in elk rapport via buildReportHtml); (4) paneel-teksten — subtitle "via mxlint + mxcli…"
→ "via mxcli + the CLEVR rules", scan-tooltip "mxlint export + lint…" → "exports the model source…".
"MxCLI Mxlint" (source=mxcli) BLIJFT zichtbaar. De source==="mxlint"-takken in originLabel/originBadge
blijven als dode, onschadelijke guards (engine levert geen mxlint-violations meer) — net als de
claim-tabel-entries (tripwires bewaken ze). index.html nu 0 mxlint-refs.
README-check: de mxlint-extensie-BOOTSTRAP-stap (sectie 5) is NIET obsoleet — de EXPORT heeft de binary
nodig; al herschreven als "for the model export" (niet "extra Rego rules"). Dus bewust behouden.
VERIFICATIE: YAML-routes intact (MAINT-010=283, MAINT-015=33, SEC-011=0); build 0/0, tests 200/200
(tripwires groen). HERPAKT: dist\CLEVR-ACR-extension(.zip) ververst (17:21), TRB-check OK, geen settings
in payload, index.html in payload 0 mxlint, count-card toont "ACR / MxCLI Mxlint / Manual". Scan-test: Michel.

REGO-ENGINE UITGESCHAKELD als findings-bron (export BLIJFT). MxlintScanService: alleen nog `export`
(ververst modelsource/), de `lint`-aanroep + MxlintNormalizer + lint-results.json-lezing zijn weg →
payload source="mxlint-export", regoEngineDisabled=true, violations=[]. Binary ONgemoeid (gedeeld met
export). RunFullScan-volgorde ongewijzigd (export eerst, dan mxcli+CLEVR). main.js: handleMxlintResult
toont nu "model export refreshed (Rego engine disabled)" i.p.v. lint-telling; replaceOrigin([],["mxlint"])
wist elke mxlint-herkomst. Installer had GEEN mxlint-download/gate (alleen mxcli) → niets te verwijderen;
README/gids herschreven: mxlint = "model export", niet langer "extra Rego rules". mxlintPath-setting BLIJFT
(export heeft de binary nodig). VERIFICATIE: YAML-routes intact op huidige modelsource — MAINT-010=283,
MAINT-015=33 (verwacht); modelsource aanwezig (12 domeinmodellen, 113 pages); geen lint-call/Normalize meer
in MxlintScanService (alleen export). Claim-tabel-entries blijven staan (schaden niet, tripwire bewaakt ze).
Build 0/0, tests 200/200. PAKKET HERPAKT: Build-Package.ps1 → dist\CLEVR-ACR-extension(.zip) ververst
(nieuwe DLLs + main.js + README), TRB-safety-check OK, geen settings in payload. Lokale dev-settings
(TRB-paden) ONgemoeid. Echte Studio-Pro-Scan-test: Michel.

mxlint VOLLEDIG VERWIJDERD (A-D); FASE E duur-check WIJKT AF → gestopt vóór repack.
FASE A: MAINT-006 → describe-sweep (deepscan). Live geverifieerd: 104 vs oude YAML-GT 94. AFWIJKING =
METRIEK (geen verlies): describe ⊇ YAML (alle 94 + 10 extra), de 10 extra zijn echte redundante boolean-
vergelijkingen in VARIABELE-TOEWIJZINGEN ($Valid/$Valide/$Vrijspraak…) die de oude YAML-route (enkel split-
condities + change-values) niet extraheerde. 104 = nieuwe, completere GT. Synthetische test toegevoegd.
FASE B: SEC-006 gedeprecateerd (DetectAnonymousEditRules niet bedraad; ProjectSecurityParser-leescode =
backup met "reactiveer zodra mxcli String(N) terugleest"). Geen claim-tabel/tripwire-entry (geen mxlint-twin).
FASE C: gate-check → 0 actieve modelsource-lezers (3 actieve providers: security/catalog/describe, allen
describe/.mpr/catalog). ✓
FASE D: mxlint-export-aanroep uit RunFullScan (beide modi). Installer: mxlintPath-setting weg. README:
sectie 5 (mxlint-extensie-bootstrap) verwijderd, intro/warning/config/troubleshooting herschreven (geen
mxlint, mxcli automatisch, Scan+Deepscan). MxlintScanService + YAML-readers blijven deprecated backup.
FASE E: build 0/0, tests 232/232. MAAR de duur-check WIJKT AF: snelle scan ≈ ~46s, NIET ~12s. Oorzaak:
de security-route (SEC-005/008/010 + MAINT-005) draait in de SNELLE scan en kost ~32s — vooral de 9
`describe userrole`-calls (~20s) die NIET batchbaar zijn (MDL kent geen DESCRIBE USERROLE-statement, alleen
de CLI-subcommand) + project-tree. De export (19s) verdween maar de security-route (~32s) kwam ervoor terug.
GESTOPT vóór de repack. AANBEVELING (mode-placement-beslissing voor Michel): verplaats de userrole-zware
regels SEC-010 + MAINT-005 naar de DEEPSCAN (zoals de andere describe-zware regels) → snelle scan zakt naar
~26s; of split de security-route (SEC-008 projectsecurity + SEC-005 catalog blijven goedkoop/fast, SEC-010+
MAINT-005 naar deep) → snel ~14s, dicht bij het doel. Daarna pas repacken. mxlint-verwijdering zelf is correct
en compleet; alleen de fast/deep-indeling van de security-regels moet beslist worden vóór we de duur claimen.

STREAMING GEBOUWD (progressieve findings, beide modi) — FASE 1/2/3 af, build+tests groen, herpakt.
FASE 1 (orchestratie): AcrScanService.RunScanStreaming(deepScan, Action<string> emit) — gedeelde RunFastPhase
(lint+security+catalog+regel-catalogus) → emit FAST-batch (volledige metadata + snelle findings, final=!deep);
bij deep daarna MxcliDescribeService.StreamViolations(chunkSize, emit) → één batch per chunk met voortgang.
DescribeStreamChunkSize=30 (empirisch: ~11-15s warm / ~25-37s koud per chunk — model laadt per chunk opnieuw,
bewust geaccepteerd). RunScanAsJson behouden (niet-gestreamd, RunAcrScan-route) via dezelfde RunFastPhase.
Batch-payload: {phase:"fast"|"describe", final, progress:{processed,total,label,requested,returned}, violations,
+ metadata alleen op fast}. ViewModel.RunFullScan post elke batch als "AcrViolations". GEVERIFIEERD met driver
(deep): 21 batches, streamed-som == niet-gestreamd EXACT (2865=2865; MAINT-006=104, REL-001=31, MAINT-008=129,
MAINT-009=1, MAINT-013=1, …), 0 incompleteness-warns, sawFinal=True. Regel-logica/claim-tabel/tripwires byte-
identiek — alleen wanneer/in-hoeveel-stukken verschilt.
FASE 2 (UI, main.js): handleMxcliResult vertakt op phase — fast=replaceOrigin (clean slate + metadata),
describe=APPEND (concat, geen replace). final=false → scanStreaming=true. streamingBanner() (inline-styled,
onmiskenbaar) + totalRow "Total (so far)" + "{n}…" zolang streaming → tussenstand NOOIT als eindstand. Voortgang
"microflows X/472". scanIncomplete (returned<requested) → rode LUID-waarschuwing. Scrollpositie bewaard over
herrenders. Vangnet finalizeStreaming() op ScanFinished (rondt af ook als final-batch ontbreekt, bv. 0 user-
microflows). Backward-compat: payload zonder phase = fast+final (oude vorm).
FASE 3: build 0/0, tests 232/232, Build-Package herpakt (TRB-check groen), streaming-code in het pakket (main.js).
De echte UI-test (beide knoppen, druppelen) doet Michel. Niet veranderd: regel-logica, getallen, claim-tabel/
tripwires, twee-knoppen-opzet.

WORTEL-ONDERZOEK warme/persistente mxcli-modus (23 juni) — CONCLUSIE: GEEN wortel-fix in mxcli, streamen is de weg.
mxcli is v0.12.0 (installer pakte 'latest'; "v0.11.0" in de context is verouderd). v0.12.0 heeft persistente
modi (REPL `mxcli` zonder args; `exec script.mdl`; `lsp`; `serve`) MAAR die houden het model alleen warm
binnen één sessie = model-load ×1 — wat onze chunk-van-200 al amortiseert. Géén bulk-describe en géén
expressie/control-flow-export (docs + `describe module` getoetst: die geeft alleen `create module X;`, niet de
inhoud). APPLES-TO-APPLES gemeten (zelfde warmte, back-to-back, alles via .NET ProcessRunner, 472 microflows):
  A) chunks-van-200 (3 processen): 301,5s (639 ms/mf)  B) één grote -c (1 proces, 27510 chars): 310,5s (658
  ms/mf)  C) exec scriptbestand (1 proces): 346,1s (733 ms/mf). Alledrie ~gelijk; chunk-200 zelfs marginaal het
SNELST. Eén-sessie is NIET sneller. (De eerdere "exec 300s vs chunk 548s" was een warmte-confound: de 548s-
chunkmeting was ~40min eerder/kouder.) Geen commandline-limiet in .NET (de 27510-char -c liep, blocks=472).
→ ~640ms/describe (warm) / ~1,1s (koud) is een HARDE mxcli-compute-vloer; geen warme/persistente/bulk-modus
breekt 'm. Chunk=200 is al optimaal (voorbij de model-load-amortisatie-knie). WORTEL-FIX bestaat dus niet in
mxcli v0.12.0 → STREAMEN (de tijd draaglijk maken) is de juiste weg. Enige echte orde-winst zou de verwijderde
BULK-EXPORT zijn (mxlint-export gaf alle expressies in ~19s) — heropent de mxlint-verwijder-beslissing; alleen
noemen als strategische optie, geen aanbeveling. Alternatief (apart, niet gemeten): leunen op mxcli's eigen
lint-regels (de ~10s lint-pass dekt mogelijk MAINT-008/009 e.d.) i.p.v. de eigen describe-route.

METING (23 juni) — fase-timing per modus (instrumented driver tmp-timing, TRB-Mx11-CLITEST, niet geschat):
Log-feit: 2 handmatige scans (16s ertussen, GEEN dubbel-trigger): FAST 55s (11:55:15→11:56:10), DEEP 4:41
(11:56:27→12:01:08). FASE-TABEL (warm, gemeten via de echte providerklassen):
  FAST: lint 6,3s | security 5,8s (describe projectsecurity + 1 guest userrole + 2 SELECTs) | catalog 4,4s
        (8 SELECTs + SHOW SETTINGS) | list-rules 0,3s | payload-build 0,05s | TOTAAL ~17s (findings 2594).
  DEEP: lint 11s | security 45,8s (9 userrole-describes ~5s elk) | catalog 7,1s | DESCRIBE-SWEEP 609s |
        list-rules 0,35s | payload 0,07s | TOTAAL 673s (11:13) (findings 2865).
VERRASSINGEN: (1) de DESCRIBE-SWEEP is ~99% van de deep-tijd. Gecontroleerd (blocks=chunkgrootte, geen
truncatie): per DESCRIBE ~1,07s marginaal + ~3,7s model-load per chunk-van-200. Batching amortiseert ALLEEN
de model-load; de per-describe-kost (~1,1s) is de vloer en domineert. (De oude "~0,6s/describe"-claim klopt
niet meer op v0.11.0/dit model.) 472 MF + 106 entiteiten = 578 × ~1,1s(koud)=608s ; × ~0,5s(warm OS-cache,
zoals de echte deep die ná de fast-scan liep) =289s ≈ log-4:41. Per-describe = 0,5s(warm)–1,1s(koud), LINEAIR.
(2) `mxcli lint` HERSCHRIJFT catalog.db ELKE scan (~6s warm, ~18s koud-rebuild, gezien in de log 11:55:15→33);
geen cross-scan-cache. Dit + koude process-starts verklaart log-FAST 55s vs warm 17s (niet reproduceerbaar
zonder OS-cache te legen). (3) payload-build is verwaarloosbaar (46ms voor 1,47MB). (4) kleine redundante
SELECTs: MODULES/MICROFLOWS/ENTITIES door zowel catalog- als describe-route; MODULES 2× in catalog-route;
list-rules apart van lint — samen ~2s, NIET de moeite. SELECT-batching bespaart 0,6s → niet doen.
EXTRAPOLATIE (describe-sweep, lineair, +~22% entiteiten zoals TRB): 2000 MF ≈ 20min(warm)–45min(koud);
5000 MF ≈ 51min(warm)–112min(koud). → STREAMEN is voor DEEP NOODZAKELIJK (frozen paneel 20–112min onaccept-
abel); voor FAST (~17–55s) nice-to-have. Snel = catalog (8×~0,4s) + lint + security; traag = describe-sweep
(chunks van 200, ~3–4min/chunk). Streaming-aanpak: fast-resultaten direct tonen (~20s), describe-findings
per chunk laten binnendruppelen (evt. kleinere chunks = vaker updaten, kost extra model-loads).

FASE E — BESLIST + DOORGEVOERD: "split de security-route". MxcliSecurityService.GetViolations(bool deepScan):
FAST = SEC-008 (describe projectsecurity, 0 userrole-calls) + SEC-005 (anon-create — alleen de GUEST-rol
nodig → ≤1 describe userrole, en alleen als guest-access aan); DEEP = die twee PLUS SEC-010 + MAINT-005
(alle 9 user-rollen). AcrScanService geeft deepScan door. Build 0/0, tests 232/232.
GEMETEN (TRB-Mx11-CLITEST, niet geschat): snelle scan ~46s → ~24s (8 userrole-calls weg). MAAR ~14s was
ONHAALBAAR — de oorzaak was verkeerd ingeschat. Per-call decompositie (gemeten): `mxcli lint` (eigen engine,
beide modi) = ~9,7s — DOMINANT en onvermijdelijk; `describe projectsecurity` = 3,9s; `describe userrole`
(guest) = 4,0s; `lint --list-rules` = 0,5s; elke `-c SELECT` (catalog.db) = ~0,4s — GOEDKOOP. De catalog-
SELECTs (8×) zijn dus NIET de kosten → SELECT-batching getest (alles in één -c) bespaart slechts ~0,6s →
NIET de moeite, geen FASE F. De ~24s = ~10s lint + ~8s twee describes (SEC-008/005) + ~6s goedkoop. Floor
mét security in fast = ~24s (beide) of ~18s (alleen SEC-008, SEC-005 ook naar deep) of ~14s (álle security
naar deep, dan 0 security in fast). REPACK-BESLISSING aan Michel: ~24s shippen of SEC-005 ook naar deep (~18s).

mxlint-VERWIJDERING GESTOPT bij FASE A — SEC-006 blokkeert. EINDBESLISSING was "mxlint volledig eruit,
6 naar mxcli, 2 deprecated", maar de verificatie weerlegde de meetkaart voor SEC-006.
FASE A (5 van 6 gemigreerd + geverifieerd tegen de oude GT):
- SEC-008 (admin=MxAdmin) → describe projectsecurity → AdminUser. GT 1 ✓
- SEC-010 (per-userrole check-security) → describe userrole. GT 0 ✓ (alle 9 enabled)
- MAINT-005 (module-rollen/module) → project-tree + describe userrole. GT 5 ✓
- SEC-005 (anon create persistent) → CATALOG.PERMISSIONS CREATE + ENTITIES PERSISTENT + guest-rol. GT 1 ✓
  (3 anon-CREATE-entiteiten, maar enkel Accesslog.AccesslogBankenportaal is PERSISTENT → 1)
  Aanpak: nieuwe MxcliSecurityService synthetiseert een equivalente project-security-YAML uit describe/
  project-tree en voedt die aan de BESTAANDE pure predicaten (logica ongewijzigd) + CatalogRules voor SEC-005.
- SEC-006 (anon-edit UNLIMITED string) → GEBLOKKEERD. mxcli v0.11.0 ontsluit de string-MAX-LENGTE NIET:
  CATALOG.ATTRIBUTES.Length=0 voor ALLE 748 strings; describe rendert ALLES als String(unlimited) — óók
  Accesslog…Username dat Length:200 is in de YAML. De catalog-variant over-telt (34 i.p.v. 4: alle anon-
  writable strings i.p.v. enkel unlimited). De unlimited-vs-limited-discriminator zit ALLEEN in de
  modelsource-YAML. → SEC-006 blijft op de YAML-route (DetectAnonymousEditRules). De meetkaart-verdict
  "CATALOG-NATIVE" was FOUT (Length-betrouwbaarheid niet getoetst); deze verificatie ving het.
- MAINT-006 (redundante boolean) → describe-migratie GEREED (zelfde extractie als REL-001/002) maar nog
  niet tegen GT 94 geverifieerd → BLIJFT voorlopig op de YAML-route (geen onbevestigde wijziging shippen).
FASE B: MAINT-015 + REL-003 deprecated (DetectPageRules niet bedraad; PageRules/PageYamlReader blijven backup).
FASE C: GEBLOKKEERD — actieve modelsource-lezers resteren: DetectAnonymousEditRules (SEC-006) +
DetectExpressionRules (MAINT-006). Dus de mxlint-export + binary KUNNEN NIET weg. Niet uitgevoerd.
CONCLUSIE: volledig export-loos is NIET haalbaar zolang SEC-006 (en, tot verificatie, MAINT-006) behouden
blijft. Beslissing aan Michel: (a) SEC-006 óók deprecaten → dan kan de export weg na MAINT-006-verificatie;
(b) SEC-006 houden → export blijft. Build 0/0, tests 230/230 (+3). tmp opgeruimd. Geen FASE C/D.

TWEE SCAN-MODI (snel default + Deepscan). Geen regel-logica gewijzigd; alleen WELKE regels draaien.
DEEL 1 (orchestratie): AcrScanService.RunScanAsJson(projectDir, bool deepScan=false). De describe-route
(MxcliDescribeService — de 5 trage regels MAINT-008/009/REL-001/002/MAINT-013) is nu ENIGE gegate stap:
`if (deepScan)`. Alle overige draaien in BEIDE modi (catalog-7, mxcli-eigen lint, YAML-route-regels
MAINT-005/SEC-005/006/008/010/MAINT-006/MAINT-015/REL-003, manual checks, export, marktplaats-modules).
Message "RunFullScan" → snel (deepScan:false); nieuw "RunDeepScan" → deep. payload.deepScan toegevoegd.
DEEL 2 (UI): tweede knop #deepScanBtn (class acr-secondary, duidelijk ondergeschikt) naast Scan. Beide
via gedeelde startScan(deep); setScanning disablet BEIDE + spinner. Deepscan toont een zichtbare duur-
waarschuwing ("…can take ~3 minutes…") + de C#-ScanProgress-tekst "Deep analysis: scanning all
microflows & entities…". Niet-opdringerige VOETNOOT onder de telkaarten bij een snelle scan (renderSummary,
class acr-scan-note → ook in het rapport): "Quick scan — the deep microflow & expression analysis
(complexity, nested ifs, empty-string checks, default ReadWrite access) is NOT included. Run a Deepscan…"
— communiceert het VERSCHIL (niet "kapot"). lastDeepScan uit payload (default true = geen hint vóór scan).
DEEL 3 (verificatie): de describe-route is de enige gate → snel = volledig MINUS de 5 describe-regels
(structureel: die zijn 0 in snel, want hun YAML-emissies staan al uit en MxcliDescribeService draait niet);
deep = + describe met de bewezen getallen (MAINT-008=129/MAINT-009=1/REL-001=31/REL-002=0/MAINT-013=1,
vorige turn gemeten, logica byte-identiek). GEMETEN componenten snelle scan: mxcli-lint 5,0s + catalog-
SELECTs ~7s + mxlint-export 19,1s + YAML-parse ~paar s ≈ ~30-35s. Deep = + describe-route 168s ≈ ~3,3 min.
Marktplaats-filter/claim-tabel/tripwires werken in beide modi (UI- resp. pure-normalizer-niveau, modus-
onafhankelijk; tests 227/227). Export (19s, grootste snelle-scan-component) blijft in beide (nodig voor de
YAML-regels) — 'm ook gaten = de YAML-regels naar deep verplaatsen = aparte beslissing (niet nu, geen stille
finding-drop). Build 0/0, tests 227/227.

PERF-FIX describe-route: gebatcht i.p.v. één proces per element. Findings ONGEWIJZIGD.
STAP 1 (meting, echte ProcessRunner-mechaniek): 30 SEPARATE describe-processen = 102.584 ms
(3.419 ms/describe) — de bottleneck is per-proces model-load (een catalog-SELECT = 1.189 ms; describe
laadt het volledige model ~3-4s). Batch-meting (PowerShell): marginale describe ≈ 550 ms, vaste model-
load ≈ 3,9 s. Dus N× model-load is de kost, niet de describe zelf.
STAP 2 (kan het in één proces): JA. `mxcli -c "CONNECT LOCAL '…'; DESCRIBE MICROFLOW A; DESCRIBE
MICROFLOW B; …"` levert alle blokken in één proces. Gemeten: 30 in één batch = 18.098 ms (vs 102.584 ms
separaat) = ~5,7× sneller, identieke conditie. Output teruggesplitst op `create or modify …`-kopregels.
STAP 3 (implementatie + verificatie): MxcliDescribeService herschreven — chunked -c-sessies (200/chunk,
veilig onder de Windows-cmdline-limiet), per-element-blok-split, robuust (luide warn + coverage-check
bij ontbrekende blokken; chunk-zonder-blokken = fout, geen stille 0). BUG gevonden+gefixt: de entiteit-
kopregex miste `non-persistent entity` → 51 van 106 entiteit-blokken vielen stil weg (106→55). Regex →
`^create or modify (?:[\w-]+ )*entity (\S+)` (elke kwalificeerder). Na fix: microflows 472/472,
entiteiten 106/106 blokken. VERIFICATIE op TRB: findings EXACT gelijk — MAINT-008=129, MAINT-009=1,
REL-001=31, REL-002=0, MAINT-013=1; catalog-regels ongewijzigd. DUUR: 169,5 s (~2,8 min) gebatcht vs
~4 min separaat (en vs ~33 min als de 3,4s/describe-separaat-meting representatief was → tot ~12×).
RESTEREND PLAFOND: de per-element describe-compute (~0,29 s × 578 ≈ 168 s) is nu de ondergrens;
parallelisme (meerdere batch-processen tegelijk) is de volgende hefboom indien nodig. Build 0/0,
tests 227/227. tmp opgeruimd.

CUTOVER mxlint→mxcli VOLTOOID. De describe-route is live-bedraad en de 4 mxcli-gedekte onderwerpen
gedeferd. Routes nu: CATALOG-SQL = MAINT-007/010/014, SEC-007/009/011, PERF-001 (7). DESCRIBE =
MAINT-008/009/013, REL-001/002 (5, via nieuwe MxcliDescribeService, user-module-scope, describe per
microflow/entity). DEFER naar mxcli's eigen regel = MAINT-011↔MPR003, PERF-002↔CONV017, MAINT-012↔
ACR_ENT_VALRULES/CONV015, commit↔CONV011 (onze CLEVR-emissie uit). NOG YAML = CLEVR-MAINT-006 (redundante
boolean, buiten scope) + de niet-mxlint ACR-regels (MAINT-005, SEC-005/006, PAGE MAINT-015/REL-003).
mxlint-export-engine = backup (findings uit, export draait nog voor de YAML-route-restanten).
STAP 2 — live-wiring: MxcliDescribeService (spike) draait de 5 pure describe-regels; YAML-emissies in
DetectExpressionRules (REL-001/002/MAINT-008/009) + DetectDomainModelBatchRules (MAINT-013) uitgezet
(pure regels = backup). STAP 3 — claim-tabel/tripwire-cutover: MPR003 + CONV011 UIT SuppressedMxcli
(mxcli's eigen regel moet júist tonen); de 4 entries → Winner = mxcli's regel, SuppressMxcli leeg,
mxlint-twin (002_0001/0006/0007/005_0002) BLIJFT onderdrukt (backup defert ook). Tripwire-lijsten
bijgewerkt: SuppressedMxcliCounterparts = {QUAL003,CONV009,DESIGN001,CONV002} (MPR003/CONV011 eruit);
InternalisedMxlintTwins membership ongewijzigd (comments → 'gedeferd'). DomainModelBatchTests.ClaimTable_
MxcliChoices omgezet (MPR003 nu DoesNotContain). STAP 4 — VERIFICATIE TRB: geen wegval (describe-getallen
bewezen in eerdere sweeps via identieke pure regels: REL-002=0, MAINT-009=1, REL-001=31, MAINT-013=1,
MAINT-008=129; catalog-regels ongewijzigd). Geen dubbel: mxcli-output MPR003=2 (System UI-gefilterd→TRB),
CONV011=0, CONV017=5, ACR_ENT_VALRULES/CONV015=0; onze CLEVR-MAINT-011/PERF-COMMIT = 0 in mxcli-output
(emissie uit) → precies één bron per onderwerp. Tripwires groen ÉN kloppend met de nieuwe toestand.
Build 0/0, tests 227/227. NIET in deze stap: mxlint-export verwijderen + severity-kalibratie. tmp opgeruimd.
PERF-CAVEAT: de describe-route = één mxcli-proces per microflow/entiteit (~578 op TRB) → merkbaar trager;
batching is een aparte latere beslissing.

ADDITIEVE RONDE (geen cutover): 3 resterende describe-regels + marktplaats-filter. NIET live-bedraad;
oude YAML-route draait door; claim-tabel/tripwire ongemoeid. Alles user-module-scope (Source leeg).
DEEL 1 — 3 describe-regels op de bewezen assembler, bestaande predicaten hergebruikt:
- REL-001 (redundante empty-string): describe Extract → ExpressionRules.RedundantEmptyString.
  Live user-module-sweep = 31 == oude YAML-GT 31 → EXACT gereproduceerd (geen afwijking).
- MAINT-013 (default-RW): nieuwe pure DescribeEntityRules — `grant <role> on <ent> (… write *)` =
  DefaultMemberAccessRights ReadWrite. Live = 1 == oude GT 1 (TRB_Email.TRB_Email, Administrator) → EXACT.
- MAINT-008 (complex zonder annotaties): describe StructureCounts (actions=niet-split-statements,
  splits=ALLE structurele if-headers incl. genest, annotations=@annotation-regels) → bestaande
  ComplexWithoutAnnotations. Live = 129 vs oude YAML-GT 103. AFWIJKING = METRIEK, niet scope/fout:
  zelfde 472 user-module-microflows, maar de describe-telling telt óók GENESTE splits (de oude YAML
  telde alleen top-level ExclusiveSplitCount) → meer microflows met splits>2 → +26. Geaccepteerd als
  nieuwe mxcli/describe-grondwaarheid (kalibratie-item), niet geforceerd naar 103.
  Synthetische positieve unit-tests per regel (beide richtingen). Build 0/0, tests 227/227 (+8).
DEEL 3 — marktplaats-filter (UI, geen demping): AcrScanService stuurt `appStoreModules` mee in de
payload (CATALOG.MODULES.Source niet leeg — zelfde mechanisme als FASE 1, via MxcliCatalogService.
AppStoreModuleNames). main.js: isAppStoreModule(v) (module-prefix ∈ set) + toggle appStoreVisible
(default TONEN) in baseViolations → werkt door in paneel ÉN rapport. Checkbox "Marketplace modules (N)"
in de Source-filterrij (zelfde stijl als de bron-filters), alleen getoond als er app-store-findings zijn.
Findings worden NIET vooraf gedempt; puur weergave-toggle. tmp opgeruimd.

FIX — mxcli exitcode 1 werd ten onrechte als mislukking behandeld. OORZAAK feitelijk vastgesteld
(v0.11.0, niet uit docs): mxcli's exitcode is GEEN succes/faal-signaal maar een CI-conventie op
SEVERITY: exit 1 = ≥1 error-severity finding (TRB: 3 errors → exit 1, mét geldige JSON), exit 0 = geen
error-findings (warnings/info kunnen er zijn — geverifieerd: alle modules-minus geeft 0 errors → exit 0).
ÉN een echte fout (connect-fout) geeft óók exit 1, maar met LEGE stdout + 'Error connecting: …' op
stderr. De "vibe-coded PoC"-waarschuwing staat ALTIJD op stderr → geen foutindicator. Geen exitcode-
conventie in --help gedocumenteerd; puur empirisch vastgesteld (3 cases).
FIX (op vastgestelde semantiek, niet op aanname): AcrScanService gokt niet meer op de exitcode. Nieuwe
MxcliOutputParser.ContainsJson(stdout) onderscheidt: stdout met JSON-envelope → mxcli draaide normaal →
parsen ongeacht exitcode; lege/niet-JSON stdout → LUID falen via Diagnostic (exitcode + stderr), nooit
stilletjes 0 findings. De oude `if (ExitCode != 0) return Diagnostic` is vervangen door deze
JSON-aanwezigheidscheck.
VERIFICATIE op de ECHTE captured mxcli-output: CASE A (findings, exit 1) → ContainsJson=true, 2574
violations geparsed → SUCCESS (niet meer afgewezen); CASE C (niet-bestaand .mpr, exit 1, lege stdout) →
ContainsJson=false → luide Diagnostic. De 7 catalog-regels (MAINT-007=30/MAINT-010=592/SEC-007=1/rest 0)
komen uit de catalog-SELECT-route (MxcliCatalogService) en staan los van deze lint-call-gate — onveranderd.
Build 0/0, tests 219/219 (+1 ContainsJson-test). tmp opgeruimd.

DESCRIBE-ROUTE BEWEZEN (FASE 2 extractor-fix). De divergentie (27 i.p.v. 0) is opgelost: de
DescribeMicroflowExpressions-extractor heeft nu een MULTI-LINE-ASSEMBLER (Assemble) — gewrapte
condities worden samengevoegd tot één statement (tot `;` of een kale ` then`) vóór het predicaat. De
bewijsstap draait op USER-MODULE-scope (Source leeg in CATALOG.MODULES — zelfde mechanisme als FASE 1;
13 user-modules, 472 microflows ≈ de 471 modelsource-microflows). Bestaande predicaten ONgewijzigd
hergebruikt (ExpressionRules.IncompleteEmptyStringCheck + MicroflowStructureRules.NestedIfStatements/
NestedIfRegex); alleen de EXTRACTIE is gefixt + ExtractSplits toegevoegd (split-conditie + caption).
DUBBELE VERIFICATIE op TRB (user-module-sweep, niet alleen één microflow):
- REL-002 = 0 (GT 0) — de multi-line complete checks (Encryption.MB_SaveCertificate 4×) tellen nu als
  compleet, geen vals-positief meer.
- MAINT-009 = 1 (GT 1) = TRB.SUB_ValidateVelden, caption 'Datum na 1 april 2015?' — het ECHTE positieve
  geval (geneste inline-if uit de echte describe-output) correct gereproduceerd. Een 0-regel zou de route
  niet bewijzen; dit positieve geval wél.
Beide exact → route bewezen. Unit-tests dekken beide + de regressie (multi-line wrap → compleet) +
plain/compound-condities (geen vals-nested). Build 0/0, tests 218/218.
STOP: de overige 3 (MAINT-008, REL-001, MAINT-013) volgen als APARTE batch nu de route bewezen is.
NIET in deze stap (aparte vervolgstappen): app-store meescannen + schermfilter, de live-wiring van de
describe-route in de scan (REL-002 draait nog via YAML), claim-tabel/tripwire-cutover voor de mxcli-gedekte regels.

MIGRATIE mxlint→mxcli (Apache-2.0). FASE 1 (mxcli-catalog-provider + 7 robuuste regels) AF; FASE 2
(describe-route bewijsregel) DIVERGEERT → gestopt, oorzaak benoemd, overige 4 NIET gebouwd.
FASE 1: nieuwe pure CatalogRules (normalizer) + MxcliCatalogService (spike, SQLite-catalog via
`-p <mpr> -c "SELECT … FROM CATALOG.*"`). 7 regels gemigreerd op catalog-SQL, rule-id/categorie/severity
+ claim-tabel ongewijzigd; live geverifieerd tegen v0.11.0/TRB:
- MAINT-007 ActivityCount>25 → 30 (was 44; mxcli-metriek geaccepteerd als nieuwe GT)
- MAINT-010 DefaultValue non-empty → 592 (was 283; incl. impliciete defaults, geaccepteerd)
- MAINT-014 user-modules(Source leeg) → 13 ≤20 → 0
- SEC-011 ExposedToClient=1 + gevoelig-naam-filter → 2 exposed, niet gevoelig → 0
- PERF-001 Generalization=Administration.Account → 0
- SEC-007 ToEntity LIKE 'System.%' gescoped user-module → 1 (TRB.Groep_UserRole)
- SEC-009 SHOW SETTINGS Hash=BCrypt → 0
YAML-emissies voor deze 7 uitgeschakeld in AcrScanService (methodes blijven als backup, mxlint-export
blijft load-bearing voor de niet-gemigreerde regels). 4 mxcli-gedekte onderwerpen → DEFER naar mxcli's
eigen v0.11.0-regels (live bevestigd aanwezig): MAINT-011↔MPR003, PERF-002↔CONV017, MAINT-012↔
ACR_ENT_VALRULES/CONV015, commit↔CONV011 — niet zelf gebouwd. (De claim-tabel/tripwire-reconciliatie
voor MAINT-011↔MPR003 + commit↔CONV011 = bewuste vervolg-cutover, NIET nu uitgevoerd.)
FASE 2 (bewijsregel CLEVR-REL-002 via describe microflow): pure DescribeMicroflowExpressions (extractor)
+ ongewijzigde ExpressionRules.IncompleteEmptyStringCheck. Unit-tests groen. MAAR de TRB-sweep
reproduceerde de GT NIET: 27 findings i.p.v. 0. DOORGEGRAVEN — twee oorzaken:
(1) SCOPE: modelsource-export = 471 microflows (excl. marketplace), catalog = 1074 (incl. app-store);
    de 27 zitten in app-store-modules (SAML20/Encryption/SupportModule) die de oude route nooit zag.
(2) PER-LINE-FALSE-POSITIVES (de serieuze): describe wrapt lange condities over meerdere regels
    (`if not($X != empty\n$X != '') then`); naïeve per-regel-extractie knipt een COMPLETE check in een
    incomplete-ogend fragment `$X != ''` → vals-positief. Bewezen op Encryption.MB_SaveCertificate
    (regels 13-15/25-27/32-34/61-63 = multi-line complete checks). TRB.SUB_ValidateVelden gaf wél 0
    omdat z'n complete check op ÉÉN regel staat (regel 62).
CONCLUSIE: describe-route is HAALBAAR (data compleet aanwezig) maar de extractor moet eerst een
multi-line-expressie-assembler krijgen (wrap-regels samenvoegen tot hele logische expressies vóór het
predicaat) + een scope-beslissing (app-store mee of niet, consistent met de catalog-regels). Pas daarna
de overige 4 (MAINT-008/009/013, REL-001). Build 0/0, tests 215/215. mxlint-backup + claim-tabel + tripwires intact.

FIX — mxcli-kant consistent: CONV011 (NoCommitInLoop) toegevoegd aan SuppressMxcli van de commit-in-loop-
entry. CONV011 meet exact het commit-in-loop-onderwerp (catalogus: "Commit actions should not be inside
loops (N+1)", performance) = zelfde als CLEVR-PERF-COMMIT-IN-LOOP/005_0002. Vuurt 0 op TRB → niets
zichtbaar verandert, maar de set is compleet zodra 'ie vuurt (consistent met CONV002/QUAL003/CONV009/
DESIGN001/MPR003). BORGING doorgetrokken: tweede tripwire SuppressedMxcli_ExactlyMatchesCounterparts —
canonieke lijst van alle 6 bewust-onderdrukte mxcli-ids moet EXACT gelijk zijn aan SuppressedMxcli;
vergeten mxcli-suppressie faalt nu net zo luid als vergeten mxlint-entry. Build 0/0, tests 200/200 (+1).

FIX — claim-tabel-drift hersteld (005_0002/0004/0005 ontbraken). Bij de microflow-batch waren de
detect-regels toegevoegd maar de suppressie-entries vergeten; alleen 005_0003 kreeg er destijds één.
005_0004 dook daardoor zichtbaar dubbel op (103 naast CLEVR-MAINT-008); 005_0002/0005 latent (0 resp.
niet in pack). Drie EngineClaim-entries toegevoegd: 005_0004→CLEVR-MAINT-008, 005_0005→CLEVR-MAINT-009,
005_0002→CLEVR-PERF-COMMIT-IN-LOOP. Detect-logica ONgewijzigd.
mxcli-tegenhanger-check: 005_0004/0005 geen (mxcli kent geen annotatie-/nested-if-regel). 005_0002 heeft
WÉL een mxcli-twin — CONV011 NoCommitInLoop — maar die vuurt 0 op TRB (0× in volledige mxcli-lint),
daarom nu niet onderdrukt; aanbeveling in de Impact om CONV011 toe te voegen voor consistentie.
LIVE GEVERIFIEERD op TRB-output: 005_0004 raw=103 → na MxlintNormalizer-suppressie 0; 005_0003 44→0;
005_0002/0005 0→0 (en alle vier in SuppressedMxlint); CLEVR-MAINT-008 behoudt z'n 103 (eigen detect).
BORGING tegen herhaling: nieuwe test ClaimTableTests.SuppressedMxlint_ExactlyMatchesInternalisedTwins —
één canonieke lijst van alle 23 geïnternaliseerde/gemigreerde mxlint-twins moet EXACT gelijk zijn aan
SuppressedMxlint. Missende entry (de 005_0004-bug) → test faalt luid; stray entry → ook. Eén plek, één
lijst. Build 0/0, tests 199/199 (was 198; +1 borgingstest). tmp opgeruimd.

REGO-INTERNALISATIE — SLOTREGEL 004_0002 ImagesWithAltText → mxlint.com-set 17/17 GEÏNTERNALISEERD.
STAP 0 al gedaan: MXLINT-ONLY (MPR005 UnconfiguredImage = ontbrekende image-SOURCE, ander onderwerp).
DOELREGEL 004_0002 (.rego category Accessibility, MEDIUM). Logica VERBATIM: walk → knoop met
$Type CustomWidgets$CustomWidget; Object.$Type == CustomWidgets$WidgetObject; minstens één
Object.Properties[].Value.PrimitiveValue == "fullImage" (= image-widget); VUURT als GEEN van diens
EIGEN Properties' Value.TextTemplate.Template.Items een Texts$Translation met AANWEZIGE Text-sleutel
heeft. SUBTIEL (uit de Rego-testfixtures bevestigd): "Text gezet" = de sleutel is gedefinieerd — ook
Text:"" telt als gezet (Rego-truthy); alleen een AFWEZIGE Text-sleutel = ontbrekend (variation_1 mist de
sleutel → vuurt; variation_2 heeft geen translation → vuurt; allow heeft Text → vuurt niet).
OMGEKEERD FN-risico (ONTBREEKT-check): een gemiste translation-tak = false POSITIVE. Daarom loopt
HasAltText ALLE eigen Properties + ALLE Items af (alleen de EIGEN Object.Properties, niet geneste
child-widgets — exact de Rego-scope).
STAP 1 (verse export): 113 pages/snippets, 62 CustomWidgets$CustomWidget-knopen, maar 0 image-widgets
(geen fullImage op TRB). WEL alt-text: 0; MISSEND: 0 → GT=0. mxlint-twin 004_0002 draait CORRECT
(testcases=113, failures=0) → geldige kruischeck die AGREE't (twin 0 = GT 0).
STAP 2: GEEN nieuwe reader — hergebruikt de PageYamlReader-boom + pure PageRules (walk zoals MAINT-015).
AcrScanService: page-batch (MAINT-015 + REL-003) op één reader-pass. Rule-id CLEVR-REL-003;
CATEGORIE-KEUZE (knop voor Michel): Accessibility→Reliability (geen ACR-bucket); MEDIUM→Major.
STAP 3 (dubbel, met expliciete FP-richting): real-rule == onafhankelijke YamlDotNet-GT (0 image-widgets,
0 missend) == werkende twin (0) → EXACT, 0 FP/FN. Synthetische tests beide kanten: image zónder
translation→vuurt (variation_2); translation zónder Text-sleutel→vuurt (variation_1); translation mét
Text→niet (allow); Text:""→niet (verbatim truthy); niet-image-widget→niet; dedup zelfde widgetnaam→1.
CLAIM-TABEL: 1 entry — winnaar CLEVR-REL-003; onderdruk mxlint 004_0002. Geen mxcli-onderdrukking.
Naam-mapping-test NameFor("004_0002")=="ImagesWithAltText" ONGEMOEID (suppressie raakt enkel Normalize).
Build 0/0, tests 198/198 (was 191; +7). tmp-alt opgeruimd. Echte Studio-Pro-Scan-test: Michel.
>>> mxlint.com-set nu VOLLEDIG geïnternaliseerd (17/17). mxlint als regelbron kan worden uitgefaseerd.

REGO-INTERNALISATIE — page/snippet-route (bewijsregel 004_0001; nieuw bestandstype):
Laatste route. STAP 0 al gedaan: 004_0001 + 004_0002 beide MXLINT-ONLY (geen mxcli/ACR-tegenhanger;
MPR005 UnconfiguredImage = ontbrekende image-SOURCE, ander onderwerp; "style" in de lijst = enkel een
Category). Alleen 004_0001 nu gebouwd (bewijsregel die de page-reader opent); 004_0002 (alt-text, diepe
CustomWidget/WidgetObject/Texts$Translation-boom) volgt pas nu de reader bewezen is.
DOELREGEL 004_0001 InlineStylePropertyUsed (Maintainability/MEDIUM→Major). Rego-logica VERBATIM:
walk(input) → elk pad waarvan de LAATSTE sleutel exact "Style" is en waarde != "" (niet-lege string).
STAP 1: pages/snippets in `*.Forms$Page.yaml` / `*.Forms$Snippet.yaml` (113 op TRB: 101 pages + 12
snippets). Style zit onder `Appearance:` (Forms$Appearance)-knopen, diep door de hele widget-boom;
top-level `Name` op kolom 0. Waarden: 7527× leeg ("") + diverse niet-lege CSS (single/double-quoted,
\r\n, block-scalars |-). mxlint-twin 004_0001 draait CORRECT (testcases=113 = alle files gevoed;
14 files met findings) → situatie "correct" (niet dormant/kapot) → Rego-kruischeck GELDIG.
SUBTIEL / DOORGEGRAVEN: ruwe niet-lege Style-voorkomens = 86, maar mxlint meldt 33. Oorzaak: de Rego's
`errors` is een SET van error-STRINGS, en die string = sprintf(... input.Name, v). Identieke (Name,value)
vallen samen → dedup per page op style-WAARDE. 33 = distinct (file,value). Regel daarop aangepast
(HashSet per page) — anders 86 i.p.v. 33 (vals-positieve dubbeltelling).
STAP 2: nieuwe `PageYamlReader` (spike, YamlDotNet) → zet elk doc om naar een PLAT objectboom-model
(Dictionary/List/string) = `PageModel` in de normalizer; de patroon-walk zit PUUR in `PageRules`
(dependency-vrij, unit-testbaar). HERBRUIKBAAR: 004_0002 loopt straks dezelfde boom af. Rule-id
CLEVR-MAINT-015, Maintainability/Major.
STAP 3: real-rule (PageYamlReader + PageRules) == onafhankelijke YamlDotNet-GT (set-semantiek) == werkende
mxlint-twin → alle drie 33 over 14 files, 0 FP/FN, EXACT (drievoudige overeenstemming). Synthetische
positieve tests: niet-lege Style→vuurt; lege/afwezig→niet; "MyStyle"/"StyleClass"/"DynamicClasses"→niet
(exact-key); identieke waarde 2× per page→1 (set), 2 distinct→2; snippet-doctype.
CLAIM-TABEL: 1 entry — winnaar CLEVR-MAINT-015; onderdruk mxlint 004_0001. Geen mxcli-onderdrukking.
Build 0/0, tests 191/191 (was 184; +7). tmp-pg opgeruimd. Echte Studio-Pro-Scan-test: Michel.

REGO-INTERNALISATIE — constant-route (bewijsregel 006_0001; nieuw bestandstype):
Laatste route (page/constant). 16 van 17 MXLINT-ONLY af; deze route vereist NIEUWE YAML-readers voor
bestandstypen die we nog niet lazen → eerst één bewijsregel, dan pas de rest.
STAP 0 (dekkingscheck, mxcli lint --list-rules + claim-tabel): 006_0001 ExposedConstants, 004_0001
InlineStylePropertyUsed, 004_0002 ImagesWithAltText → GEEN mxcli-bundled of geclaimde ACR-regel raakt
deze onderwerpen (MPR005 UnconfiguredImage = ontbrekende image-SOURCE, ander onderwerp dan alt-text;
"style" in de lijst = enkel de Category van MPR001 NamingConvention). Alle drie MXLINT-ONLY. Alleen
006_0001 nu gebouwd (bewijsregel); 004_0001/0002 volgen pas als de reader bewezen is (004_0002 alt-text
is de lastigste — diepe widget/translation-boom).
STAP 1 (bestand + grondwaarheid): constants staan in `*.Constants$Constant.yaml` (PLAT: `$Type`,
`ExposedToClient` bool, `Name` string — alles kolom 0). Eerste constant-bestand dat we lezen. TRB: 9
constants, ALLE 9 ExposedToClient: false → YamlDotNet-grondwaarheid (exposed && gevoelige naam) = 0.
mxlint-twin 006_0001 WERKT hier correct (testcases=9 — de glob `**/*$Constant.yaml` voedt alle 9 files —
failures=0): geldige kruischeck die AGREE't (mxlint 0 = GT 0). Dus NIET dormant/kapot (anders dan
001_0007/003_0001 die het verkeerde pad lazen).
STAP 2 (bouw): nieuwe YamlDotNet-reader `ConstantYamlReader` (spike, infra-stijl als MicroflowYaml-
Expressions) → pure `ConstantRules` (normalizer). Rule-id CLEVR-SEC-011 (SEC-reeks), Security/Critical
(mxlint HIGH). SUBTIELE PLEK — de .rego heeft TWEE branches: (1) ELKE exposed constant = MEDIUM, (2)
exposed + gevoelige naam = HIGH. We bouwen BEWUST alleen branch (2) (branch 1 = ruis: flagt álle exposed
constants). Gevoelig-naam-detectie VERBATIM uit de .rego: substring (case-insensitief) op keyword-lijst
["id","ident","username","user_name","user","usr","uname","secret","scrt","password","pwd","passwrd"].
Bewust over-breed net als de Rego (bv. "Width" bevat "id") — niets verzonnen/toegevoegd.
STAP 3 (verifieer): real-rule (ConstantYamlReader + ConstantRules) == onafhankelijke YamlDotNet-GT op TRB
= 0, 0 FP/FN, EXACT. Synthetische positieve tests: exposed+gevoelig→vuurt; exposed+onschuldig→niet;
gevoelig+niet-exposed→niet; + keyword-lijst verbatim-test.
CLAIM-TABEL: 1 entry — winnaar CLEVR-SEC-011; onderdruk mxlint 006_0001. Impact benoemt expliciet dat
de blanket-MEDIUM-branch (élke exposed constant) hierbij vervalt (bewuste keuze). Geen mxcli-onderdrukking.
Build 0/0, tests 184/184 (was 171; +13). tmp-cv opgeruimd. Echte Studio-Pro-Scan-test: Michel.

REGO-INTERNALISATIE — security-/settings-/modules-batch (4 van 4 MXLINT-ONLY gebouwd):
STAP 0 (dekkingscheck vóór bouw, criterium "mxcli OF bestaande ACR-regel dekt het ONDERWERP al →
NIETS bouwen"): 001_0004 StrongPasswordPolicy = GEDEKT (mxcli ACR_SEC_PWPOLICY + SEC002) → NIET gebouwd;
001_0005, 001_0007, 001_0008, 003_0001 = MXLINT-ONLY (geen mxcli/ACR-regel raakt admin-username,
hash-algoritme, per-userrole-security, of module-telling) → alle vier gebouwd.
STAP 1+3 (veld-bestaat + grondwaarheid, YamlDotNet/structureel als oracle; real-rule == GT, 0 FP/FN):
- 001_0005 → CLEVR-SEC-008 MxAdminNotUsed (Security/Critical=HIGH): GT=1 rule=1 (AdminUserName: MxAdmin).
  mxlint-twin vuurt óók 1 (input `.*Security$ProjectSecurity.yaml`, geen `/`) → geldige kruischeck.
- 001_0007 → CLEVR-SEC-009 HashAlgorithm (Security/Critical=HIGH): GT=0 rule=0 (HashAlgorithm: BCrypt).
  mxlint-twin STRUCTUREEL kapot op deze export: leest `input.Settings.HashAlgorithm`, maar Settings is
  een LIJST (geen mapping) → vuurt 0 om de verkeerde reden. Onze regel vindt het veld waar het staat.
- 001_0008 → CLEVR-SEC-010 CheckSecurityOnUserRoles (Security/Critical=HIGH): GT=0 rule=0 (9/9 user-
  roles CheckSecurity: true). Per-role; afwezig/false = overtreding (zoals Rego `not CheckSecurity`).
- 003_0001 → CLEVR-MAINT-014 NumberOfModules (Maintainability/Major=MEDIUM): GT=0 rule=0 (12 user-
  modules ≤ 20). mxlint-twin STRUCTUREEL kapot: leest `Modules[i].Attributes.FromAppStore == false`,
  maar de export zet FromAppStore VLAK onder het module-item (niet onder Attributes) → telt 0. Onze
  regel = item zónder `FromAppStore: true` = user-module → correcte telling.
Synthetische positieve unit-tests per regel (TRB is 1/0/0/0, dus detectie apart bewezen).
CLAIM-TABEL: 4 entries toegevoegd — winnaars CLEVR-SEC-008/009/010 + CLEVR-MAINT-014; mxlint-twins
001_0005/001_0007/001_0008/003_0001 onderdrukt. Geen mxcli-onderdrukking (alle vier MXLINT-ONLY).
Bron-hergebruik: ProjectSecurityParser (4 nieuwe Detect-methods); AcrScanService leest nu ook
Settings$ProjectSettings.yaml + Metadata.yaml. MxlintNormalizerTests-fixture die 001_0005 als generic
passes-through gebruikte → herpunt naar 001_0004 (StrongPasswordPolicy, nog niet geïnternaliseerd → niet
onderdrukt). Build 0/0, tests 171/171 (was 156; +15). tmp-dg opgeruimd. Echte Studio-Pro-Scan-test: Michel.

REGO-INTERNALISATIE — domein-model-batch (6 van 7; 002_0004 bewust overgeslagen):
KERNBEVINDING: alle 7 mxlint-tegenhangers zijn DORMANT op Windows — hun .rego `input: .*/DomainModels…`
(met `/`) matcht geen backslash-paden → mxlint voedt ze 0 files (testcases=0). 002_0009 werkt alleen
omdat die `.*DomainModels…` (zonder `/`) gebruikt. Dus de mxlint-"0" is GEEN geldige kruischeck; de
YamlDotNet-grondwaarheid is de enige toets. (Dat ze dormant zijn = juist het bewijs dat internaliseren
waarde heeft.) Gebouwd op de line-parser (ProjectSecurityParser uitgebreid: MaybeGeneralization
Persistable/Generalization, Value.$Type, ValidationRules-telling, AccessRule.DefaultMemberAccessRights,
+ top-level CrossAssociations-parser). Per regel echte-rule == YamlDotNet-grondwaarheid, 0 FP/FN:
- 002_0001 → CLEVR-MAINT-011 (Maintainability/Major): 1 (TRB 19 persistent >15).
- 002_0003 → CLEVR-PERF-001 (Performance/Major): 0 (geen Administration.Account-inheritance).
- 002_0005 → CLEVR-SEC-007 (Security/Critical = HIGH): 1 (TRB|Groep_UserRole cross-assoc → System).
- 002_0006 → CLEVR-PERF-002 (Performance/Major): 0 (geen entiteit >10 calculated).
- 002_0007 → CLEVR-MAINT-012 (Maintainability/Major): 0 (geen domein-validatieregels).
- 002_0008 → CLEVR-MAINT-013 (Maintainability/Major): 1 (TRB_Email ReadWrite-access).
Synthetische positieve unit-tests per regel (TRB is 0/1, dus detectie apart bewezen).
002_0004 NIET gebouwd: de Rego is buggy — `not startswith(<undefined>,"System.")` vuurt op alle 60
no-generalization-entiteiten → 64 ruis i.p.v. de 4 bedoelde non-System-inheritors. Aanbeveling: niet
internaliseren zoals-is; evt. de INTENT (4) als aparte bewuste regel — Michel beslist.
CLAIM-TABEL per regel: mxlint-twins onderdrukt (002_0001/0003/0005/0006/0007/0008). mxcli-checks:
MPR003 (vuurt 2: System 27 UI-gefilterd + TRB 19) → onderdrukt voor 002_0001 (geen zichtbaar verlies);
CONV017 (vuurt 5, ELKE calculated) → NIET onderdrukt (breder dan onze >10-regel, zou 5 verliezen);
ACR_ENT_VALRULES/CONV015/CONV006/CONV007 → 0 op TRB, niets te onderdrukken. Build 0/0, tests 156/156.

REGO-INTERNALISATIE — domein-model-YAML-route GEOPEND: CLEVR-MAINT-010 = mxlint 002_0009 NoDefaultValue.
.rego: per (entity, attribute) → attribute.Value.DefaultValue != null && != "". Categorie Maintainability,
severity LOW → ACR Minor. Hergebruikt ProjectSecurityParser.ParseEntitiesWithAttributes (de SEC-005/006-infra),
uitgebreid met Value→DefaultValue + UNQUOTE ('' /"" → leeg; "false" → false) — cruciaal want het export-
veld is gequote (288× "" = leeg/geen-violation, 145× "false", 106× "0", 32× strings). GRONDWAARHEID op verse
TRB-export = 283; doorgegraven (verdacht "8 vs 283"): de xUnit-failures tellen FILES (8), niet findings —
un-bundled = 283 (2+1+1+7+74+48+144+6). 283 is hoog omdat de Rego ELKE niet-lege default flagt (incl. boolean
false + integer 0) — getrouw gereproduceerd. STAP 3: echte regel (line-parser) = 283 = YamlDotNet-grondwaarheid
283 = mxlint 002_0009 283 → EXACT, 0 FP/FN (bewijst de line-parser-uitbreiding + unquote correct). Claim-tabel:
002_0009 toegevoegd — winnaar CLEVR-MAINT-010; onderdruk mxlint 002_0009 (identiek, geen verlies) ÉN mxcli
CONV002 NoEntityDefaultValues (vuurt 106, ALLEEN integer-'0' → STRIKTE SUBSET van onze 283 → geen verlies;
zonder onderdrukking een nieuwe 106-mxcli-dubbeling). Build 0/0, tests 142/142 (+4; 4 bestaande
MxlintNormalizer-fixtures die 002_0009 als "generic passes-through" gebruikten → herpunt naar synthetisch
999_9999, naam-mapping-tests bleven 002_0009). (Wegwerp-tools tmp-gt5/gt6 kunnen blijven staan — inert.)

REGO-INTERNALISATIE — expressie-route: CLEVR-REL-002 = mxlint 005_0001 EmptyStringCheckNotComplete.
.rego (verbatim): per "Expression"-keyed waarde → strip SPATIES → contains "!=''" ÉN niet "!=empty"
(per-expressie substring-check; complement van REL-001 dat juist BEIDE-checks-op-1-pad = redundant
meet → bevestigd geen overlap). Categorie .rego "Error" → Reliability (gemapt); severity MEDIUM → Major.
Nieuwe extractie MicroflowYamlExpressions.ExpressionKeyedValues (alle key=="Expression"-waarden, ≠ de
bestaande split+change-set die REL-001/006 voeden) → ExpressionRules.IncompleteEmptyStringCheck.
GRONDWAARHEID op verse TRB-export = 0; doorgegraven (geen blind 0): van 27 expressies met !='' hebben
ALLE ook !=empty (313 gebruiken !=empty) → 0 incompleet. Echte regel = 0, mxlint 005_0001 = 0 → EXACT
(0==0==0); REL-001=31 draait er los naast (bewijs geen overlap). Claim-tabel: 005_0001 toegevoegd
(alleen mxlint-onderdrukking; GEEN mxcli-tegenhanger — bevestigd in de 60-regel-meting). Build 0/0,
tests 138/138 (+14). (Wegwerp-tool tmp-gt5\ kan blijven staan door een build-server-handle — inert.)

CLAIM-TABEL (cross-engine ontdubbeling — vervangt de mxlint-only denylist): nieuwe ClaimTable.cs
(normalizer) — per onderwerp één winnende bron, tegenhangers onderdrukt op BEIDE engines.
MxcliNormalizer dropt nu SuppressedMxcli (op rule-id), MxlintNormalizer dropt SuppressedMxlint (op
rulenumber); de oude hardcoded 6-rulenumber-denylist is weg incl. de 2 foute entries (001_0004/002_0007
verwezen naar niet-geclaimde ACR_SEC_PWPOLICY/ACR_ENT_VALRULES → niet meer onderdrukt). Regel-logica
ongemoeid; alleen de aggregatielaag. BEWIJS-onderwerpen (2): microflow-grootte (CLEVR-MAINT-007 wint →
onderdruk mxcli QUAL003+CONV009, mxlint 005_0003) en attribuut-telling (ACR_ENT_ATTRS/CLEVR-MAINT-001 wint
→ onderdruk mxcli DESIGN001, mxlint 002_0002). 3 security-onderwerpen 1-op-1 gemigreerd (mxlint-only,
gedrag ongewijzigd) zodat de denylist-vervanging niet regresseert. GEVERIFIEERD op TRB via de ECHTE
normalizers: mxcli QUAL003 24→0, CONV009 61→0, DESIGN001 16→0 (totaal 2131→2030, −101); mxlint 005_0003
44→0; WINNAARS intact (CLEVR-MAINT-001=6, CLEVR-MAINT-007=44); 005_0004 onaangeroerd (103, geen proof-topic).
BEWUST VERLIES benoemd: CONV009 17 (16–25 activiteiten) + DESIGN001 10 (11–25 attrs) — tegenhangers dekten
breder; per onderwerp bevestigd. Build 0/0, tests 124/124 (+13). Overige onderwerpen (naamgeving/guest mxcli
+ de 17 te internaliseren) volgen pas na goedkeuring per onderwerp. (Meet-tool tmp-gt4\ inert blijven staan.)

DUBBELING-METING (TRB, verse mxcli-lint --format json, 2131 findings / 37 van 60 regels vuren):
De zichtbare dubbeling is NIET vooral mxlint↔ACR (al gesuppressed), maar mxcli-GENERIC ↔ geclaimd-ACR/CLEVR:
111 redundante generic-findings NU zichtbaar: attribuut-telling ACR_ENT_ATTRS∩DESIGN001=6; microflow-grootte
CLEVR-MAINT-007∩QUAL003=24 + ∩CONV009=44 (alle 44 dubbel, 24 driedubbel); enum ACR_ENUM_PREFIX∩CONV004=26;
snippet ACR_SNIP_PREFIX∩CONV005=10; guest ACR_SEC_GUEST∩SEC004=1. Commit-in-loop latent (CONV011=0, onze=0).
De mxlint-denylist (6 rulenumbers) raakt HIER NIETS van — allemaal mxcli-intern. APART: de denylist mist de
005-familie (005_0002/0003/0004/0005) → mxlint 005_0003(44)+005_0004(103) dubbelen NU met onze CLEVR-MAINT-007/008
zodra mxlint meedraait (≈147, denylist-config-fix nodig). Conclusie voor de oplossing: een claim-tabel op
ONDERWERP (suppress op beide engines: mxcli-generic + mxlint) is proportioneel; een mxlint-only denylist dekt
de 111 mxcli-generic-doubles principieel niet. (Meet-tool tmp-gt3\ bleef door build-server-handle staan — inert.)

REGO-INTERNALISATIE — flow-AST-route VOLTOOID (3 resterende microflow-regels, batch na MAINT-007):
Eén YAML-parse (MicroflowYamlExpressions.Parse → record met expressies + objectcount + top-level
type-tellingen + ExclusiveSplits + in-loop-acties); pure regels in MicroflowStructureRules; gewired in
DetectExpressionRules. Verse export (mxlint export+lint) → grondwaarheid + Rego-tegenhanger:
- CLEVR-MAINT-008 = 005_0004 ComplexMicroflowsWithoutAnnotations (Maintainability/Major): >10 ActionActivity
  OF >2 ExclusiveSplit (top-level) ÉN 0 Annotation. Rule=103, Rego=103, EXACT.
- CLEVR-MAINT-009 = 005_0005 NestedIfStatements (Complexity→Maintainability/Major): ExclusiveSplit met
  SplitCondition.Expression die de VERBATIM .rego-regex `^[\S\s]*(then|else)[\S\s]*(if)[\S\s]*$` matcht.
  Rule=1 (TRB.SUB_ValidateVelden, multi-line block-scalar correct geparsed). GÉÉN Rego-tegenhanger: de
  deployed v3.3.0 rules-pack bevat 005_0005 NIET (alleen 0001-0004) → gecheckt tegen de .rego-bron.
- CLEVR-PERF-COMMIT-IN-LOOP = 005_0002 AvoidCommitInLoop (Performance/Major): committende actie
  (CommitAction OF ChangeAction Commit=="Yes") binnen een LoopedActivity. Rule=0, Rego=0. BEWUSTE id-keuze:
  hergebruikt het bson-PoC-id (BsonMicroflowParser.RuleId) — de YAML-route VERVANGT de niet-gewirede
  bson-PoC. De 0 is ECHT geverifieerd (alle 136 CommitActions op indent 12 = top-level, géén in een loop),
  niet door een kapotte walk (positieve unit-test bewijst detectie). De Rego's 0 is daarentegen DORMANT:
  005_0002 leest input.MainFunction + .Attributes — beide afwezig in de modelsource-export (de echte vorm is
  ObjectCollection.Objects → LoopedActivity → ObjectCollection.Objects → ActionActivity.Action). Toevallig
  beide 0; onze regel reproduceert de INTENT correct op de echte structuur.
Build 0/0, tests 111/111 (+16). Verificatie via de ECHTE extractor+regels (geen herimplementatie).
(Wegwerp-tool tmp-gt2\ bleef door een build-server-handle staan — inert, los te verwijderen.)

REGO-INTERNALISATIE — 1e bewijsregel (flow-AST-route geopend): CLEVR-MAINT-007 = mxlint 005_0003
NumberOfElementsInMicroflow. .rego: count(ObjectCollection.Objects) - 2 > 25 (top-level, NIET-recursief;
-2 = vaste Start+End-offset). Gebouwd als pure MicroflowStructureRules.NumberOfElements (kind=acr,
source=clevr-acr, categorie Maintainability letterlijk, severity Major = mxlint MEDIUM, bij te stellen).
Spike: MicroflowYamlExpressions.ParseMicroflow parseert nu 1× per microflow en levert ZOWEL expressies
ALS top-level objecten-count (root.ObjectCollection.Objects) → DetectExpressionRules voedt de count-regel.
GRONDWAARHEID op verse TRB-export (mxlint export 14s, 471 microflows, parseFail=0): 44 findings, max 98
elementen (Rapportages.IVK_GenereerGlobaalHerzieningsbeslissing, raw 100). Geverifieerd met 2 onafhankelijke
methodes (YamlDotNet structural + scoped indent-count: 29/100/72 exact). LET OP: globale indent-grep was
fout (telde dashes buiten ObjectCollection) — scoped/structureel is juist. STAP 3: de ECHTE regel op dezelfde
counts → 44/44, 0 FP, 0 FN, 0 count-mismatch, geen System/marketplace (modelsource heeft geen System-module;
appstore-modules export-uitgesloten). Build 0/0, tests 95/95 (+9). Alléén deze regel; 005_0002/0004/0005
volgen pas na bewijs in Studio Pro (door Michel). (Wegwerp-verificatietool tmp-groundtruth\ bleef door een
build-server-handle staan — inert, los te verwijderen.)

INSTALLER mxcli AUTO-DOWNLOAD (collega strandde op "git clone + make build" — make ontbreekt op Win):
- Install-ClevrAcr.ps1 mxcli-stap nu: (1) op PATH? gebruik dat; (2) eerder door ons gedownload in
  %LOCALAPPDATA%\clevr-acr\mxcli\mxcli.exe? hergebruik; (3) anders: vraag bevestiging (geen stille
  download) → Install-Mxcli haalt latest release-asset mxcli-windows-amd64.exe via
  api.github.com/repos/mendixlabs/mxcli/releases/latest (User-Agent + TLS1.2), VERIFIEERT sha256
  (uit asset.digest) + bytegrootte, schrijft absoluut pad in mxcliPath. Mismatch/fout/weiger/geen net
  → bestand weg + nette fallback naar handmatige route (releases-URL), crasht nooit. mxlint NIET
  geautomatiseerd (blijft optioneel via officiële extensie — te fragiel: vereist mxlint.yaml-generatie
  + versie-ontkoppeling, mxlint-CLI latest v3.15.0 ≠ onze hardcoded v3.14.2-padconventie).
- README: mxcli-stap herschreven (auto-download met checksum + handmatig alternatief), expliciete
  waarschuwing "gebruik de release-binary, NIET git clone + make build (geen make op Windows)".
- Geverifieerd: parser-syntax OK; echte download v0.12.0 (80.809.984 bytes, sha256 OK) → cache;
  `mxcli --version` werkt vanaf de download; end-to-end installer (cached-branch) schrijft absoluut
  mxcliPath in schone settings; verse zip bevat de bijgewerkte installer+gids, geen TRB, geen settings.

PACKAGING-HARDENING (klantnaam + settings uit het gedeelde pakket):
- OORZAAK weggenomen: de csproj kopieerde mijn lokale acr-scan-settings.json (machine-/klantpaden)
  naar bin\Debug\net10.0 via CopyToOutputDirectory=Always → bij het verversen van de payload
  overschreef dat de gesaneerde versie. De `<None Update="acr-scan-settings.json">`-regel is
  VERWIJDERD. De extensie heeft geen settings-file nodig (AcrScanSettings.Load → defaults: mxcli via
  PATH + geopende app). Mijn lokale csharp-spike\acr-scan-settings.json blijft bestaan voor lokaal
  gebruik (wordt niet meer mee-gekopieerd).
- INSTALLER (Install-ClevrAcr.ps1) is nu eigenaar van de settings: vraagt + valideert het projectpad
  (map moet een .mpr bevatten; neutrale placeholder, geen klantvoorbeeld), detecteert mxcli UITSLUITEND
  via Get-Command (PATH) — gevonden → mxcliPath = .Source, niet gevonden → duidelijke melding +
  Mendix-Labs-install-URL (PLACEHOLDER — moet nog ingevuld) + mxcliPath="" (PATH-fallback), en
  schrijft een schone acr-scan-settings.json op de leeslocatie. Upgrade behoudt bestaande geldige
  waarden.
- HERHAALBAAR-VEILIGE assemblage: Build-Package.ps1 (repo-root, maintainer-only) bouwt → spiegelt
  bin → payload → STRIPT defensief elke acr-scan-settings.json → zipt → faalt-luid bij 'TRB'.
- KLANTNAAM gescrubd: rules.sample.json _pending ("op TRB" → "op het referentieproject");
  dist\clevracrshell (oude mock met TRB-sampledata) verwijderd; CLEVR-ACR-extension(.zip) bevat
  nergens nog 'TRB' of een settings-file (geverifieerd). OPEN: dist\CLEVR-ACR-source.zip is een
  volledige bron-snapshot waarin 'TRB' intrinsiek in tests/grondwaarheid-docs/sample-data zit —
  volledig scrubben valt buiten de "raak scan-logica/tests niet aan"-grens; beslissing aan Michel.
- Geverifieerd: bin geen settings (correct), lokale settings ongemoeid, pakket+zip TRB-vrij,
  installer schrijft schone settings (temp-projecttest). Echte Studio Pro-install-test: door Michel.

AFRONDINGSRONDE (product deelbaar gemaakt — 6 punten):
1. KNOPPEN SAMENGEVOEGD → één "Scan"-knop. Nieuw C#-bericht `RunFullScan`
   (SpikeDockablePaneViewModel.RunFullScan) orkestreert op één achtergrond-thread, in
   volgorde: (1) mxlint export+lint (ververst modelsource/), (2) mxcli + de CLEVR-eigen
   regels (security-export + expressie-pass) die op die VERSE modelsource leunen → lost de
   stale-modelsource-valkuil structureel op. Beide stappen draaien op DEZELFDE projectmap
   (ExclusionsProjectDir(), één keer resolved) zodat export en regels naar identieke
   modelsource wijzen. De losse routes (RunAcrScan/RunMxlintScan) blijven intern bestaan.
2. LAAD-INDICATOR: spinner naast de knop + voortgangstekst ("Exporting model…/Analyzing…")
   via `ScanProgress`-berichten; knop disabled tijdens de scan; `ScanFinished` her-enabled.
   CSS-valkuil gefixt: `.acr-spinner[hidden]{display:none}` (expliciete display:inline-block
   overrulet anders het [hidden]-attribuut). Geverifieerd via statische preview-harness.
3. HERNOEMD "CLEVR ACR Spike" → "CLEVR ACR" op elke zichtbare plek (menu-item, pane-titel,
   log-prefix). Interne id's/DLL/namespace ongemoeid (risicoloos houden).
4. CLEVR-LOGO: officiële CLEVR-logo.png in wwwroot/clevr-logo.png. Paneel-kop toont het via
   <img src>; het geëxporteerde rapport embedt het als data-URI (fetch→FileReader bij open
   van de pane → clevrLogoDataUri) zodat het rapport standalone blijft.
5. INSTALLATIEPAKKET: dist/CLEVR-ACR-extension/ — clevracr/ (VOLLEDIGE build-output incl.
   YamlDotNet.dll), Install-ClevrAcr.ps1 (vraagt projectpad, kopieert naar
   <project>/extensions/clevracr, bewaart bestaande settings bij upgrade, verifieert
   kritieke files), README.md (Studio Pro extension-development aanzetten, map-locatie,
   mxcli/mxlint-vereisten). Script getest tegen een temp-project: kopieert + verifieert OK.
6. MENDIX 11+: vereiste-melding in de paneel-footer én in het rapport; prominente sectie in
   de README. (Runtime-versiedetectie niet gedaan: de 11.10-API laadt sowieso niet op Mx10,
   dus een draaiende extensie is per definitie 11+.)
Build 0/0, tests 86/86. Nog te doen door de gebruiker: deploy via het script + scan vanuit
Studio Pro (echte flow) verifiëren.

Laatst bijgewerkt na de sessie waarin: Fase 1 (mapping-fix) + Fase 2 (rapport-
export) VOLTOOID en in Studio Pro geverifieerd; Fase 3 deel A (mxlint.com als 2e
engine in de C#-keten) VOLTOOID na een pittige async/deadlock-debug; en de
volledige mxcli-oppervlakte systematisch in kaart gebracht — met als kern-
correctie: de getypeerde flow-AST die we "misten" zit WEL in mxcli, via
`bson dump --format json`.

---

## WAT NU WERKT (bewezen in Studio Pro 11.10 op TRB)

### Lint-regels (geverifieerd)
- 11 geverifieerde ACR .star-regels, gekalibreerd tegen de grondwaarheid.
  Metadata uit de grondwaarheid; 4 security-severities op "TODO-confirm".
- Vastgelegd in acr-mxlint-voortgang.md + rule registry (rules.sample.json).

### De extensie (hybride C# + web, in Studio Pro)
- Hybride architectuur BEWEZEN: C#-backend (.NET 10) draait een proces via
  Process.Start, output via message-bus naar een C#-gehoste webview-pane.
- Werkende mxcli-scan: knop -> `mxcli lint --format json` -> parser -> C#-
  normalizer + registry -> Violation[] -> ACR-layout in de pane.
- Op TRB (mxcli): 2625 improvements (77 ACR / 2548 generiek), verdeeld over de 6
  ACR-categorieen (Performance + Reliability nu gevuld dankzij de mapping-fix).
- DRIE engines nu werkend: ACR .star-regels + bundled mxcli-regels + mxlint.com
  (Rego). mxlint draait als 2e engine via een aparte knop (deel A), resultaten in
  een aparte lijst — nog NIET samengevoegd met de mxcli-data (= deel B).
- UI: alles in 6 ACR-categorieen; per-regel groepering (uitklapbaar); herkomst-
  badges + regelnaam; 3 telkaarten; herkomst-filter; tekstfilter; preview-tekst;
  severity letterlijk uit de bron. Term "Improvements".
- CLEVR-gebrand HTML-rapport (Fase 2): zelfde Violation[] + renderfuncties als de
  pane, naar <project>\.clevr-acr\CLEVR-ACR-report-<timestamp>.html + auto-open.
- Normalizer = pure, geteste .NET 10-lib (25 tests groen).

---

## NIEUWE BEVINDINGEN DEZE SESSIE (belangrijk — sturen de routekaart)

### 1. mxcli kan veel MEER dan we benutten (`lint --list-rules`)
mxcli heeft een grote set bundled regels die al meedraaien: QUAL001 (McCabe
complexity), CONV011 (commit-in-loop), CONV009/QUAL003 (microflow-grootte),
ARCH001-003, SEC005-009, CONV013/014 (error handling), etc. De "flowgraaf"-
regels die we als Rego-exclusief aannamen, doet mxcli dus deels AL.

### 2. De lege categorieen waren een MAPPING-BUG, geen ontbrekende engine
De display-mapping mapt op letter-PREFIX (CONV/MPR/QUAL), maar mxcli's CONV
bevat naming-, performance-, quality- en architectuur-regels door elkaar.
Daardoor: Performance werd alleen door (niet-bestaande) prefix PERF gevoed ->
altijd leeg; Reliability door niets gemapt -> altijd leeg.
FIX (klein, bestaande data): map per-regel op de ECHTE mxcli-categorie uit
`--list-rules` (die we al ophalen voor namen) i.p.v. de prefix. Gemeten op TRB
vult dat beide categorieen: Reliability ~388, Performance ~11.
Implementatie-noot: lint-JSON heeft geen categorie per violation -> joinen op
`--list-rules`.

### 3. `mxcli report` bestaat al — bijna je hele export-fase
`mxcli report -p <mpr> --format html|json|markdown` geeft een GESCOORD best-
practices-rapport (overallScore, per-categorie scores, topActions/remediaties,
alle findings). HTML is standalone met embedded CSS. Dit kan de geplande export
grotendeels VERVANGEN of VOEDEN. Caveat: mxcli's eigen 7-categorie-taxonomie en
styling, niet de 6 ACR-categorieen / CLEVR-look. trb-report.html = goed genoeg
voor een product owner; de CSV-export van mxlint is dat NIET.

### 4. mxlint.com BEWEZEN werkend in Studio Pro (eigen extensie geinstalleerd)
mxlint-cli `export` (model -> YAML in modelsource/) + `lint` (Rego op de YAML)
draait, lokaal, en is als Studio Pro-extensie geinstalleerd. Bewijs: 2363 checks,
24 rules, 182 fails op TRB. Voegt regels toe die mxcli NIET heeft:
- ComplexMicroflowsWithoutAnnotations (103), NumberOfElementsInMicroflow (44),
  InlineStylePropertyUsed (14), HeadingsInAscendingOrder (11, accessibility),
  NoDefaultValue (8), MxAdminNotUsed (1), OneH1TagPerPage (1).
EERLIJKE NUANCE: AvoidCommitInLoop gaf 0 op TRB; de ECHT diepe ACR-Performance-
regels (Non-indexed attr in XPath, CRUD too early in flow, XPath ordering — de
~804 ACR-Performance-violations) zitten OOK in mxlint.com NIET. Die blijven het
domein van ACR/SDK + de Studio Pro Best Practice Recommender.
CONCLUSIE: mxcli + mxlint.com samen = een groot, waardevol deel van ACR — NIET
"alles". Eerlijk communiceren: niet "ACR volledig vervangen".

### 5. De mxlint-bronnen zijn OPEN (wiel niet opnieuw uitvinden)
- mxlint-cli (Go) — broncode binnengehaald (mxlint-cli-main.zip).
- mxlint-extension (Studio Pro) — broncode binnengehaald (mxlint-extension-main.zip).
- Rego-regels — mxlint-rego-inventaris.md (28 regels, metadata in # METADATA-blok).
Claude Code kan hun aanpak hergebruiken i.p.v. from scratch. LET OP CRLF (\r) in
de Rego-metadata bij parsen.

### 6. VOLLEDIGE mxcli-inventarisatie — de gemiste diepte zit in `bson dump`
Systematisch elk mxcli-commando + subhelp doorlopen (geen steekproef meer). Kern-
correctie op een eerdere conclusie: we dachten dat alleen mxlint de getypeerde
flow-AST kon geven (describe gaf alleen MDL-TEKST). FOUT — `mxcli bson dump
--type microflow --object <naam> --format json` geeft de VOLLEDIGE getypeerde
model-AST: $Type-nodes met LoopedActivity, CommitAction, CreateChangeAction,
ExclusiveSplit, expressies — dezelfde structuur als mxlint's YAML (beide lezen
dezelfde BSON uit de .mpr v2).
GEVOLG: een deterministische flow-regel (bv. commit-in-loop) is op mxcli ALLEEN
te schrijven door die boom te doorlopen — zonder mxlint, zonder Rego, zonder
MDL-tekst te parsen. Dit is de "derde weg" (zie hieronder).
NUANCE: bson dump = RUWE BSON-als-JSON (verbose, {Key,Value}-vorm, alpha
"inspection"-tool), per element (--object; --list om te enumereren). mxlint geeft
een opgeschoonde boom + kant-en-klare Rego-engine. Dus: voor LOSSE eigen checks
in C# is bson dump reeel; voor een BREED regelpakket blijft mxlint efficienter.
ANDERE nuttige, nog niet benutte commando's (allemaal --json): impact, refs,
callers/callees (call-graph), context (relaties), en de MDL CATALOG-query
(`SELECT ... FROM CATALOG.ENTITIES --json` → AttributeCount, AccessRuleCount,
HasEventHandlers, Generalization, QualifiedName...). `eval` = vaste check-set
(entity_exists, lint_passes, mx_check_passes...) — GEEN plek voor eigen flow-
logica, alleen een acceptatie-/regressieharnas. structure/show/project-tree =
naam-/signatuurniveau (geen flow-internals).
DUS: we hebben nu de VOLLEDIGE oppervlakte in kaart; de diepte is toegankelijk
via mxcli zelf (bson dump). Eerdere "alleen mxlint kan de boom"-conclusie =
gecorrigeerd.

### 7. Geleerde lessen uit de Fase 3 deel A debug (voor toekomstige engines)
- PIPE-DEADLOCK: een proces met veel stdout/stderr (mxlint lint = honderden
  regels) deadlockt als je de streams SEQUENTIEEL leest (eerst stdout helemaal,
  dan stderr). Fix: beide streams PARALLEL async leegtrekken vóór WaitForExit,
  + een timeout-vangnet. ProcessRunner doet dit nu voor alle aanroepen.
- ASYNC/UI-THREAD: WebView2 PostMessage MOET op de UI-thread. De MessageReceived-
  thread heeft een WPF DispatcherSynchronizationContext (geverifieerd: aanwezig);
  zwaar werk via Task.Run, resultaat terug-marshallen via die context, en
  GEGARANDEERD posten in elke uitkomst (anders blijft de pane stil op "Bezig...").
- DIAGNOSTIEK: ILogService schrijft naar Studio Pro's interne log (Help -> Open
  Log File Directory), NIET naar %LOCALAPPDATA%\Mendix. Daarom schrijft de
  extensie nu ook naar <project>\.clevr-acr\mxlint-debug.log (vindbaar). LES: bij
  een hang in een dichte doos eerst ZICHTBAAR maken wat er gebeurt, dan pas fixen
  — dat hakte de knoop door na meerdere gok-rondes.
- mxlint EXIT 1 = findings (geen fout): lees de jsonFile ongeacht exitcode.
- mxlint BUNDELT per document meerdere violations van dezelfde regel in 1
  failure.message, gescheiden door de [SEVERITY, CATEGORY, rulenummer]-marker.
  Normalizer splitst nu op die marker: 182 "failures" -> 480 losse violations.
- FINGERPRINT-beperking: gesplitste violations van dezelfde regel op hetzelfde
  document delen nu 1 fingerprint (het attribuut staat alleen in de reason, en de
  spec verbiedt reason in de fingerprint). Gevolg voor Fase 6: exclusion werkt op
  rule+document-niveau, niet per attribuut. Per-attribuut zou reason-parsing in
  elementName vereisen — bewust uitgesteld.

---

## OPENSTAANDE FASEN (HERZIENE volgorde na de bevindingen)

### Fase 1 — Display-mapping per-regel  ✅ VOLTOOID (geverifieerd op TRB)
Generieke regels mappen nu op de echte mxcli-categorie uit `--list-rules` i.p.v.
de prefix. Geverifieerd in Studio Pro: Performance (CONV016/017) en Reliability
niet langer leeg; CONV011→Performance en CONV001→Project hygiene (zelfde prefix,
nu verschillende categorie = bug weg). Mapping-tabel in spec §5; "mxcli
correctness → ACR Reliability" expliciet vastgelegd. Display-mapping in de render-
laag; intern Violation.category ongewijzigd.

### Fase 2 — Rapport-export  ✅ VOLTOOID (geverifieerd: rapport ziet er goed uit)
Optie B gekozen: eigen CLEVR-gebrand HTML uit dezelfde Violation[] + renderfuncties
als de pane (consistent met wat de developer ziet), i.p.v. mxcli's eigen HTML.
Opslag: <project>\.clevr-acr\CLEVR-ACR-report-<timestamp>.html + auto-open + pad
in de statusregel (geen native save-dialoog in de Studio Pro API). mxcli report
blijft een latere optie als databron voor scores/remediaties.

### Fase 3 deel A — mxlint.com als 2e engine (C#-keten)  ✅ VOLTOOID
MxlintScanService (export+lint via Process.Start, async, parallel stream-drain,
exit≠0=findings), MxlintNormalizer (split op marker → 480 violations, source=
mxlint, CRLF-trim), aparte knop + aparte lijst. Geverifieerd op TRB: 480 losse
violations. Zie bevinding 7 voor de geleerde lessen. Nog NIET samengevoegd met de
mxcli-data (= deel B).

### Fase 3 deel B — mxlint samenvoegen in het hoofdpaneel  ✅ VOLTOOID
mxlint-violations staan nu samengevoegd met de mxcli-data in de 6 ACR-categorieen
(aparte lijst weg). Twee aparte knoppen, één overzicht; replaceOrigin() vervangt
alleen de violations van de gescande herkomst (je kunt mxcli én mxlint draaien en
samen zien). Herkomst-filter Mxlint.com nu gevuld; 3 telkaarten tellen de merged
set. 26/26 tests. De 3 gemaakte keuzes:
1. PRECEDENTIE = mxcli > ACR > mxlint (optie A; Mendix zet in op mxcli → voorop,
   ook boven de gekalibreerde ACR-regels). De 6 ACR↔mxlint-overlappen
   (AnonymousDisabled↔ACR_SEC_GUEST, DemoUsersDisabled↔ACR_SEC_DEMOUSERS,
   SecurityChecks↔ACR_SEC_CHECKED, StrongPasswordPolicy↔ACR_SEC_PWPOLICY,
   NumberOfAttributes↔ACR_ENT_ATTRS, AvoidUsingValidationRules↔ACR_ENT_VALRULES)
   zijn onderdrukt aan de mxlint-kant (spec §4).
2. ACCESSIBILITY → Maintainability via display-mapping (spec §5; intern ongewijzigd).
3. TWEE knoppen, één scherm — functioneel: mxcli werkt op Mx11, mxlint ook op
   Mx10, dus bruikbaar over beide versies. mxlint async, mxcli synchroon.

>> NIEUW ONTDEKT, NOG NIET OPGELOST — ACR↔mxcli-overlap (eigen vervolgtaak):
   er is óók overlap tussen jouw ACR .star-regels en mxcli's bundled regels — bv.
   ACR_SEC_STRICT ↔ SEC005 StrictModeDisabled: beide melden "strict mode uit" op
   hetzelfde document, net anders verwoord → dubbel in het rapport. Volgens
   precedentie A wint mxcli (SEC005 heeft ook de CVE-2023-23835-verwijzing); 
   ACR_SEC_STRICT moet onderdrukt. VERVOLGTAAK (frisse sessie): systematisch alle
   11 ACR-regels langs de mxcli-bundled-regellijst leggen en per regel beslissen:
   onderdrukken (mxcli wint) óf de ACR-regel helemaal LATEN VALLEN omdat mxcli 'm
   al dekt. Vermoede kandidaten: de 4 ACR_SEC_* (vs mxcli SEC001-009). Opschoonslag,
   geen quick fix — vergt de inventarisatie eerst.

### Fase 4 — Klikbaar object: navigeren naar element + docs  [GEBOUWD]
Bleek breder haalbaar dan alleen docs. Geverifieerd tegen de ECHTE 11.10-assembly
(reflectie). GEBOUWD, compileert (0/0), tests 26/26:
(a) DOCUMENT OPENEN IN STUDIO PRO — klik op de documentregel van een improvement
    opent het document. C# resolt de unit: eerst via stabiele GUID
    IModel.TryGetAbstractUnitById(documentId) (mxcli/ACR hebben documentId);
    fallback naam-walk Root.GetModules()->module->DomainModel/folders/GetDocuments()
    (mxlint heeft GEEN GUID). Navigatie op DOCUMENTNIVEAU via
    IDockingWindowService.TryOpenEditor(unit, null) — net als de mxlint-extensie.
(b) DOC-URL IN BROWSER — klik op "Documentatie" opent de URL via
    Process.Start{UseShellExecute=true} (zelfde patroon als rapport openen).
Injectie: SpikeDockablePaneExtension importeert nu IDockingWindowService en geeft
() => CurrentApp (IModel) + de service door aan de VM. Handlers: "OpenDocument",
"OpenUrl". Data/UI-scheiding intact: Violation ongewijzigd; JS post alleen
bestaande velden; het HTML-rapport blijft statisch (interactive=false) met een
gewone werkende doc-href.
NAVIGATIE PER DOCUMENTTYPE (geverifieerd tegen 11.10 via net10-reflectie):
- microflow/page/enumeratie: GUID-route (TryGetAbstractUnitById) → opent. ✓
- entiteit: GEEN unit-GUID → naam-route → DOMEINMODEL openen ÉN de entiteit FOCUSSEN
  (IEntity is een IElement; IDomainModel.GetEntities() → match op naam →
  TryOpenEditor(domainModel, entity)). Fallback = domeinmodel zonder focus. ✓ [GEBOUWD]
- subfolder-docs (microflow/page e.d.): naam-walk is RECURSIEF (module → folders →
  documenten); werkt. Pages/microflows openen meestal al via de GUID-route. ✓
- SNIPPETS = API-GRENS (definitief, via net10-reflectie): de 11.10 ExtensionsAPI kent
  GEEN snippet-type. De volledige IDocument/IAbstractUnit-set is: IConstant, IEnumeration,
  IJavaAction, IMicroflow(+Rule/ServerSide/Base), IPage, IDomainModel. Geen ISnippet.
  Bovendien geeft mxcli snippets een LEGE documentId (terwijl pages/microflows/enums/
  entiteiten wél een GUID krijgen) → ook de SDK kent snippets geen unit-identiteit toe;
  en GetDocuments() levert ze niet op (recursieve walk mist ze, bevestigd in de log).
  Conclusie: snippets zijn niet rechtstreeks te openen via deze API. Klik toont een
  eerlijke melding i.p.v. "niet gevonden". (Eerdere dubbele-module-diagnose was NIET de
  oorzaak — die was wél een echte aparte bug en is gefixt.)
SYSTEM-MODULE FILTER (render-laag, main.js): violations uit de System-module worden VOLLEDIG
verborgen in de weergave (lijst, 3 telkaarten, totaal, geëxporteerd rapport) — System is niet
wijzigbaar door een developer → ruis. Bepaald op de qualified name (prefix "System." of exact
"System"). Data blijft compleet (data/UI-scheiding); puur weergavefilter, bovenop de bestaande
categorie/severity/herkomst/tekst-filters + reset. Op TRB: 78 van 2625 ruwe violations zijn System.
- ENUMERATIONS = HOST-GRENS: IEnumeration is een unit, TryOpenEditor slaagt technisch,
  maar Studio Pro toont enums als DIALOOG (niet altijd zichtbaar vanuit extensie). Geen
  alternatieve toon-API. We openen nog wel, maar tonen een EERLIJKE melding.
- PROJECT SECURITY = API-GRENS: project-niveau-artefact, GEEN module-document. 11.10 heeft
  geen ISecurity-type/open-methode; IProjectDocument heeft geen Name om op te matchen;
  INavigationManagerService doet alleen web-menu's. Klik toont eerlijke melding:
  "Project security is niet direct te openen via de Extensibility API (11.10)."
Alle routes loggen hun keuze + uitkomst in mxlint-debug.log.

MXLINT REGELNAMEN: mxlint-regels tonen nu een beschrijvende naam naast hun nummer
(002_0009 → NoDefaultValue), net als mxcli. Bron: de # METADATA `rulename` van de .rego's
→ vaste map MxlintRuleNames (25 regels). De lint-results.json zelf heeft GEEN naam, alleen
het .rego/.js-bestandspad. MxlintNormalizer.BuildRuleNames(json) bouwt rulenumber→naam per
testsuite: vaste map, anders PascalCase van de bestandsnaam-slug — zo krijgen óók regels die
(nog) niet in de map staan een naam (bv. de .js-accessibility-regels 004_0003 one_h1 → OneH1,
004_0004 headings → Headings; de reference-ruleset is ouder dan wat in TRB draait). De service
zet dit in payload.ruleNames (zelfde vorm als mxcli); main.js merget beide engines in
lastRuleNames → render-laag toont 'm via ruleName() (geen render-wijziging).

UI-TAAL: de volledige extensie-UI is nu consistent ENGELS (knoppen, telkaart-koppen,
status-/foutmeldingen, tooltips, placeholder, rapport-kop). De ACR-categorienamen
(Project hygiene/Maintainability/Performance/Architecture/Reliability/Security) zijn
onveranderd — die horen bij het datacontract. Debug-log-teksten blijven bewust NL (intern).

### Fase 5 — "Ask Maia"-prompt (PLAK-variant)  [GEBOUWD — plak-variant]
Render-laag (main.js): "Copy Maia prompt"-knop op TWEE niveaus:
- REGEL-kop: prompt voor de hele regel met al z'n punten (gecapt op 50, "... and N more"
  zodat grote regels als de 283-punts default-value-regel niet exploderen).
- INDIVIDUEEL punt: prompt gericht op dat ene geval.
Prompt is ENGELS en bevat ruleId+naam, categorie (displayCategory), severity, herkomst-
engine, document(en), reason(s) en suggestion(s). Kopiëren via navigator.clipboard met
fallback op textarea+execCommand (WebView2 blokkeert clipboard soms). Bevestiging:
"Maia prompt copied — paste it into Maia". Niet in het geëxporteerde rapport (interactive=false).
Data/UI-scheiding intact. DIRECTE injectie in Maia blijft onbewezen → niet gebouwd.

### EERSTE EIGEN REGEL op de project-security-export — ACR #12  [GEBOUWD]
"Project role should have at most one module role per module" (CLEVR-MAINT-005). Géén
mxcli/mxlint — eigen pure parser ProjectSecurityParser (csharp-normalizer, spiegel van
BsonMicroflowParser; 6 tests). Bron: modelsource/Security$ProjectSecurity.yaml (UserRoles[]
→ {Name, ModuleRoles:["Module.Role"]}); groepeer per user-role op het module-deel; >1 =
overtreding. Identiteit: ruleId CLEVR-MAINT-005, acrCode ProjectRoleMaxOneModuleRolePerModule,
engineRuleKey CLEVR_SEC_ONE_MODULEROLE_PER_MODULE (zelf-geproduceerd, NIET mxcli-geclaimd →
bewust niet in rules.sample.json). Categorie Maintainability (ACR: Performance — bewuste keuze,
één constante om bij te stellen), severity Critical. Herkomst: kind=acr/source=clevr-acr →
ACR-badge. Integratie: hangt aan de mxcli "Scan for improvements" (AcrScanService leest de YAML
uit de projectmap → Violations in de AcrViolations-payload). Geverifieerd tegen TRB-grondwaarheid:
exact 5 violations / 2 rollen (Administrator op Accesslog/Administration/SupportModule/UserCommons
+ Behandelaar op TRB), 7 rollen clean — geen false positives/negatives. NB: MDL CATALOG legt deze
mapping NIET bloot → de YAML-export is de bron.

### TWEE SECURITY-REGELS op de export — ACR #7 + #10  [GEBOUWD]
Beide kind=acr/source=clevr-acr (ACR-badge), Security/Blocker (zoals ACR), geïntegreerd in de
mxcli "Scan for improvements" (AcrScanService), pure geteste parser-uitbreidingen op
ProjectSecurityParser. Anonieme rol-set = ModuleRoles van GuestUserRole MITS EnableGuestAccess
true (anders 0). Op TRB nu guest AAN met GuestUserRole=WebserviceUser (set: System.User,
Administration.User, Integratie.Admin, Accesslog.Admin). LET OP: de modelsource-export kan stale
zijn — eerst `mxlint export` draaien voor verse YAML (TRB modelsource was 4 dagen oud).
- ACR #7 (CLEVR-SEC-005, AnonymousCreatePersistentEntity): persistente entiteit met AccessRule
  AllowCreate:true + een anonieme AllowedModuleRole. Persistable uit CATALOG.entities.EntityType
  (betrouwbaarste bron — YAML zet Persistable genest onder MaybeGeneralization + ÉRFT via
  generalization). Access-rules uit de domain-model-YAML. TRB-grondwaarheid (geverifieerd, FP/FN-vrij):
  1 violation = Accesslog.AccesslogBankenportaal (via Accesslog.Admin). Integratie.Melder/Melding
  hebben anon-create maar zijn NON_PERSISTENT → terecht niet geflagd.
- ACR #10 (CLEVR-SEC-006, AnonymousEditableUnlimitedString): unlimited string-attribuut (Length 0)
  dat ReadWrite is voor de anonieme rol (MemberAccess onder een anonieme AllowedModuleRole).
  Length uit de YAML (StringAttributeType.Length); CATALOG.attributes.Length is ONBETROUWBAAR
  (0 voor alle 748 strings). Geen persistable-filter. TRB-grondwaarheid (geverifieerd, FP/FN-vrij):
  4 violations = Accesslog.AccesslogBankenportaal.Message, Integratie.Melder.Overige_Gegevens,
  Integratie.Melding.BeschrijvingGedrag + .Overige_Gegevens.
Bevinding: modelsource bevat maar 67/283 entiteiten (alleen app-eigen modules, net als mxlint);
System/marketplace niet geëxporteerd → deze regels dekken de app-scope (consistent met mxlint).

### EERSTE EXPRESSIE-ROUTE-REGEL — redundante empty-string-check (CLEVR-REL-001)  [GEBOUWD]
Nieuwe route: Mendix-expressie-STRINGS parsen uit de bson-AST (expressies staan plat, niet als
sub-AST). Bron: ExpressionSplitCondition.Expression (split-condities) + ChangeActionItem.Value
(toekenningen). Pure ExpressionAnalysis.RedundantEmptyStringPaths (regex) + bson-extractie
(BsonMicroflowParser.DetectRedundantEmptyStringChecks + generieke VisitNodes-walker). CONSERVATIEF:
flag alleen als OP HETZELFDE pad ($x/Attr) zowel een empty-check (=/!= empty) ALS een lege-string-
check (=/!= ''/"") staat in dezelfde expressie; losse != empty (396 idiomatisch) en losse != ''
worden NIET geflagd. Categorie Reliability (leunt tegen correctness; één const, bij te stellen naar
Maintainability), severity Major (voorstel), kind=acr/clevr-acr, engineRuleKey
CLEVR_REL_REDUNDANT_EMPTY_STRING. TRB-grondwaarheid (geverifieerd, FP/FN-vrij): 19 distinct
(microflow,pad) over 8 microflows; FP-check op een microflow zonder lege-string-literal = 0. NB:
méér dan de verkenning's grove 15 — die miste de "= empty or = ''"-vorm (IVK_SaveDossier) +
YAML-quoting; de conservatieve bson-detectie is accurater. 13 tests. NIET in de live-scan gehangen:
dat vergt per-microflow bson-dump (~1592×, te traag synchroon) — gedeelde orchestratie, te beslissen
bij het batchen van meer expressie-regels (de extractie is dan herbruikbaar; 2e regel ~regel-logica + tests).

### EXPRESSIE-ROUTE LIVE — orchestratie + REL-001 + regel D (CLEVR-MAINT-006)  [GEBOUWD]
BRON-BESLISSING (met cijfers): YAML wint van bson. Snelheid: YAML één-pass over 471 microflow-
YAML's = 0,89s (2891 expressies); bson = ~2s/dump × 471 ≈ 16 min. Betrouwbaarheid: 110 expressies
zijn block-scalars (multi-line) + quoting → daarom een ECHTE YAML-parser (YamlDotNet, in de spike;
normalizer blijft dependency-vrij). Bewezen: YAML reproduceert de bson-expressies exact (cross-check
tegen bson op de nieuwe microflows, incl. block-scalar $ZoekObject/Voornaam). Gedeelde infra:
AcrScanService.DetectExpressionRules → MicroflowYamlExpressions.Extract (YamlDotNet) → (mf,expr)-paren
→ ExpressionRules. Pure regel-laag in de normalizer: ExpressionAnalysis (string-predicaten) +
ExpressionRules (Violation-bouw uit paren); bson- én YAML-route delen dezelfde regel-laag.
- CLEVR-REL-001 (redundante empty-string): nu LIVE in "Scan for improvements". GEVERIFIEERDE
  grondwaarheid = 31 distinct (microflow,pad) over 14 microflows — NIET 19. De eerdere 19 kwam uit
  een incomplete grep-kandidaatselectie die block-scalar-microflows miste; de volledige YAML-scan
  is accurater (bson-cross-check bevestigt de extra's).
- CLEVR-MAINT-006 (redundante boolean-vergelijking $x = true/false): categorie Maintainability,
  severity Major (beide bij te stellen), kind=acr/clevr-acr. Conservatief: operand moet een $pad
  zijn; alleen de true/false-literal (word-boundary, geen enum/identifier). GEVERIFIEERDE
  grondwaarheid = 94 distinct (microflow,operand) — NIET ~40 (zelfde block-scalar-reden). 13 tests.
SCAN-DUUR: de expressie-pass = 0,89s (verwaarloosbaar naast de mxcli-lint). Schaalt: een 3e
expressie-regel hergebruikt dezelfde pass (paren in geheugen) → ~ms. Caveat: leest modelsource
(ververst door mxlint-export) — draai die vóór de scan voor verse expressies.

DEPLOY-BUG (gevonden + gefixt): YamlDotNet.dll werd NIET mee-gedeployed → in Studio Pro faalde
MicroflowYamlExpressions.Extract op de assembly-load → de try/catch in DetectExpressionRules slikte
'm (naar ILogService, niet vindbaar) → 0 expressie-violations (de andere regels werkten want die
raken YamlDotNet niet). OORZAAK: een class-library kopieert NuGet-runtime-deps niet naar de output.
FIX in Clevr.AcrSpike.csproj: <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies> +
ExtensionsAPI met ExcludeAssets="runtime" (Studio Pro levert die zelf — niet meekopiëren). Geverifieerd:
YamlDotNet.dll staat nu in bin/Debug/net10.0, ExtensionsAPI.dll niet. DEPLOY de HELE output-map
(incl. YamlDotNet.dll), niet losse DLL's. DIAGNOSE-LOGGING (DebugLog → mxlint-debug.log) staat nu in
DetectExpressionRules: projectDir + modelsource-pad + #YAML's + #expressies + #violations + volledige
exception; plus de pad-vergelijking (mxcli/regels-projectDir vs _getProjectDir van de open app) om
een eventuele tweede oorzaak (export ververst pad B, scan leest pad A) zichtbaar te maken.

### Fase 6 — Exclusions met verplichte reden  [GEBOUWD]
Een improvement uitsluiten kan ALLEEN met een reden (geen stille uitsluiting). Opslag in
$project/.clevr-acr/exclusions.json (mee in version control → team deelt). Match op de
fingerprint sha1(ruleId|documentQualifiedName|elementName), al op elke Violation.
- C# (pure, getest): Exclusion-record + ExclusionsJson (parse/serialize/upsert/remove,
  4 tests). IO in ExclusionStore (csharp-spike); ViewModel-handlers RequestExclusions/
  AddExclusion/RemoveExclusion → schrijft het bestand, post "Exclusions" terug. excludedBy=
  Environment.UserName, date=vandaag. Server-side vangnet: lege reason → ExclusionError.
  Exclusions landen in DEZELFDE projectmap als de scan (settings.projectPath, anders CurrentApp).
- Render-laag (main.js): "Exclude"-knop per punt → modale dialoog die een reden VERPLICHT
  (Exclude-knop disabled tot er tekst staat). Uitgesloten improvements verdwijnen uit lijst +
  alle telkaarten + totaal + rapport (activeViolations() = niet-System én niet-excluded),
  bovenop de bestaande categorie/severity/herkomst/tekst-filters. "Show excluded (N)"-toggle
  toont de Excluded-sectie met reden + excludedBy/date + "Remove exclusion".
- FINGERPRINT-BEPERKING (bevinding 7) EERLIJK afgehandeld: gebundelde mxlint-violations delen
  één fingerprint → de dialoog WAARSCHUWT vooraf ("shares one fingerprint with N findings ...
  will hide all N") en de Excluded-card toont "applies to N findings". Niet omgebouwd.
- STALE exclusions (fingerprint matcht geen huidige violation) zichtbaar gemarkeerd
  ("stale — no longer matches"), met Remove. RAPPORT: aparte "Excluded improvements"-sectie
  (matched + stale, met reden), statisch (geen knoppen). Toast-feedback bij exclude/remove.
- EXCLUDE RULE (uitbreiding): knop "Exclude rule" op de regel-kop (naast Copy Maia prompt)
  sluit ALLE punten onder de regel uit met DEZELFDE reden. Hergebruikt de verplichte-reden-
  dialoog (openReasonDialog). Schrijft één exclusion per UNIEKE fingerprint via een batch
  ("AddExclusions" → ExclusionStore.AddMany, één bestand-write, upsert=dedup) — gebundelde
  mxlint-punten met gedeelde fingerprint dus NIET dubbel. De dialoog-telling = aantal unieke
  fingerprints; bij bundeling licht een note het verschil toe ("N findings map to M entries").
  Per-punt Remove blijft werken.
- EXCLUDED-SECTIE PER REGEL (uitbreiding): de "Show excluded"-sectie is gegroepeerd PER REGEL
  (regel-kop = ruleId + naam + aantal, zoals de hoofdlijst) met de uitgesloten punten eronder;
  stale entries staan in hun regel-groep met "stale"-markering. Knop "Remove rule exclusion"
  op de regel-kop zet ALLE entries van die regel (incl. stale) in één keer terug via een
  lichte bevestigingsdialoog ("This will restore all N excluded findings for this rule.") →
  batch "RemoveExclusions" → ExclusionStore.RemoveMany (één bestand-write). Per-punt Remove
  blijft beschikbaar. Rapport: zelfde groepering, statisch (geen knoppen).

### Manual checks — controlevragen die de developer zelf beantwoordt  [GEBOUWD]
Generiek + uitbreidbaar mechanisme (eerste vraag: Performance/Major over de Best Practice
Recommender). Een manual check is geen model-violation maar een vaste vraag die als normale
improvement verschijnt tot 'ie geldig beantwoord is, en na 30 dagen opnieuw moet (recheck).
- DEFINITIES + verloop-logica in de render-laag (main.js: MANUAL_CHECKS, MANUAL_CHECK_EXPIRY_DAYS).
  C# is GENERIEK: bewaart alleen het antwoord per id (ManualCheckStore → $project/.clevr-acr/
  manual-checks.json, mee in version control, NIET gitignored). Pure model+json in de normalizer
  (ManualChecksJson, getest). Handlers: RequestManualChecks/AnswerManualCheck/ClearManualCheck.
- STATE: unanswered / "no" (+reden) / "yes" (+toelichting). Verplichte note via de HERGEBRUIKTE
  reden-dialoog (uitgebreid met Ja/Nee-knoppen). Gestempeld met Environment.UserName + datum
  (server-side vangnet op lege note). Geldig "yes" (<30d) → VERDWIJNT uit open-lijst/-telling →
  "Answered manual checks"-sectie (toggle, analoog aan Show excluded) + in het rapport, met
  antwoord/datum/wie/recheck-datum. Verlopen "yes" (≥30d) of "no"/unanswered → telt als open.
- INTEGRATIE: open checks worden synthetische violations (kind="manual", origin "manual" — 4e
  herkomst in de filter + telkaart + status + rapport-kop) en lopen zo door de bestaande
  pipeline: categorie (Performance), tellingen, filters, System-filter, Exclude + Ask-Maia.
  Data/UI-scheiding intact; exclusions-infra hergebruikt (store/handlers/dialoog/secties).

### Fase 5 (oud ontwerp) — "Ask Maia"-prompt (PLAK-variant)  [haalbaar; injectie NIET]
Splits het idee in twee:
- HAALBAAR: een "Ask Maia"-knop op een improvement die een context-rijke PROMPT
  GENEREERT (improvement + regel + document + remediatie) die de developer zelf
  in Maia plakt. Dit is puur tekst samenstellen in je eigen paneel -> kan zeker.
- ONBEWEZEN: de prompt RECHTSTREEKS in Maia injecteren. Vereist een Maia-API
  voor extensies waarvan NIET bekend is dat 'ie bestaat. Niet op bouwen tot
  bewezen. Begin met de plak-variant; die levert bijna alle waarde.

### Fase 6 — Exclusions-UI  [waardevol zodra in gebruik]
Improvements onderdrukken met reden. Spec sectie 3 ligt klaar (fingerprint,
$project/.clevr-acr/exclusions.json, stale exclusions zichtbaar tonen). LET OP de
mxlint fingerprint-beperking uit bevinding 7 (rule+document-niveau, niet per
attribuut, tenzij je reason-parsing toevoegt).
ONTWERP-AANSCHERPING (Michel): een gebruiker MOET een reden opgeven om een
improvement uit te sluiten (geen stille uitsluiting — altijd verantwoording). De
exclusion + reden wordt vastgelegd in $project/.clevr-acr/exclusions.json (staat
IN de projectmap → gaat mee in version control). Bij commit + volgende pull ziet
de volgende developer de uitsluiting + reden. De uitgesloten improvements + reden
moeten ZICHTBAAR zijn in het CLEVR-rapport (transparant naar product owner /
volgende developer: "bewust niet opgelost, want ..."). NB: per-punt-exclusion
voor mxlint botst op de fingerprint-beperking (bevinding 7) — bewust afwegen.

### Fase 7 (OPTIONEEL/VERKENNING) — "derde weg": eigen diepe regels op `bson dump`
Nu bewezen dat `mxcli bson dump --format json` de getypeerde flow-AST geeft, kun
je eigen deterministische Orange-tier-regels (flow/expressie) in C# bouwen op
mxcli ALLEEN — zonder mxlint. Afweging: ruwe/verbose BSON + per-element
enumereren vs. mxlint's nette boom + Rego. NIET nu bouwen; relevant als je single-
engine wilt blijven óf als Mendix de BSON-output ooit opschoont tot nette JSON
(aannemelijk gezien hun AI-richting). Veel flow-checks bestaan al in mxcli lint
(CONV011/QUAL001/CONV009) — die hoef je sowieso niet zelf te bouwen.

---

## OPRUIM- / BESLISPUNTEN (niet urgent, wel bewust maken)
- SPIKE -> PRODUCT: de "spike"-codebase is feitelijk de productbasis. Beslis
  bewust: definitief maken (hernoemen/opschonen) of schoon herbouwen.
- Oude echo/RunCommand-handler = dode code -> opruimen.
- 4 security-severities (ACR_SEC_*) op "TODO-confirm" -> uit ACR Java-bron.
- DISTRIBUTIE: collega's mogen GEEN npm/build nodig hebben. Mendix extensie-
  packaging uitzoeken (Marketplace / zip / installer). Tip: bekijk hoe de
  mxlint-extension (open bron) z'n distributie doet.
- STRATEGISCH (met CLEVR-collega's bespreken): Mendix bouwt mxcli uit als
  AI-toegang tot Mendix. Positioneer de extensie als de CLEVR-AGGREGATOR +
  CLEVR-context (categorieen, kalibratie, rapport-voor-klant, Ask-Maia) bovenop
  de engines — NIET als "ACR herbouwd". Feitelijke stand na de inventarisatie:
  * mxcli + mxlint dekken de BREDE Blue-tier (structuur, naming, security,
    accessibility, complexiteit-als-telling) goed.
  * De DIEPE flow/XPath-regels (ACR's ~804 Performance-set) zitten kant-en-klaar
    in GEEN van beide. MAAR: de getypeerde structuur om ze zelf te bouwen is
    toegankelijk via mxcli bson dump (Fase 7) — dus de extensie is NIET
    vastgepind op de Blue-tier; je hebt opties en groeit mee met mxcli.
  * SDK-route (ACR's Java/SDK in de extensie draaien) = afgeraden: ordegrootte
    zwaarder, reproduceert ACR, roeit tegen Mendix' richting in.

---

## DOCUMENTEN (de "geheugens" van dit project)
- clevr-acr-shell-spec.md ......... datacontract + architectuur (autoritatief)
- clevr-acr-shell-status.md ....... dit document (kompas)
- acr-mxlint-voortgang.md ......... de 11 geverifieerde regels + API-feiten
- acr-mxlint-indeling.md .......... feasibility-map (Green/Blue/Orange/Red)
- mxlint-rego-inventaris.md ....... de 28 Rego-regels voor Fase 3
- acr-rule-counts-groundtruth.json  de ACR-grondwaarheid (autoritatieve bron)

## REFERENTIE-BRONNEN (open source, uitgepakt in _reference/ — NIET je eigen code)
Pas relevant vanaf FASE 3 (mxlint.com-integratie) en FASE 4 (klikbare docs).
Voor Fase 1/2 niet nodig. Verwijs Claude Code GERICHT naar het relevante stuk,
laat het niet de hele repo doorploegen.
- _reference/mxlint-cli ........... hoe `export` (model->YAML) en `lint` werken
- _reference/mxlint-extension ..... hoe zij het in Studio Pro doen (o.a. klikbare docs)
- _reference/mxlint-rules ......... de Rego-regels + metadata (# METADATA-blok; LET OP CRLF)

## IDEEEN-PARKEERPLAATS (later wegen, niet vergeten)
- Ask-Maia DIRECTE injectie (als ooit een Maia-extensie-API blijkt te bestaan).
- (Voeg nieuwe ideeen hier toe zodat ze de lopende fase niet onderbreken maar
  ook niet verloren gaan.)
