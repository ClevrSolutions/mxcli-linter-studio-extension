# CLEVR ACR Shell — functionele specificatie & datacontract

Doel: een CLEVR Studio Pro-extensie die lint-violations samenvoegt en presenteert zoals de oude ACR —
zelfde categorieën, exclusions met reden, en een ACR-stijl rapport.

Dit document is het KOMPAS voor wie de extensie bouwt (Claude Code / Codex).
De bouwer codeert tegen dit contract; hij verzint formaten of categorieën niet zelf.
De mens (Michel/CLEVR) bewaakt de functionele standaard: categorieën, reporting,
exclusions, en de regel "een rule telt pas als verified".

---

## ⚠️ EINDSTAND (juni 2026) — lees dit eerst; het gaat boven de historische delen hieronder

Dit document is geschreven in de **twee-engine-fase** (mxcli .star + mxlint .rego). **De eindstand is anders.**
Het **datacontract is nog steeds geldig en leidend**; alleen de engine-/fasebeschrijvingen zijn historisch.

**WAT NOG KLOPT (gebruik dit als contract):**
- **§1** ACR-categorieën (zes) & severities (Minor<Major<Critical<Blocker).
- **§2** Het genormaliseerde `Violation`-formaat — `kind: "acr" | "generic"`, alle velden, `documentType`-canonicalisatie.
- **§3** Exclusions + de fingerprint-strategie (`sha1(ruleId|documentQualifiedName|elementName)`).
- **§4** Rule registry-concept + de gouden regel ("één ruleId = één bron") + de **"telt pas als verified"**-discipline + cross-engine-ontdubbeling (claim-tabel).
- **§5** Rapport (per-regel-groepering, herkomst-badge, display-mapping van mxcli-categorie → ACR-categorie).

**WAT IS ACHTERHAALD (historisch — zie `CLEVR-ACR-overdracht.md` + `clevr-acr-shell-status.md` voor de echte stand):**
- **mxlint/.rego is VOLLEDIG verwijderd.** Eén engine: **mxcli** (Apache-2.0, v0.12.0). Geen tweede engine, geen Rego, geen `modelsource/`-YAML-export, geen mxlint-binary/-download/-bootstrap.
- **`source`-waarden:** in de praktijk alleen `"clevr-acr"` (ACR-regels) en `"mxcli"` (generiek). `"mxlint"` komt niet meer voor (de UI-takken ervoor blijven als dode, onschadelijke guards).
- **`packs.json`** met een mxlint-pack is achterhaald; alleen mxcli's bundled regels zijn de generieke bron.
- **De overlap-/onderdrukkingstabel (§4, 6 mxlint-twins)** is vervangen door de **claim-tabel + twee tripwire-tests** (mxcli-vs-CLEVR-ontdubbeling, intern).
- **De databronnen (§3-historisch, §7, §9)** zijn nu allemaal mxcli: `lint --format json`, `CATALOG.*` (SQLite), `describe <type>`, `describe projectsecurity`/`userrole`, `project-tree`. GEEN modelsource/YAML.
- **De MVP-/fasebouwvolgorde (§6, §9)** is afgerond; het product is live.
- **Regels:** 12 gemigreerd (7 catalog + 5 describe), 4 gedefereerd aan mxcli's eigen regels, security via describe. Twee scan-modi (snel/diep) met **progressief streamen** van findings. Drie geparkeerde heractiveringen (SEC-006; MAINT-015/REL-003; severity-kalibratie). Details in de overdracht + status.

---

## 0. Architectuur (samenvatting) — HYBRIDE C# + TypeScript

GECORRIGEERD na verkenning, BEWEZEN door de C#-spike (juni 2026): een pure
web/TypeScript-extensie kan GEEN extern proces starten (de web-UI draait in een
sandboxed webview zonder procesexecutie-API). Een extern CLI-proces draaien moet
via een C#-backendcomponent (.NET 10, Process.Start). De C#-backend HOST bovendien
de web-UI (Studio Pro's ingebouwde webserver, WebServerExtension) en communiceert
ermee via de WebView-message-bus (IWebView.PostMessage ⇄ window.chrome.webview),
NIET via studioPro.ui.messagePassing. De extensie is dus hybride:

    CLEVR ACR Shell (Studio Pro extension — C#-host + web-UI in de webview)
       │
       ├─ C#-backend (.NET 10 .dll, in-process)       ← procesexecutie + UI-hosting
       │    ├─ DockablePaneExtension  → registreert de pane (Id + Open())
       │    ├─ MenuExtension          → opent de pane via IDockingWindowService.OpenPane
       │    ├─ WebServerExtension     → serveert de ui-render-bundle uit wwwroot
       │    ├─ engine: mxcli   → Process.Start "mxcli lint --format json" (.star)
       │    ├─ engine: mxlint  → Process.Start "mxlint-cli lint" (.rego, OPA)
       │    │                     output via mxlint.yaml (jsonFile / xunitReport), GEEN SARIF
       │    └─ stuurt violations naar de webview via de WebView-message-bus
       │       (IWebView.PostMessage  ⇄  window.chrome.webview)
       │
       └─ web-UI (de Fase 1 ui-render-bundle, in de door C# gehoste webview)  ← de UI
            ├─ normalizer      → mapt engine-output naar EEN Violation-formaat (sectie 2)
            ├─ rule registry   → bewaakt: elke ruleId een engine (sectie 4)
            ├─ exclusions      → suppress met reden + fingerprint (sectie 3)
            └─ report          → ACR-stijl HTML/CSV/JSON (sectie 5)

TWEE GESCHEIDEN MESSAGING-BRUGGEN (niet verwarren — dit bepaalt de architectuur):
- WebView-message-bus (window.chrome.webview ⇄ IWebView): C# ↔ web-content IN een
  door C# gehoste webview. DIT is de brug tussen engine-output en de UI.
- studioPro.ui.messagePassing (web ↔ web): alleen tussen web-entrypoints BINNEN een
  web-extensie. C# kan hier NIET in.
Gevolg: C# kan NIET in het losse Fase 1-TS-paneel (studioPro.ui.panes) praten. Het
violations-paneel wordt daarom in Fase 2 C#-GEHOST; het serveert de bestaande Fase 1
ui-render-bundle als content. De render-laag (consumeert Violation[] via een
ViolationSource) hoeft niet te wijzigen — alleen de databron verandert.

Niet afhankelijk worden van de MxLint-UI; alleen de engine/CLI gebruiken, alles in
het eigen CLEVR-paneel tonen.

LET OP twee engines, twee tools (niet verwarren):
- mxcli (mendixlabs/mxcli): de .star/Starlark-engine, ondersteunt --format sarif.
- mxlint-cli (github.com/mxlint/mxlint-cli): de .rego/OPA-engine, output via
  mxlint.yaml (jsonFile + xunitReport XML), GEEN SARIF en GEEN --format-vlag.

BEWEZEN door de spike (Studio Pro 11.10, juni 2026): een C#-extensie startte via
Process.Start het commando `cmd /c echo test` (exitCode 0, stdout "test") en stuurde
de output via de WebView-message-bus naar het pane, dat het als ruwe tekst toonde.
Procesexecutie én message passing werken dus. Bevestigde feiten (zie csharp-spike/):
- Runtime: .NET 10 (de ExtensionsAPI 11.10.0 vereist net10.0; net8.0 wordt NIET
  ondersteund — NU1202).
- Pane openen: er is GEEN ViewMenuCaption in 11.10; de route is een MenuExtension die
  IDockingWindowService.OpenPane(paneId) aanroept (pane verschijnt onder Extensions).
Fase 1 (schil + hardcoded JSON) hing hier niet van af en is al af.

---

## 1. ACR-categorieën & severities (vastgelegd — niet zelf verzinnen)

Categorieën (exact deze zes, zoals ACR ze toont):
- Project hygiene
- Maintainability
- Performance
- Architecture
- Reliability
- Security

Severities (oplopend): Minor < Major < Critical < Blocker.
(Mapping vanuit engine-severity: een .star/.rego-regel declareert zijn ACR-severity
in metadata; de shell vertaalt NIET zelf, maar leest het uit de regel-metadata.)

LET OP — scope: deze zes categorieën en vier severities gelden voor ACR-regels
(`kind: "acr"`, sectie 2). Generieke best-practice-regels (`kind: "generic"`,
bundled mxcli / mxlint.com) behouden hun EIGEN engine-categorie en -severity en
zijn NIET beperkt tot deze lijst.

---

## 2. Genormaliseerd Violation-formaat (het datacontract)

Beide engines worden naar exact dit JSON-object gemapt. Dit is de enige vorm die
de UI, exclusions en het rapport kennen.

Er zijn TWEE soorten violations, onderscheiden door `kind` (de shell toont ALLE
beschikbare regels, maar houdt de ACR-identiteit scherp gescheiden):
- `kind: "acr"`     — een CLEVR ACR-regel. Heeft `acrCode`; `category`/`severity`
                      zijn exact één uit sectie 1 en komen UIT het rule registry
                      (sectie 4), niet uit de engine.
- `kind: "generic"` — een generieke best-practice-regel uit een engine-pack (bundled
                      mxcli of mxlint.com). GEEN `acrCode`; `category` en `severity`
                      zijn de EIGEN waarden van de engine (vrije tekst, NIET beperkt
                      tot sectie 1). `source` toont de herkomst in de UI.

Verplicht (beide soorten): ruleId, kind, source, category, severity, documentType,
documentQualifiedName, reason, fingerprint. ACR-only verplicht: acrCode.
Optioneel: elementName, suggestion, documentationUrl, documentId.

`documentId` is de stabiele Mendix document-GUID (uit de engine, bv. mxcli). Bruikbaar
voor navigatie (open het document) en als robuustere fingerprint-basis later. Optioneel
omdat niet elke engine 'm levert.

`source`-waarden: `"clevr-acr"` | `"mxcli"` (bundled best practices) | `"mxlint"`
(mxlint.com). De `engine`-property (`"star"`|`"rego"`) blijft ALLEEN voor debug; de
UI toont 'm nooit. Voor generieke regels toont de UI in plaats daarvan `source`, zodat
de gebruiker een MPR-regel niet voor een ACR-regel aanziet.

ACR-regel (`kind: "acr"`):
```json
{
  "ruleId": "CLEVR-PERF-014",          // CLEVR-eigen stabiele id (sectie 4)
  "kind": "acr",
  "source": "clevr-acr",
  "acrCode": "MicroflowDbActionsAtEnd",// originele ACR-rulenaam (traceerbaarheid)
  "engine": "star",                    // ALLEEN debug
  "category": "Performance",           // exact één uit sectie 1 (uit registry)
  "severity": "Major",                 // exact één uit sectie 1 (uit registry)
  "documentType": "Microflow",
  "documentQualifiedName": "TRB.SUB_X",
  "elementName": "",
  "reason": "Database action is not at the end of the microflow.",
  "suggestion": "Move commit/change actions to the end.",
  "fingerprint": "sha1:...",           // stabiele hash (sectie 3)
  "documentationUrl": "https://.../CLEVR-PERF-014",
  "documentId": "c0ffee00-1234-5678-9abc-def012345678" // optioneel: Mendix document-GUID
}
```

Generieke regel (`kind: "generic"`, bundled mxcli of mxlint):
```json
{
  "ruleId": "mxcli:MPR-0042",          // stabiele ENGINE-regel-id (geen CLEVR-id)
  "kind": "generic",
  "source": "mxcli",                   // getoond in de UI (herkomst)
  "engine": "star",                    // ALLEEN debug
  "category": "MPR",                   // EIGEN engine-categorie (vrije tekst, niet sectie 1)
  "severity": "warning",               // EIGEN engine-severity (vrije tekst, niet sectie 1)
  "documentType": "Microflow",
  "documentQualifiedName": "TRB.SUB_Y",
  "elementName": "",
  "reason": "...",
  "fingerprint": "sha1:...",
  "documentId": "..."                  // optioneel: Mendix document-GUID
  // GEEN acrCode; suggestion/documentationUrl optioneel
}
```

Exclusions (sectie 3) en de fingerprint werken identiek voor BEIDE soorten
(de fingerprint gebruikt ruleId — voor generiek de engine-regel-id).

### documentType — canonieke waarden (BEIDE engines mappen hiernaartoe)
De normalizer canonicaliseert de engine-`documentType` naar exact deze PascalCase-vorm,
zodat de UI consistent groepeert/filtert en .star/.rego naar één vorm convergeren
(bv. mxcli `"entity"` → `"Entity"`, `"microflow"` → `"Microflow"`). Canonieke lijst:

    Entity, Association, Microflow, Nanoflow, Page, Snippet, Layout, Module,
    Enumeration, ProjectSecurity, ModuleSecurity, Rule, Constant, ScheduledEvent,
    PublishedRestService, ConsumedRestService, MessageDefinition, Image, Document,
    JavaAction, JavaScriptAction

De lijst is uitbreidbaar. Een ENGINE-waarde die hier (case-insensitief) niet in staat,
wordt met een hoofdletter-begin doorgegeven (bv. `"widget"` → `"Widget"`) en is als
afwijking te signaleren, zodat een nieuw/onbekend type de groepering niet stil breekt.

---

## 3. Exclusions — suppress met reden (kritieke ontwerpbeslissing)

Opslag: `$project/.clevr-acr/exclusions.json` (in de projectmap, mee te committen
zodat het team dezelfde exclusions deelt).

```json
{
  "exclusions": [
    {
      "ruleId": "CLEVR-PERF-014",
      "fingerprint": "sha1:ab12...",
      "reason": "Bewuste bulk-migratie, wordt apart afgehandeld in EPIC-123.",
      "excludedBy": "michel@clevr.nl",
      "date": "2026-06-09",
      "expiry": "2026-12-31"               // optioneel; daarna weer actief
    }
  ]
}
```

### Fingerprint-strategie (het subtiele deel — goed doordenken)
Doel: een uitgesloten violation blijft uitgesloten bij irrelevante modelwijzigingen,
maar een NIEUWE/ANDERE violation wordt NIET per ongeluk mee-onderdrukt.

Aanbevolen fingerprint = sha1 van de stabiele identiteit, NIET van vluchtige tekst:
    fingerprint = sha1( ruleId + "|" + documentQualifiedName + "|" + elementName )

Bewust WEL opnemen: ruleId, het gekwalificeerde document, en het subelement
(widget/attribuut) — dat is de identiteit van "wat" wordt geflagd.
Bewust NIET opnemen: de `reason`-tekst, drempelgetallen, of tellingen — die
veranderen bij elke modelaanpassing en zouden de exclusion laten "weglekken".

Gevolgen die de bouwer expliciet moet afhandelen:
- HERNOEMEN van een element verandert documentQualifiedName → fingerprint verschuift
  → exclusion vervalt en de violation komt terug. Dit is CORRECT (het is feitelijk
  een ander element), maar moet in de UI zichtbaar zijn als "exclusion no longer
  matches" i.p.v. stil verdwijnen.
- Een exclusion die NERGENS meer matcht (stale) moet de UI tonen als "stale
  exclusion" zodat het team 'm kan opruimen — niet stil negeren.
- Model-drift (element toegevoegd ná een scan) is GEEN exclusion-zaak maar een
  nieuwe violation; toon 'm gewoon. (cf. Tabsnippet_Editable_AdL-casus.)

---

## 4. Rule registry — ACR-identiteit afdwingen, generieke regels toelaten

Het registry kent twee rollen:
- **(A) ACR-regels** — expliciet geregistreerd, met afgedwongen gouden regel. Worden
  `kind: "acr"`.
- **(B) Generieke packs** — aangevinkte best-practice-packs (bundled mxcli, mxlint.com)
  waarvan de regels ONGEWIJZIGD (met eigen categorie/severity) worden doorgelaten als
  `kind: "generic"`.

### (A) ACR-regels — `$project/.clevr-acr/rules.json`
Eén CLEVR-ruleId heeft precies ÉÉN source of truth (één engine). Dit wordt AFGEDWONGEN.
Elke entry bindt een engine-regel aan ACR-metadata. `engineRuleKey` is de identifier
waarmee de engine die regel rapporteert — hiermee herkent de normalizer welke
engine-violation een ACR-regel is.
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

De shell MOET bij het laden valideren (de gouden regel, ongewijzigd):
- geen twee entries met dezelfde ruleId,
- geen ruleId die zowel een actieve .star als .rego heeft,
- geen twee ACR-entries die dezelfde `engineRuleKey` claimen,
- alleen regels met `"status": "verified"` tellen mee in het ACR-hoofdrapport;
  `todo`/`approximate` worden apart getoond (niet als harde violation gemengd).

`status` waarden: verified | needs-threshold | approximate | todo | out-of-reach.
(Dit is de "een rule telt pas als verified"-discipline, in het datamodel verankerd.)

`ruleId`-schema: `CLEVR-<CAT>-<NNN>`, waarbij CAT de ACR-categorie codeert
(MAINT = Maintainability, HYG = Project hygiene, SEC = Security, PERF = Performance,
ARCH = Architecture, REL = Reliability), per categorie oplopend genummerd vanaf 001.
`acrCode` is de originele rule-naam (traceerbaarheid); voor onze eigen .star-regels is
dat het .star rule-id (= engineRuleKey), behalve waar een beschrijvende naam bestaat
(bv. ACR_ENT_ATTRS → "EntityAmountAttributes").
Severity-bron: de ACR-grondwaarheid (count-export) wint boven eerdere aannames — bv.
ACR_ENT_ATTRS is Minor (Maintainability), niet Major. Een regel zonder baseline-rij
(0 violations op TRB, zoals de vier Security-regels) krijgt severity `"TODO-confirm"`:
niet verzinnen, wel `verified` (de regel werkt).

### (B) Generieke packs — `$project/.clevr-acr/packs.json`
Lijst van aangevinkte packs; hun regels komen ONGEWIJZIGD binnen als `kind: "generic"`
en worden NIET per stuk vooraf geregistreerd (het zijn er te veel). Aan/uit per pack:
```json
{
  "packs": [
    { "source": "mxcli",  "enabled": true, "label": "mxcli best practices" },
    { "source": "mxlint", "enabled": true, "label": "mxlint.com" }
  ]
}
```

### Normalizer-mapping (beide soorten) — draait in C# (sectie 9)
Voor ELKE binnenkomende engine-violation:
1. Zoek de engine-regel-key op in de ACR-registry (A):
   - **MATCH + status = verified** → `kind: "acr"`: neem `acrCode` + ACR-`category`/
     `severity` UIT de registry (niet uit de engine), `source: "clevr-acr"`,
     `ruleId` = de CLEVR-id.
   - **MATCH + status ≠ verified** → ACR-regel "in ontwikkeling": apart tonen
     (sectie 5), NIET als harde ACR-violation meetellen.
   - **GEEN match** (regel uit een ingeschakeld pack) → `kind: "generic"`: behoud de
     EIGEN engine-`category` en -`severity`, `source` = het pack (`"mxcli"`/`"mxlint"`),
     `ruleId` = de engine-regel-id. GEEN acrCode.
2. Bereken `fingerprint` (sectie 3) en pas exclusions toe — voor BEIDE soorten.

PRECEDENTIE / ONTDUBBELING (bevestigd besluit): de ACR-registry CLAIMT de check.
Als een `engineRuleKey` in de ACR-registry staat, wordt die violation NOOIT óók als
generieke regel toegevoegd — ook niet als de bundled mxcli/mxlint-pack dezelfde check
bevat. Een geclaimde-en-verified check verschijnt één keer als ACR-regel; een
geclaimde-maar-niet-verified check verschijnt één keer in de "in ontwikkeling"-sectie.
Alleen NIET-geclaimde pack-regels worden generiek. Dit voorkomt dubbele tellingen en
dubbel werk: elke check verschijnt precies één keer, met ACR als bovenliggende bron.

### Engine-precedentie bij overlap (Fase 3B — meerdere engines in één overzicht)
Volgorde: **mxcli > ACR > mxlint**. Checkt meer dan één engine (ongeveer) hetzelfde,
dan wint de hoogste in deze volgorde — mxcli (Mendix' eigen richting) eerst, dan de
gekalibreerde ACR-regel, mxlint als laatste. De verliezende variant wordt onderdrukt.
BEWUSTE KEUZE: bij overlap toont het overzicht de metadata van de WINNENDE bron, niet
een mengvorm — dit kan de gekalibreerde ACR-metadata vervangen door die van de winnaar.

OVERLAP-TABEL (onderdrukt aan de mxlint-kant; deze 6 mxlint-Rego-regels checken
hetzelfde als bestaande ACR-regels, dus ACR wint en de mxlint-variant wordt gedropt):

| mxlint-regel (rulenumber) | ACR-regel | onderwerp |
|---|---|---|
| AnonymousDisabled (001_0001)        | ACR_SEC_GUEST      | anonieme/guest-toegang |
| DemoUsersDisabled (001_0002)        | ACR_SEC_DEMOUSERS  | demo-gebruikers |
| SecurityChecks (001_0003)           | ACR_SEC_CHECKED    | security-check aan |
| StrongPasswordPolicy (001_0004)     | ACR_SEC_PWPOLICY   | wachtwoordbeleid |
| NumberOfAttributes (002_0002)       | ACR_ENT_ATTRS      | aantal attributen |
| AvoidUsingValidationRules (002_0007)| ACR_ENT_VALRULES   | validatieregels |

De onderdrukking gebeurt deterministisch in de mxlint-normalizer (op rulenumber); de
overige mxlint-regels komen normaal mee als `source: "mxlint"`.

---

## 5. Rapport (ACR-stijl)

ÉÉN overzicht: ALLE improvements (ACR + generiek) verdeeld over de ZES ACR-categorieën
(sectie 1). De HERKOMST blijft altijd zichtbaar (badge per regel + uitsplitsing in de
telkaart), zodat een developer een gekalibreerde ACR-regel nooit voor een
engine-generieke regel aanziet — de scheiding zit nu in de badge, niet in een aparte
sectie.

1. App info (projectnaam, datum, Mendix-versie, # gescande documenten).
2. Telkaart — telt ALLE improvements:
   - per categorie (de zes, sectie 1),
   - per severity (zie severity-weergave hieronder),
   - per HERKOMST: ACR (gekalibreerd) / MxCLI Mxlint / Mxlint.com.
3. Improvements — gegroepeerd per de zes ACR-categorieën (sectie 1) en BINNEN elke
   categorie per regel (groeperingsmodel hieronder). Elke regel toont een
   HERKOMST-badge (ACR / MxCLI / Mxlint.com); ACR-regels tonen daarnaast hun `acrCode`.
4. Exclusions apart zichtbaar — wat is onderdrukt, door wie, waarom, sinds wanneer.
   Geldt voor beide soorten; `kind` blijft zichtbaar.

### Categorie van generieke regels — DISPLAY-mapping (per-regel mxcli-categorie)
Generieke regels behouden INTERN hun eigen engine-categorie (sectie 2: `Violation.category`
= de mxcli-prefix, ongewijzigd). Voor het rapport worden ze in één van de zes
ACR-categorieën geplaatst. De mapping zit in de render-laag; het Violation-contract
verandert niet.

GECORRIGEERD (mapping-bug): map op de **ECHTE per-regel mxcli-categorie**, NIET op de
ruleId-prefix. mxcli geeft per regel een categorie (style/quality/correctness/performance/
…) mee — opvraagbaar via `mxcli lint --list-rules` (die we al ophalen voor de regelnamen;
per ruleId staat er een `Category:`-regel). De prefix was te grof: de `CONV`-prefix bevat
bv. performance- (CONV011 NoCommitInLoop), naming- (CONV001) én quality-regels door elkaar,
waardoor Performance leeg bleef (alleen door de niet-bestaande prefix `PERF` gevoed) en
Reliability door niets werd gemapt.

| mxcli-categorie | ACR-categorie    | reden |
|-----------------|------------------|-------|
| security        | Security         | beveiliging |
| naming          | Project hygiene  | naamgevingsconventies → opgeruimd project |
| style           | Project hygiene  | stijl/conventie → opgeruimd project |
| quality         | Maintainability  | codekwaliteit → onderhoudbaarheid |
| complexity      | Maintainability  | (McCabe-)complexiteit → onderhoudbaarheid |
| maintainability | Maintainability  | direct |
| design          | Architecture     | ontwerp/structuur → architectuur |
| architecture    | Architecture     | architectuur |
| **correctness** | **Reliability**  | **bewuste vertaling: runtime-correctheid (crashes, lege validatie) ≈ betrouwbaarheid** |
| performance     | Performance      | performance |

Onbekende/nieuwe mxcli-categorie → **Maintainability** (fallback, te herzien). Ontbreekt
de mxcli-categorie (bv. `--list-rules` kon niet geladen worden), dan geldt een grove
prefix-fallback (SEC→Security, ARCH/DESIGN→Architecture, PERF→Performance, anders
Maintainability). ACR-regels (`kind: "acr"`) gebruiken hun registry-categorie (sectie 4),
niet deze tabel.

Geverifieerd op TRB (2625 improvements): met deze mapping is **Reliability = 388** en
**Performance = 11** (beide voorheen leeg); de overige verdeling Project hygiene 491 /
Maintainability 1080 / Architecture 252 / Security 403.

### Categorie van mxlint-regels — DISPLAY-mapping (Fase 3B)
mxlint (Rego) levert z'n eigen categorie LETTERLIJK in de failure-message (sectie 2:
`Violation.category` blijft ongewijzigd). Die categorieën wijken af van de zes ACR-namen
en worden voor het rapport gemapt — opnieuw puur in de render-laag, contract ongewijzigd:

| mxlint-categorie | ACR-categorie   | reden |
|------------------|-----------------|-------|
| Security         | Security        | beveiliging |
| Maintainability  | Maintainability | direct |
| Performance      | Performance     | performance |
| **Accessibility**| **Maintainability** | **bewuste keuze: geen eigen ACR-categorie → onderhoudbaarheid** |
| Microflows       | Maintainability | microflow-structuur → onderhoudbaarheid |
| Complexity       | Maintainability | complexiteit → onderhoudbaarheid |
| Error            | Reliability     | runtime-fouten ≈ betrouwbaarheid |

Onbekende mxlint-categorie → **Maintainability** (fallback). Op TRB komen voor:
Maintainability, Accessibility (→ Maintainability), Security.

### Severity-weergave
- ACR-regels: hun ACR-severity (Minor/Major/Critical/Blocker, uit het registry, sectie 4).
- Generieke regels: LETTERLIJK de mxcli engine-severity (error/warning/info/hint) —
  NIET vertaald naar een ACR-severity (geen verzonnen severity). Beide worden als
  severity-chip getoond op dezelfde manier.
- Een ACR-severity buiten de vier (bv. `TODO-confirm`) valt onder "Te bevestigen".

### GROEPERINGSMODEL — PER REGEL, niet per instantie (zoals ACR's "Summary per rule"):
- Elke regel verschijnt PRECIES ÉÉN keer als regelvermelding (regel-id/acrCode,
  herkomst, severity, totaal aantal improvements).
- Daaronder, uitklapbaar/genest, ALLE instanties die aan die regel voldoen
  (document + elementName + reason per instantie).
- Een regel met 12 instanties is één regelvermelding met 12 geneste gevallen —
  NIET 12 losse regelvermeldingen. Een regel zonder instanties wordt niet getoond.

Exports: HTML (primair, ACR-look), CSV en JSON (voor CI/pipeline). De telkaart telt
alle improvements; de herkomst-uitsplitsing houdt zichtbaar wat gekalibreerd ACR is
(`verified`) en wat engine-generiek — zodat de cijfers eerlijk én volledig zijn.

---

## 6. MVP-bouwvolgorde (eerst contract, dan integraties)

1. Extension skeleton in Studio Pro (TypeScript / Web Extensibility API).
2. CLEVR ACR-paneel met HARDCODED voorbeeld-JSON in ACR-layout
   (bewijst de UI/het rapport vóór enige engine-integratie).
3. Integreer engine 1: `mxcli lint --format json` → normalizer → paneel.
   VERIFIEER eerst dat die JSON-output bestaat en stabiel is (zie sectie 7).
4. Integreer engine 2: MxLint/Rego-CLI → normalizer → paneel.
   VERIFIEER eerst dat een Rego-flowgraaf-regel haalbaar is (zie sectie 7).
5. Exclusions (lezen/schrijven .clevr-acr/exclusions.json + fingerprint-logica).
6. Rapport-export (HTML/CSV/JSON).
7. Pas DAARNA: regels migreren volgens Green/Blue/Orange/Red.

---

## 7. Aannames — status na verkenning (juni 2026)

1. mxcli JSON-output: NOG TE BEVESTIGEN empirisch. We weten dat mxcli `--format sarif`
   heeft; of `--format json` bestaat en bruikbaar is, bepaal je door het te draaien op
   TRB. Stel het exacte schema vast vOOr de normalizer.
2. MxLint/Rego headless CLI: BEVESTIGD. `mxlint-cli lint` (Go-CLI, OPA/Rego), output
   via mxlint.yaml (jsonFile + xunitReport XML). GEEN SARIF, GEEN --format-vlag. Exact
   JSON-schema nog empirisch vast te stellen vOOr de normalizer.
3. Rego-flowgraaf-regel (complexity / db-actions-at-end) haalbaar + kalibreerbaar tegen
   grondwaarheid (315 / 31): NOG TE BEWIJZEN. Valideert de "twee engines"-reden, maar
   blokkeert de MVP niet (de mxcli-engine alleen levert al een werkend product).
4. Eigen paneel + procesexecutie: BEWEZEN (spike, Studio Pro 11.10, juni 2026).
   - Eigen dockable paneel: BEVESTIGD. In Fase 1 via studioPro.ui.panes (web); voor
     het eindproduct C#-gehost via DockablePaneExtension + IDockingWindowService.OpenPane.
   - Procesexecutie + message passing: BEWEZEN. C#-extensie draaide `cmd /c echo test`
     via Process.Start (exitCode 0, stdout "test") en leverde de output via de
     WebView-message-bus (IWebView.PostMessage ⇄ window.chrome.webview) aan het pane,
     dat het als ruwe tekst toonde. Geen losse aanname meer; zie csharp-spike/.
   - Runtime: .NET 10 (ExtensionsAPI 11.10.0 vereist net10.0; net8.0 → NU1202).

Node.js/npm: BEVESTIGD aanwezig (v24 / npm 11).

Volgorde-gevolg: Fase 1 (schil + hardcoded JSON in het paneel) is af. De
C#-procesexecutie-spike — de poort naar Fase 2 — is GESLAAGD. Aanname 1 (mxcli
JSON-schema) is nu de EERSTE Fase 2-stap: ze gateert de normalizer en moet als
eerste empirisch worden opgelost. Aanname 3 (Rego-flowgraaf) blijft open maar is pas
relevant bij engine 2. Zie het Fase 2-plan in sectie 9.

---

## 8. Wat er al klaar is (Blue-tier, verified)

11 geverifieerde .star-regels (zie acr-mxlint-voortgang.md) zijn de eerste
`verified`-entries in het registry. De shell hoeft die niet opnieuw te bouwen;
ze vullen de Blue-kolom en bewijzen de mxcli-engine-integratie meteen met echte data.

---

## 9. Fase 2 — plan (na geslaagde spike)

Architectuurbesluit (volgt uit sectie 0 + de spike): het violations-paneel wordt
C#-GEHOST. De C#-backend draait de engines via Process.Start en serveert de bestaande
Fase 1 ui-render-bundle als content (WebServerExtension → wwwroot).

BESLUIT — NORMALISATIE DRAAIT IN C# (niet in de web-laag):
Beide engines komen via Process.Start in de C#-laag binnen (mxcli → JSON,
mxlint-cli → XML/JSON). C# mapt die daar naar één `Violation[]` (sectie 2), zodat de
web-laag ENGINE-ONBEWUST blijft — een harde spec-eis (de gebruiker mag niet weten of
een regel uit .star of .rego komt). Bovendien horen `fingerprint` (sectie 3) en het
toepassen van exclusions bij het genormaliseerde Violation-object; C# berekent de
fingerprint en past exclusions toe VÓÓRDAT de data naar de webview gaat. Gevolg:
- C# = engines draaien + normaliseren + fingerprint + exclusions + registry-validatie.
- web-laag = PUUR presentatie. De render-laag blijft ongewijzigd en consumeert
  `Violation[]` via een ViolationSource; in Fase 2 levert een nieuwe ViolationSource
  de (al genormaliseerde) data uit de C#-backend — via de WebView-message-bus of een
  HTTP-route van de ingebouwde webserver.

Bouw incrementeel — elke stap is op zichzelf verifieerbaar voordat de volgende start.
Stap 0 (`cmd /c echo test` → pane) is ✅ KLAAR via de spike (procesexecutie + message
passing end-to-end bewezen). De Fase 2-stappen:

1. EERSTE FASE 2-STAP — stel het ECHTE JSON-schema van `mxcli lint --format json`
   op TRB vast. Dit gaat vooraf aan al het andere: de normalizer (stap 3) kan pas
   geschreven worden als dit schema bekend is, en het lost aanname 1 op (we weten dat
   `--format sarif` bestaat; `--format json` + het exacte schema moet empirisch
   bevestigd). Voorwaarde: `mxcli` is aanroepbaar vanuit de C#-extensie (PATH of
   volledig pad — desnoods eerst kort met `mxcli --version` checken).
2. Draai `mxcli lint --format json` op TRB vanuit C# en toon de ruwe JSON in de pane
   (bewijst de echte engine-aanroep + dat het schema klopt met stap 1).
3. Normalizer IN C# (engine-JSON → `Violation[]` per sectie 2): map mxcli-JSON naar
   het datacontract. Bepaal per violation `kind` via de ACR-registry-match (sectie 4):
   geclaimd+verified → `kind: "acr"` met ACR-metadata; niet-geclaimd pack-regel →
   `kind: "generic"` met eigen categorie/severity + `source`. Bereken `fingerprint` en
   pas exclusions toe (sectie 3) — alles in C#, voor beide soorten.
4. Violations in het C#-gehoste paneel: vervang HardcodedViolationSource door de
   backend-bron, zodat de echte (genormaliseerde) mxcli-violations in de ACR-layout
   verschijnen. De web-laag verandert niet.
5. Pas DAARNA: engine 2 (mxlint-cli, .rego — aanname 3) door dezelfde normalizer,
   exclusions-UI (sectie 3), rapport-export (sectie 5), regelmigratie
   (Green/Blue/Orange/Red).

Open aannames die Fase 2 raken:
- Aanname 1 (mxcli `--format json`-schema): is letterlijk stap 1 — blokkeert de
  normalizer en dus de hele engine-integratie tot ze opgelost is.
- Aanname 3 (Rego-flowgraaf-regel kalibreert tegen 315/31): pas relevant bij engine 2
  (stap 5); de mxcli-engine alleen levert al een werkend product.