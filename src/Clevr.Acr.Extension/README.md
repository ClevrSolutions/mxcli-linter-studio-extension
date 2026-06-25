# CLEVR ACR — C#-spike: procesexecutie + message passing

Gerichte spike voor de openstaande aanname uit
[`../clevr-acr-shell-spec.md`](../clevr-acr-shell-spec.md) **sectie 7 / punt 4**:

> Kan een **C#-backendcomponent** van de extensie (a) een **extern proces** starten
> via `Process.Start`, en (b) de **output via Message Passing** bij het web-paneel
> krijgen?

Dit is **alleen** een ja/nee-bewijs. Geen normalizer, geen echte lint, geen Fase 2.

---

## TL;DR — uitkomst

| Deel | Status | Onderbouwing |
|---|---|---|
| (a) `Process.Start` vanuit C#-extensie | **Ja (gegarandeerd)** | Een C#-extensie is een gewone **.NET 10**-class library; `System.Diagnostics.Process` is standaard en wordt door Mendix niet gesandboxt. Geen Mendix-API nodig. |
| (b) Output via Message Passing naar de webview | **Ja (gedocumenteerd + code-compleet)** | `IWebView.PostMessage(string, object?)` (C#→web) ⇄ `window.chrome.webview.postMessage` / `MessageReceived` (web→C#). Signatures geverifieerd in de officiële API-referentie. |
| End-to-end gedraaid in Studio Pro | **NIET geverifieerd in deze omgeving** | Er is hier **geen .NET SDK** (alleen de runtime) en geen Studio Pro. De code is geschreven tegen de echte API; compileren + draaien moet op jouw machine. |

**Belangrijkste architectuur-bevinding (lees dit):** de gedocumenteerde C#↔web-brug
werkt alleen als de **C#-kant de webview-pane bezit** (`DockablePaneExtension` +
`WebServerExtension` die `wwwroot` serveert). Dat is een **andere** brug dan de
web-extensie z'n `studioPro.ui.messagePassing` (die is web-entrypoint↔web-entrypoint
*binnen* de web-extensie). Er is **geen** gedocumenteerde manier voor C# om een
bericht te duwen naar de pane die de Fase-1 TypeScript-extensie registreerde via
`studioPro.ui.panes`. Gevolg voor Fase 2: het violations-paneel wordt dan
**door C# gehost** (het serveert onze bestaande render-UI uit `wwwroot`), in plaats
van door de losse TS-pane. Dit is een ontwerpkeuze die je bewust moet maken.

---

## Fase 2A + 2B — echte mxcli-data in de ACR-layout (op deze spike gebouwd)

Bovenop de bewezen spike (Process.Start + message passing) is de **echte mxcli-engine**
aangesloten op de **bestaande, geteste normalizer** (deel A), en de **ACR-layout** uit
de Fase 1 web-extensie is naar de `wwwroot` van deze C#-gehoste pane geport (deel B).

**Keten:** knop "Scan for improvements" → C# draait `mxcli lint -p "<.mpr>" --format json`
via `Process.Start` (cwd = projectmap) → `MxcliOutputParser` → `MxcliNormalizer` +
`RuleRegistry` (uit `rules.json`) → `Violation[]` → als JSON via de message-bus naar de
pane → **ACR-layout** (`wwwroot/main.js`). De `engine`-property wordt **niet** getoond.

**ACR-layout (deel B), zoals spec sectie 5:**
- **Per-regel groepering:** elke regel verschijnt één keer (uitklapbaar `<details>`),
  met severity, ruleId, acrCode/source-badge en totaal aantal improvements; de
  individuele gevallen (document + reden) zitten genest eronder. MPR008 met 12 gevallen
  = één uitklapbare regel met 12 items, niet 12 rijen.
- **Alles in de zes categorieën + herkomst zichtbaar (spec sectie 5):** ALLE improvements
  (ACR + generiek) staan in de zes ACR-categorieën. Generieke regels worden voor de
  weergave in een categorie geplaatst via een vaste mxcli-prefix→categorie-mapping
  (`GENERIC_CATEGORY_MAP`: SEC→Security, PERF→Performance, DESIGN/ARCH→Architecture,
  CONV→Project hygiene, MPR/QUAL→Maintainability; onbekend→Maintainability). Per regel
  een **herkomst-badge** (ACR / MxCLI / Mxlint.com) — ACR-regels eerst — zodat
  gekalibreerd ACR nooit met engine-generiek wordt verward. Het interne
  `Violation.category` blijft de engine-prefix; dit is puur een display-mapping.
- **Severity:** ACR-regels tonen hun ACR-severity (uit het registry); generieke regels
  tonen LETTERLIJK de mxcli engine-severity (error/warning/info/hint) — niet vertaald.
  Beide als severity-chip. Een ACR-severity buiten de vier (`TODO-confirm`) valt onder
  "Te bevestigen".
- **Telkaart (drie kaarten):** telt ALLE improvements — per categorie, per severity, en
  per **herkomst** (ACR (gekalibreerd) / MxCLI Mxlint / Mxlint.com) zodat de verdeling
  ACR-vs-generiek zichtbaar blijft.
- **Terminologie:** de UI zegt overal **"Improvements"** (kop, telkaart, secties,
  knop). Dit is ALLEEN UI-tekst — het interne `Violation`-type/datacontract is
  ongewijzigd.

De render-laag in `wwwroot/main.js` is puur en bron-onbewust: `renderReport(root,
violations, query)` consumeert het `Violation[]`-array en weet niet dat het uit mxcli
komt (zelfde data/UI-scheiding als Fase 1). Een filterveld doorzoekt de improvements.

**Regelnaam op de regel-kop:** elke regel toont een herkenbare naam naast het id —
ACR-regels hun `acrCode` (bv. CLEVR-HYG-001 → DuplicateEntityNames), generieke
mxcli-regels de naam uit de **mxcli-catalogus** (`mxcli lint --list-rules`, bv. CONV001
→ BooleanNaming, MPR001 → NamingConvention). De lint-JSON zelf bevat GEEN naam (alleen
de ruleId), daarom haalt `AcrScanService` de catalogus apart op en stuurt 'm als
`ruleNames` (ruleId → naam) mee in de payload; `MxcliRulesCatalogParser` parseert de
tekstoutput. Best-effort: lukt `--list-rules` niet, dan tonen generieke regels alleen
hun id + preview.

**Detailtekst op de regel-kop:** elke dichtgeklapte regel toont daarnaast een korte
preview — de `reason` van het eerste geval, afgekapt op ~60 tekens (de VOLLEDIGE reason,
geen document-strip-heuristiek: voorspelbaarder). Bij uitklappen blijft de volledige
reason per geval zichtbaar.

**Statusregel:** toont de volledige herkomst-uitsplitsing met dezelfde labels als de
filter/telkaart, bv. `2185 improvements (77 ACR / 2108 MxCLI Mxlint / 0 Mxlint.com)
— 2185 ruw, exit 0` (counts berekend uit de violations via `originOf`).

**Engine-filter (herkomst):** onder de knop staan toggles **ACR / MxCLI Mxlint /
Mxlint.com** (met totaal-count per herkomst uit de volledige scan). Hiermee filter je
de getoonde improvements op herkomst. Mxlint.com verschijnt al maar is grijs/uitgeschakeld
zolang er geen mxlint-data binnenkomt (count 0). **Keuze:** de telkaarten **bewegen mee
met de actieve filter** (ze tellen de gefilterde set), zodat de aantallen altijd matchen
met de zichtbare lijst — consistent met het tekstfilter. De per-herkomst-count in de
filter-toggles zelf blijft het totaal tonen.

**Nieuwe bestanden:**
| Bestand | Rol |
|---|---|
| [`AcrScanSettings.cs`](AcrScanSettings.cs) | configureerbaar `mxcliPath` + `projectPath` (uit `acr-scan-settings.json`) |
| [`AcrScanService.cs`](AcrScanService.cs) | orkestreert run → parse → normaliseer → JSON (alleen IO/bedrading, géén normalisatielogica) |
| [`acr-scan-settings.json`](acr-scan-settings.json) | de instellingen (zie hieronder) |
| `rules.json` (build-link naar [`../csharp-normalizer/rules.sample.json`](../csharp-normalizer/rules.sample.json)) | het ACR-registry, één bron van waarheid |

De normalizer + registry zijn **ongewijzigd** hergebruikt (project-referentie naar
`Clevr.Acr.Normalizer`). De pure parse-helpers `MxcliOutputParser` en
`RuleRegistryJson` zijn als **nieuwe** bestanden aan die library toegevoegd (de
bestaande, geteste klassen zijn niet aangeraakt).

### Instellen (niet hardcoded)
Vul vóór het draaien [`acr-scan-settings.json`](acr-scan-settings.json) in:
```json
{ "mxcliPath": "C:\\pad\\naar\\mxcli.exe", "projectPath": "C:\\pad\\naar\\App" }
```
- `mxcliPath` leeg/weg → `mxcli` (verondersteld op PATH).
- `projectPath` = de **projectmap** (waarin precies één `.mpr` staat) óf direct een
  `.mpr`-pad. Leeg → valt terug op de map van de **geopende app** (`CurrentApp`).

De scan draait mxcli net als de werkende handmatige run: **WorkingDirectory = de
projectmap** (mxcli vindt z'n `.mxcli`-cache relatief) en `-p` krijgt de
**.mpr-bestandsnaam** (relatief), niet de map. Bij een start-fout, exit≠0 of
parse-fout toont de pane de volledige diagnostiek: command-line, working directory,
exitcode en de eerste ~1000 tekens van stdout én stderr.

Dit bestand staat in de extensiemap (naast de dll's) en wordt bij elke build
meegekopieerd. Je kunt het ook ná deployment in de extensiemap aanpassen.

### Bouwen, laden, draaien
1. `dotnet build -c Debug` (in `csharp-spike`). Output: `bin\Debug\net10.0\` met
   `Clevr.AcrSpike.dll`, **`Clevr.Acr.Normalizer.dll`**, `manifest.json`,
   `rules.json`, `acr-scan-settings.json` en `wwwroot\`.
2. Kopieer de **hele** inhoud van `bin\Debug\net10.0\` naar
   `<app>\extensions\clevracrspike\` (de normalizer-dll moet mee!).
3. Start Studio Pro met `--enable-extension-development`, **F4** om te (her)laden.
4. **Extensions → … → CLEVR ACR Spike** → klik **Scan for improvements**.

### Wat je verwacht te zien
- Een samenvattingsregel, bv. `2185 improvements (77 ACR / 2108 generiek) — 2185 ruw, exit 0`.
- De **ACR-layout**: een telkaart (per categorie / per severity / per herkomst) en de
  zes ACR-categorieën met per categorie uitklapbare regels. In elke categorie staan zowel
  onze geverifieerde ACR-regels (badge **ACR**, CLEVR-ruleId, categorie/severity uit het
  registry — ENT_ATTRS = Maintainability/Minor) als de bundled mxcli-regels (badge
  **MxCLI**, eigen engine-severity), in dezelfde categorie maar met zichtbaar verschillende
  herkomst. Bijvoorbeeld: in **Maintainability** zie je CLEVR-MAINT-* naast MPR/QUAL-regels.
- Lukt het starten van mxcli niet (verkeerd pad / niet op PATH), dan toont de pane
  de volledige diagnostiek (command, cwd, exitcode, stdout/stderr) i.p.v. te crashen.

> Bekend aandachtspunt: de scan draait **synchroon** in de message-handler (zoals de
> spike). Een lint op een groot project kan de UI even laten wachten; async +
> terug-marshallen naar de UI-thread is een latere verbetering.

### Verificatiestatus van deze stap
- **Compileert:** ja — `Clevr.AcrSpike` + `Clevr.Acr.Normalizer` bouwen schoon (.NET 10).
- **Keten klopt:** bewezen met unit-tests (`DataChainTests`): `rules.sample.json` →
  registry, mxcli-gevormde JSON → DTO's → normalizer → `Violation[]` met juiste
  kind/categorie/severity (ACR_ENT_ATTRS → acr/Maintainability/Minor; MPR001 →
  generic/MPR). **16/16 tests slagen.**
- **End-to-end in Studio Pro met echte mxcli:** doe jij (geen mxcli/Studio Pro hier).
  `MxcliOutputParser` knipt nu de **statusregels** weg die mxcli vóór de JSON op stdout
  schrijft (bv. "Connected to…", "✓ Catalog ready") — het pakt de tekst vanaf de eerste
  regel die met `{` of `[` begint — en dekt zowel bare-array als object-wrapper. Lukt
  parsen toch niet, dan toont de pane de eerste ~500 tekens van de ruwe stdout, zodat
  de werkelijke vorm zichtbaar is.

---

## Wat de spike doet

1. Registreert een **door C# beheerde** dockable pane, geopend via
   **Extensions → … → CLEVR ACR Spike** (`IDockingWindowService.OpenPane`).
2. De pane toont een mini-webpagina (knop **Run command** + een output-veld).
3. Klik → JS stuurt `RunCommand` naar C# → C# draait `cmd /c echo test` via
   `Process.Start` → C# stuurt de ruwe stdout/stderr/exitcode terug met
   `PostMessage("CommandOutput", tekst)` → JS toont het als ruwe tekst.

Het commando staat in [`ProcessRunner.cs`](ProcessRunner.cs) → `RunSpikeCommand()`.
Begin met `cmd /c echo test`; zet daarna het commentaar om naar `mxcli --version`.

### Bestanden
| Bestand | Rol |
|---|---|
| `Clevr.AcrSpike.csproj` | .NET 10-project, refereert `Mendix.StudioPro.ExtensionsAPI` |
| `manifest.json` | `{ "mx_extensions": [ "Clevr.AcrSpike.dll" ] }` |
| `SpikeDockablePaneExtension.cs` | registreert de pane (`Id` + `Open()`) |
| `SpikeMenuExtension.cs` | menu-item dat de pane opent via `IDockingWindowService.OpenPane` |
| `SpikeDockablePaneViewModel.cs` | **de brug**: `MessageReceived` → proces → `PostMessage` |
| `ProcessRunner.cs` | **(a)** `Process.Start`, vangt stdout/stderr/exit |
| `SpikeWebServerExtension.cs` | serveert `wwwroot/index.html` + `main.js` |
| `HttpListenerResponseUtils.cs` | mini-helper om een bestand te serveren |
| `wwwroot/index.html`, `wwwroot/main.js` | **(b)** web-kant van de message-bus |

---

## Vereisten (op jouw machine)

- **.NET 10 SDK** (niet alleen de runtime) — de ExtensionsAPI voor Studio Pro 11.10
  vereist `net10.0`. (bv. via `winget install Microsoft.DotNet.SDK.10`.)
- Visual Studio 2022 **of** de `dotnet` CLI. (Rider/VS Code kan ook.)
- Studio Pro 11.10 (jouw versie). De NuGet-versie in de `.csproj` moet **≤** je
  Studio Pro-versie zijn — staat nu op `11.10.0`.
- Toegang tot de NuGet-bron met `Mendix.StudioPro.ExtensionsAPI` (nuget.org).

---

## Bouwen

**Met de CLI:**
```powershell
cd csharp-spike
dotnet build -c Debug
```
De output (dll + `manifest.json` + `wwwroot/`) staat in `bin\Debug\net10.0\`.

**Met Visual Studio 2022:** open/maak een solution met dit project en **Build**.

---

## Laden in Studio Pro

1. Open je app-map (in Studio Pro: **App → Show App Directory in Explorer**).
2. Maak `<app>\extensions\clevracrspike\`.
3. Kopieer de **inhoud** van `bin\Debug\net10.0\` daarheen (dll, `manifest.json`,
   en de `wwwroot`-map).
   - Of: zet in `Clevr.AcrSpike.csproj` het `PostBuild`-`Copy`-pad goed en haal het
     commentaar weg, dan kopieert de build zelf.
4. Start Studio Pro met extensie-ontwikkeling aan:
   ```powershell
   .\studiopro.exe --enable-extension-development
   ```
5. Open je app. In Studio Pro: druk **F4** (Synchronize App Directory) om de
   extensie te (her)laden.
6. Open het paneel via **Extensions → … → CLEVR ACR Spike** en klik **Run command**.

**Verwacht resultaat (= bewijs):** in het output-veld verschijnt zoiets als:
```
exitCode: 0
ok: True

--- stdout ---
test

--- stderr ---
```

---

## Herladen na een wijziging

- C#-code gewijzigd → opnieuw `dotnet build`, output kopiëren, **F4** in Studio Pro
  (of Studio Pro herstarten).
- Alleen `wwwroot` (html/js) gewijzigd → kopiëren en de pane sluiten/heropenen.

---

## Debuggen

- **Webview-console** (de JS in de pane): start met
  `.\studiopro.exe --enable-extension-development --webview-remote-debugging`,
  open dan `edge://inspect` en attach. (Of `IWebView.ShowDevTools()` aanroepen mits
  DevTools toegestaan is.)
- **C#-code:** Visual Studio → **Debug → Attach to Process** → `studiopro.exe`,
  breakpoint in `MessageReceived`/`ProcessRunner`.
- **Logs:** `ILogService.Info(...)` (gebruikt in de spike) verschijnt in de Studio
  Pro-log — **Help → Open Log File Directory** → `log.txt`.

---

## Eerlijke beperking van dit bewijs

Ik kon de spike **niet** in deze omgeving compileren of draaien: er is geen .NET SDK
geïnstalleerd (alleen de .NET-runtime-host) en geen Studio Pro. De code is daarom
geschreven en gecontroleerd **tegen de officiële Mendix-API-referentie** (signatures
van `IWebView.PostMessage`, `MessageReceived`, `DockablePaneExtension`,
`WebServerExtension`, `ILogService` zijn één voor één geverifieerd). Deel (a) is
sowieso een .NET-garantie. De laatste 5% — daadwerkelijk groen licht in Studio Pro
11.10 — moet jij op je machine bevestigen met bovenstaande stappen. Verwacht een
soepele run; het meest waarschijnlijke struikelpunt is de NuGet-versie/Studio
Pro-versie-match, niet de aanname zelf.
