# CLEVR ACR — Projectoverdracht (eindstand juni 2026)

> **Hoe te gebruiken:** plak dit document in een nieuw gesprek met Claude (of geef het aan Claude Code) om naadloos verder te werken aan de CLEVR ACR-extensie. Het beschrijft wat het project is, waar het staat, hoe het werkt, en welke werkwijze het draagt. Lees het naast **`clevr-acr-shell-status.md`** (het levende kompas, met alle meet- en beslis-details) en **`clevr-acr-shell-spec.md`** (het datacontract).

---

## 1. Wat is dit project?

De **CLEVR ACR**-extensie is een Mendix Studio Pro 11-extensie (een "ACR shell") die lint-bevindingen ("improvements") in ACR-stijl toont: zes categorieën (Security, Reliability, Performance, Architecture, Maintainability, Project hygiene), severities Minor < Major < Critical < Blocker, klikbare filters, exclusions met reden, manual checks, en een exporteerbaar HTML-rapport.

**Kernidee:** een *aggregator* van model-leesbare bevindingen — mxcli's eigen lint + CLEVR's eigen gekalibreerde regels + CLEVR-context — **geen vervanger** van ACR of de live Studio Pro-analyse.

**Vereiste:** Mendix 11+ (ExtensionsAPI 11.10 / .NET 10). Werkt niet op Mendix 10.

---

## 2. BELANGRIJKSTE WIJZIGING t.o.v. eerdere overdrachten: mxlint is VOLLEDIG verwijderd

Het project draaide ooit op **twee** engines (mxcli + mxlint.com/Rego) en las een **modelsource-YAML-export**. Dat is allemaal weg. **Eindstand: één engine, Apache-2.0 — mxcli.**

- **Geen mxlint** meer: geen binary, geen download, geen bootstrap-stap, geen Rego, geen `modelsource/`-export. Alle regels lezen via **mxcli** (catalog/describe/lint/.mpr). Geverifieerd: **0 actieve `modelsource/`-lezers**.
- **mxcli** is nu **v0.12.0** (de installer haalt 'latest'; oudere docs noemen v0.11.0 — verouderd).
- Databronnen (allemaal mxcli):
  - `mxcli lint -p "<app>.mpr" --format json` — mxcli's eigen ~60 regels (de generieke bevindingen).
  - **CATALOG** (SQLite, `mxcli -c "SELECT … FROM CATALOG.*"`) — entity/attribute/module/microflow/constants/associations-metadata. Snel (prebuilt `catalog.db`, ~0,4s/query). **Bekende grenzen:** `ATTRIBUTES.Length=0` voor álle strings (lengte niet uitleesbaar), user-role↔module-role niet als tabel.
  - **`describe <type> <name>`** — rendert een element als MDL (de enige bron voor microflow-control-flow/expressies). **~1s per describe** (zie §6).
  - `describe projectsecurity` / `describe userrole <r>` / `project-tree` — security-config (de YAML-export vervangen).

---

## 3. Architectuur (hybride C# + web, ongewijzigd in opzet)

- **C#-backend** (.NET 10 DLL, in-process in Studio Pro) draait mxcli via `Process.Start`, normaliseert naar één `Violation`-contract (zie spec §2), en stuurt via de WebView2 message-bus naar:
- **Web-render-laag** (JS + HTML/CSS in `wwwroot/`): pure presentatie — filters, categorie-display-mapping, telkaarten, rapport-HTML.
- **Splitsing:** regel-logica + normalisatie in C#; de web-laag toont alleen. UI-term = "Improvements", intern type = "Violation".
- **WebView2-detail:** `PostMessage` op de UI-thread; zwaar werk via `Task.Run`, terug-marshallen via de gecaptureerde `SynchronizationContext`.
- **Debug-log:** `<project>\.clevr-acr\mxlint-debug.log` (interne naam ongewijzigd; Nederlands).

### Codebase (`C:\Apps\clevr-acr-shell`)
- **`csharp-spike/`** — de extensie (IO + bedrading): `SpikeDockablePaneViewModel` (knoppen + message-bus), `AcrScanService` (orkestratie + streaming), `MxcliCatalogService` / `MxcliDescribeService` / `MxcliSecurityService` (de drie providers), `ProcessRunner`, `ReportExporter`, `ExclusionStore`, `ManualCheckStore`, `wwwroot/` (index.html + main.js), `rules.json`, `manifest.json`. NuGet: YamlDotNet (16.2.0, nog gebruikt door deprecated backup-code), Mendix.StudioPro.ExtensionsAPI (11.10, niet meegedeployd). `MxlintScanService` + de YAML/Page-readers blijven als **deprecated backup-code**, nooit aangeroepen.
- **`csharp-normalizer/`** — pure, dependency-vrije .NET-library: `Violation`, `RuleRegistry`, `Fingerprint`, de normalizers, `CatalogRules`, `ProjectSecurityParser`, `ExpressionRules`/`MicroflowStructureRules`/`DescribeEntityRules`/`DescribeMicroflowExpressions`, + **232 unit-tests** (groen).
- **`dist/CLEVR-ACR-extension/`** — het deelbare eindgebruikerspakket (installer + README + `clevracr/`-payload). Gebouwd door `Build-Package.ps1`.
- **`_reference/`** — uitgepakte mxlint-broncode (historisch; mxlint is uit het product).

---

## 4. De regels — eindstand (alle live geverifieerd op TRB)

**12 regels gemigreerd naar mxcli (7 catalog + 5 describe), GEEN export meer:**

- **Catalog-route (7)** — `MxcliCatalogService` → pure `CatalogRules`, uit `CATALOG.*`:
  MAINT-007 (microflow-grootte), MAINT-010 (default value), MAINT-014 (aantal modules), SEC-011 (exposed constants), PERF-001 (inherit Administration.Account), SEC-007 (System-associatie), SEC-009 (hash-algoritme).
- **Describe-route (5)** — `MxcliDescribeService` → pure regels, uit `describe microflow/entity`:
  MAINT-008 (complexity zonder annotatie), MAINT-009 (geneste ifs), REL-001 (redundante empty-string-check), REL-002 (incomplete empty-string-check), MAINT-013 (default ReadWrite-access). **MAINT-006** (redundante boolean) verhuisde óók van de YAML-route naar deze describe-sweep (deepscan); grondwaarheid TRB = **104** (vollediger dan de oude 94 — vangt nu ook variabele-toewijzingen).
- **Security-route** — `MxcliSecurityService` (synthetiseert een equivalente security-YAML uit `describe projectsecurity`/`userrole` + `project-tree`, voedt de bestaande pure `ProjectSecurityParser`):
  SEC-008 (admin = MxAdmin), SEC-005 (anon create op persistente entiteit), SEC-010 (per-userrole check-security), MAINT-005 (≤1 module-rol/module), + SEC-004 (guest access aan). SEC-008 + SEC-005 draaien in de **snelle** scan (≤1 `describe userrole`); SEC-010 + MAINT-005 in de **deepscan** (alle 9 userrole-describes).

**4 onderwerpen GEDEFEREERD aan mxcli's eigen regels** (mxcli dekt ze al; onze emissie uit om dubbeltelling te voorkomen):
- MAINT-011 (te veel persistente entiteiten) → mxcli **MPR003**
- PERF-002 (te veel virtuele attributen) → mxcli **CONV017**
- MAINT-012 (validatieregels) → mxcli **ACR_ENT_VALRULES / CONV015**
- commit-in-loop (PERF) → mxcli **CONV011**

De claim-tabel + twee tripwire-tests bewaken de cross-engine-ontdubbeling (geen check verschijnt dubbel).

---

## 5. Twee scan-modi + streaming

- **Snelle scan (Scan-knop):** mxcli lint + catalog-route + security (SEC-008/005) + manual checks. ~17s warm / ~55s koud (eerste scan = mxcli lint herbouwt `catalog.db`). GEEN describe-sweep.
- **Deepscan-knop:** alles van de snelle scan **+** de describe-sweep (MAINT-008/009, REL-001/002, MAINT-013, MAINT-006) **+** security SEC-010/MAINT-005. Minuten tot, voor grote apps, een uur — zie §6.
- **Streaming (beide modi):** `AcrScanService.RunScanStreaming(deepScan, emit)` post findings in **batches** — eerst de FAST-batch (lint+catalog+security, ~seconden), daarna bij deepscan de describe-findings **per chunk** (`DescribeStreamChunkSize=30` elementen, ~11-15s warm / ~25-37s koud per chunk) met voortgang ("microflows 80/472"). De UI (`main.js`) toont een **voortgangsbanner** + markeert de totalen als **"Total (so far)"** tot de laatste batch (`final=true`) — een tussenstand is nooit als eindstand te lezen. Een chunk die minder teruggaf dan gevraagd → **rode LUID-waarschuwing** (geen stille minder-findings). **Bewezen:** de som van alle batches is byte-identiek aan de niet-gestreamde scan (2865 = 2865 op TRB).

---

## 6. De deepscan-traagheid (onderzocht tot de wortel) — streaming is de mitigatie, geen warme mxcli-modus

`describe` kost **~1s per element** (warm ~0,5-0,64s, koud ~1,1s), gemeten en gecontroleerd. Dit is een **harde mxcli-compute-vloer**, geen model-load-artefact: binnen één `-c`-sessie (model al geladen) kost elke describe nóg ~1s. Apples-to-apples gemeten (zelfde warmte, .NET): chunks-van-200 (301s) ≈ één grote `-c` (310s) ≈ `exec`-scriptbestand (346s) — **één sessie is NIET sneller**. mxcli v0.12.0 heeft persistente modi (REPL, `exec`, `lsp`, `serve`) maar die houden het model alleen warm binnen een sessie = de model-load die chunking al amortiseert; ze breken de per-describe-vloer niet. Er is **geen bulk-describe en geen expressie-export** in mxcli.

**Extrapolatie (lineair):** TRB (472 microflows) = ~4:41 warm / ~10 min koud; 2000 microflows ≈ 20-45 min; 5000 ≈ 51-112 min. → **streamen is voor de deepscan noodzakelijk** (de tijd draaglijk maken). De enige echte orde-winst zou de verwijderde bulk-export zijn (~19s voor alles) — heropent de mxlint-beslissing; niet aanbevolen.

---

## 7. DRIE GEPARKEERDE HERACTIVERINGEN (code staat klaar als backup)

1. **SEC-006** (unlimited string editable door anoniem) — **gedeprecateerd**. mxcli leest de string-MAX-lengte niet terug (`CATALOG.ATTRIBUTES.Length=0` voor álle strings; `describe entity` rendert óók `Length:200` als `String(unlimited)` — bevestigd, ook na verse catalog-rebuild). `CatalogRules.AnonymousEditableUnlimitedString` + de leescode blijven als ongebruikte backup. **Reactiveer zodra mxcli string-Length (String(N) vs String(unlimited)) betrouwbaar terugleest.**
2. **MAINT-015** (inline-style) + **REL-003** (alt-text) — **gedeprecateerd**. mxcli ontsluit `WIDGETS.Style`/alt-text niet en `describe page` is lossy. `PageRules` + `PageYamlReader` blijven backup. **Reactiveer zodra mxcli WIDGETS Style/alt-text ontsluit.**
3. **Severity-kalibratie** — de hoog-volume regels op TRB: **MAINT-007 = 30**, **MAINT-008 = 129**, **MAINT-010 = 592**. Hun severity/default-demping is nog niet gekalibreerd tegen ruis; te bezien met Michel (mogelijk lager standaard-severity of een drempel). De getallen zijn echt (geverifieerd), niet verzonnen.

**Bekende cosmetische opruiming (geen functie-impact):** drie stale comments noemen nog "export"/`MxlintViolations` als scan-uitkomst — `SpikeDockablePaneViewModel.cs` (~r.87-88 en ~r.642) en `main.js` (~r.1366). De bijbehorende code is dood/backup en wordt nooit aangeroepen (geverifieerd: de knoppen posten alleen `RunFullScan`/`RunDeepScan` → `AcrScanService`, nooit `RunMxlintScan`/`MxlintViolations`). Mag opgeruimd worden bij gelegenheid; kost één rebuild+repack.

---

## 8. Kernwaarde & werkwijze (BELANGRIJK — dit draagt het project)

**"Een feature/regel die draait is niet per se correct."** De discipline die elke regel betrouwbaar maakte:

1. **Toon EERST de echte data + de grondwaarheid op het testproject, dán bouwen, dán verifiëren.** Nooit categorieën/severities/tellingen verzinnen.
2. **Verdachte uitkomsten (een "0", een onverwacht getal, een 2× sneller resultaat) = signaal om door te graven, niet te accepteren.** Meermaals bleek dat een databron-fout (lege CATALOG-kolom, stale catalog, een warmte-confound bij timing) — niet de waarheid.
3. **"Werkt in mijn probe" ≠ "komt door de echte scan-pijplijn".** Meet via de echte code-paden (instrumented driver tegen de echte providerklassen), niet losse bash-experimenten — bash-arg-truncatie gaf ooit een vals "0 findings".
4. **Stop bij afwijking; rapporteer per fase.** Jaag een verwacht getal niet blind na — als de describe-route 104 i.p.v. 94 geeft, benoem of het scope/metriek/bug is.

**Rolverdeling:** chat-Claude = sparringpartner/prompts. **Claude Code** (op `C:\Apps\clevr-acr-shell`) = bouwt & verifieert. Michel test in Studio Pro en plakt resultaten terug.

**Deploy:** niet meer met de hand kopiëren. `Build-Package.ps1` bouwt + assembleert `dist/CLEVR-ACR-extension(.zip)`; `Install-ClevrAcr.ps1` installeert in `<project>\extensions\clevracr` en **haalt mxcli automatisch op** (GitHub-release, sha256-geverifieerd, na één Y/n-bevestiging). Daarna Studio Pro herstarten; fixes gelden voor NIEUWE scans.

---

## 9. Security-inventaris (voor de security officer)

- **Talen/runtimes:** C# op .NET 10 (backend-DLL's, in-process), JavaScript + HTML/CSS (web-render-laag in WebView2).
- **NuGet:** `YamlDotNet` 16.2.0 (alleen nog door deprecated backup-code), `Mendix.StudioPro.ExtensionsAPI` 11.10 (door Studio Pro geleverd, niet meegedeployd).
- **Externe binary:** `mxcli.exe` (v0.12.0, **Apache-2.0**) — door de installer **auto-gedownload** van de officiële GitHub-release, **sha256 + bytegrootte geverifieerd** vóór gebruik, opgeslagen in `%LOCALAPPDATA%\clevr-acr\mxcli\`. **Geen mxlint meer** (de vorige supply-chain-zorg over de auto-gedownloade mxlint-binary is vervallen).
- **Aanbeveling:** periodiek `dotnet list package --vulnerable --include-transitive`; vastleggen welke mxcli-versie wordt gedownload.

---

## 10. Eerste prompt-suggestie voor een vervolg-sessie

> Ik neem CLEVR ACR over (zie de overdracht + `clevr-acr-shell-status.md` + `clevr-acr-shell-spec.md`). Eindstand: mxlint volledig weg, alles via mxcli (Apache-2.0), 12 regels gemigreerd, twee scan-modi met streaming. Drie geparkeerde heractiveringen (SEC-006 string-Length; MAINT-015/REL-003 WIDGETS Style/alt-text; severity-kalibratie MAINT-007/008/010). Voordat we iets bouwen: bevestig de huidige feiten — build + 232 tests groen, en `mxcli --version` op de laptop. Geen bouwactie; eerst de stand verifiëren.
