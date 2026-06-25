# CLEVR ACR — C# spike: process execution + message passing

Targeted spike for the open assumption from
[`../clevr-acr-shell-spec.md`](../clevr-acr-shell-spec.md) **section 7 / point 4**:

> Can a **C# backend component** of the extension (a) start an **external process**
> via `Process.Start`, and (b) get the **output via Message Passing** to the web panel?

This is **only** a yes/no proof. No normalizer, no real lint, no Phase 2.

---

## TL;DR — outcome

| Part | Status | Substantiation |
|---|---|---|
| (a) `Process.Start` from C# extension | **Yes (guaranteed)** | A C# extension is a regular **.NET 10** class library; `System.Diagnostics.Process` is standard and is not sandboxed by Mendix. No Mendix API required. |
| (b) Output via Message Passing to the webview | **Yes (documented + code-complete)** | `IWebView.PostMessage(string, object?)` (C#→web) ⇄ `window.chrome.webview.postMessage` / `MessageReceived` (web→C#). Signatures verified in the official API reference. |
| End-to-end run in Studio Pro | **NOT verified in this environment** | There is **no .NET SDK** here (only the runtime) and no Studio Pro. The code is written against the real API; compiling + running must be done on your machine. |

**Key architecture finding (read this):** the documented C#↔web bridge
only works if the **C# side owns the webview pane** (`DockablePaneExtension` +
`WebServerExtension` serving `wwwroot`). That is a **different** bridge from the
web extension's `studioPro.ui.messagePassing` (which is web-entrypoint↔web-entrypoint
*within* the web extension). There is **no** documented way for C# to push a
message to the pane that the Phase 1 TypeScript extension registered via
`studioPro.ui.panes`. Consequence for Phase 2: the violations panel will then be
**hosted by C#** (it serves our existing render UI from `wwwroot`), rather than
by the standalone TS pane. This is a design choice you need to make consciously.

---

## Phase 2A + 2B — real mxcli data in the ACR layout (built on this spike)

On top of the proven spike (Process.Start + message passing), the **real mxcli engine**
has been connected to the **existing, tested normalizer** (part A), and the **ACR layout**
from the Phase 1 web extension has been ported to the `wwwroot` of this C#-hosted pane (part B).

**Chain:** button "Scan for improvements" → C# runs `mxcli lint -p "<.mpr>" --format json`
via `Process.Start` (cwd = project folder) → `MxcliOutputParser` → `MxcliNormalizer` +
`RuleRegistry` (from `rules.json`) → `Violation[]` → as JSON via the message bus to the
pane → **ACR layout** (`wwwroot/main.js`). The `engine` property is **not** shown.

**ACR layout (part B), as per spec section 5:**
- **Per-rule grouping:** each rule appears once (expandable `<details>`),
  with severity, ruleId, acrCode/source badge and total number of improvements; the
  individual cases (document + reason) are nested underneath. MPR008 with 12 cases
  = one expandable rule with 12 items, not 12 rows.
- **Everything in the six categories + origin visible (spec section 5):** ALL improvements
  (ACR + generic) are in the six ACR categories. Generic rules are placed in a category for
  display via a fixed mxcli-prefix→category mapping
  (`GENERIC_CATEGORY_MAP`: SEC→Security, PERF→Performance, DESIGN/ARCH→Architecture,
  CONV→Project hygiene, MPR/QUAL→Maintainability; unknown→Maintainability). Per rule
  an **origin badge** (ACR / MxCLI / Mxlint.com) — ACR rules first — so that
  calibrated ACR is never confused with engine-generic. The internal
  `Violation.category` remains the engine prefix; this is purely a display mapping.
- **Severity:** ACR rules show their ACR severity (from the registry); generic rules
  show the mxcli engine severity LITERALLY (error/warning/info/hint) — not translated.
  Both as a severity chip. An ACR severity outside the four (`TODO-confirm`) falls under
  "To be confirmed".
- **Summary card (three cards):** counts ALL improvements — per category, per severity, and
  per **origin** (ACR (calibrated) / MxCLI Mxlint / Mxlint.com) so that the distribution
  ACR-vs-generic remains visible.
- **Terminology:** the UI says **"Improvements"** everywhere (heading, summary card, sections,
  button). This is ONLY UI text — the internal `Violation` type/data contract is
  unchanged.

The render layer in `wwwroot/main.js` is pure and source-agnostic: `renderReport(root,
violations, query)` consumes the `Violation[]` array and does not know it comes from mxcli
(same data/UI separation as Phase 1). A filter field searches the improvements.

**Rule name on the rule heading:** each rule shows a recognizable name next to the id —
ACR rules their `acrCode` (e.g. CLEVR-HYG-001 → DuplicateEntityNames), generic
mxcli rules the name from the **mxcli catalog** (`mxcli lint --list-rules`, e.g. CONV001
→ BooleanNaming, MPR001 → NamingConvention). The lint JSON itself contains NO name (only
the ruleId), which is why `AcrScanService` fetches the catalog separately and sends it as
`ruleNames` (ruleId → name) in the payload; `MxcliRulesCatalogParser` parses the
text output. Best-effort: if `--list-rules` fails, generic rules only show their id + preview.

**Detail text on the rule heading:** each collapsed rule also shows a short
preview — the `reason` of the first case, truncated to ~60 characters (the FULL reason,
no document-strip heuristic: more predictable). When expanded, the full
reason per case remains visible.

**Status line:** shows the full origin breakdown with the same labels as the
filter/summary card, e.g. `2185 improvements (77 ACR / 2108 MxCLI Mxlint / 0 Mxlint.com)
— 2185 raw, exit 0` (counts calculated from the violations via `originOf`).

**Engine filter (origin):** below the button there are toggles **ACR / MxCLI Mxlint /
Mxlint.com** (with total count per origin from the full scan). This filters the displayed
improvements by origin. Mxlint.com appears already but is greyed out/disabled
as long as no mxlint data comes in (count 0). **Choice:** the summary cards **move with
the active filter** (they count the filtered set), so the numbers always match
the visible list — consistent with the text filter. The per-origin count in the
filter toggles themselves continues to show the total.

**New files:**
| File | Role |
|---|---|
| [`AcrScanSettings.cs`](AcrScanSettings.cs) | configurable `mxcliPath` + `projectPath` (from `acr-scan-settings.json`) |
| [`AcrScanService.cs`](AcrScanService.cs) | orchestrates run → parse → normalize → JSON (IO/wiring only, no normalization logic) |
| [`acr-scan-settings.json`](acr-scan-settings.json) | the settings (see below) |
| `rules.json` (build-link to [`../csharp-normalizer/rules.sample.json`](../csharp-normalizer/rules.sample.json)) | the ACR registry, single source of truth |

The normalizer + registry are reused **unchanged** (project reference to
`Clevr.Acr.Normalizer`). The pure parse helpers `MxcliOutputParser` and
`RuleRegistryJson` have been added as **new** files to that library (the
existing, tested classes have not been touched).

### Configuration (not hardcoded)
Fill in [`acr-scan-settings.json`](acr-scan-settings.json) before running:
```json
{ "mxcliPath": "C:\\path\\to\\mxcli.exe", "projectPath": "C:\\path\\to\\App" }
```
- `mxcliPath` empty/absent → `mxcli` (assumed on PATH).
- `projectPath` = the **project folder** (containing exactly one `.mpr`) or directly a
  `.mpr` path. Empty → falls back to the folder of the **opened app** (`CurrentApp`).

The scan runs mxcli just like the working manual run: **WorkingDirectory = the
project folder** (mxcli finds its `.mxcli` cache relatively) and `-p` receives the
**.mpr filename** (relative), not the folder. On a start error, exit≠0 or
parse error the pane shows the full diagnostics: command line, working directory,
exit code and the first ~1000 characters of stdout and stderr.

This file lives in the extension folder (next to the dlls) and is copied with every build.
You can also modify it after deployment in the extension folder.

### Building, loading, running
1. `dotnet build -c Debug` (in `csharp-spike`). Output: `bin\Debug\net10.0\` with
   `Clevr.AcrSpike.dll`, **`Clevr.Acr.Normalizer.dll`**, `manifest.json`,
   `rules.json`, `acr-scan-settings.json` and `wwwroot\`.
2. Copy the **entire** contents of `bin\Debug\net10.0\` to
   `<app>\extensions\clevracrspike\` (the normalizer dll must be included!).
3. Start Studio Pro with `--enable-extension-development`, **F4** to (re)load.
4. **Extensions → … → CLEVR ACR Spike** → click **Scan for improvements**.

### What you expect to see
- A summary line, e.g. `2185 improvements (77 ACR / 2108 generic) — 2185 raw, exit 0`.
- The **ACR layout**: a summary card (per category / per severity / per origin) and the
  six ACR categories with expandable rules per category. Each category contains both
  our verified ACR rules (badge **ACR**, CLEVR ruleId, category/severity from the
  registry — ENT_ATTRS = Maintainability/Minor) and the bundled mxcli rules (badge
  **MxCLI**, own engine severity), in the same category but with visibly different
  origin. For example: in **Maintainability** you see CLEVR-MAINT-* next to MPR/QUAL rules.
- If starting mxcli fails (wrong path / not on PATH), the pane shows
  the full diagnostics (command, cwd, exit code, stdout/stderr) instead of crashing.

> Known point of attention: the scan runs **synchronously** in the message handler (as in the
> spike). A lint on a large project may cause the UI to wait briefly; async +
> marshalling back to the UI thread is a later improvement.

### Verification status of this step
- **Compiles:** yes — `Clevr.AcrSpike` + `Clevr.Acr.Normalizer` build cleanly (.NET 10).
- **Chain correct:** proven with unit tests (`DataChainTests`): `rules.sample.json` →
  registry, mxcli-shaped JSON → DTOs → normalizer → `Violation[]` with correct
  kind/category/severity (ACR_ENT_ATTRS → acr/Maintainability/Minor; MPR001 →
  generic/MPR). **16/16 tests pass.**
- **End-to-end in Studio Pro with real mxcli:** you do this (no mxcli/Studio Pro here).
  `MxcliOutputParser` now strips the **status lines** that mxcli writes to stdout before the JSON
  (e.g. "Connected to…", "✓ Catalog ready") — it picks up the text from the first
  line starting with `{` or `[` — and covers both bare array and object wrapper. If
  parsing still fails, the pane shows the first ~500 characters of raw stdout, so
  the actual format is visible.

---

## What the spike does

1. Registers a **C#-managed** dockable pane, opened via
   **Extensions → … → CLEVR ACR Spike** (`IDockingWindowService.OpenPane`).
2. The pane displays a mini web page (button **Run command** + an output field).
3. Click → JS sends `RunCommand` to C# → C# runs `cmd /c echo test` via
   `Process.Start` → C# sends the raw stdout/stderr/exit code back with
   `PostMessage("CommandOutput", text)` → JS displays it as raw text.

The command is in [`ProcessRunner.cs`](ProcessRunner.cs) → `RunSpikeCommand()`.
Start with `cmd /c echo test`; then uncomment the `mxcli --version` line.

### Files
| File | Role |
|---|---|
| `Clevr.AcrSpike.csproj` | .NET 10 project, references `Mendix.StudioPro.ExtensionsAPI` |
| `manifest.json` | `{ "mx_extensions": [ "Clevr.AcrSpike.dll" ] }` |
| `SpikeDockablePaneExtension.cs` | registers the pane (`Id` + `Open()`) |
| `SpikeMenuExtension.cs` | menu item that opens the pane via `IDockingWindowService.OpenPane` |
| `SpikeDockablePaneViewModel.cs` | **the bridge**: `MessageReceived` → process → `PostMessage` |
| `ProcessRunner.cs` | **(a)** `Process.Start`, captures stdout/stderr/exit |
| `SpikeWebServerExtension.cs` | serves `wwwroot/index.html` + `main.js` |
| `HttpListenerResponseUtils.cs` | mini helper for serving a file |
| `wwwroot/index.html`, `wwwroot/main.js` | **(b)** web side of the message bus |

---

## Requirements (on your machine)

- **.NET 10 SDK** (not just the runtime) — the ExtensionsAPI for Studio Pro 11.10
  requires `net10.0`. (e.g. via `winget install Microsoft.DotNet.SDK.10`.)
- Visual Studio 2022 **or** the `dotnet` CLI. (Rider/VS Code works too.)
- Studio Pro 11.10 (your version). The NuGet version in the `.csproj` must be **≤** your
  Studio Pro version — currently set to `11.10.0`.
- Access to the NuGet source with `Mendix.StudioPro.ExtensionsAPI` (nuget.org).

---

## Building

**With the CLI:**
```powershell
cd csharp-spike
dotnet build -c Debug
```
The output (dll + `manifest.json` + `wwwroot/`) is in `bin\Debug\net10.0\`.

**With Visual Studio 2022:** open/create a solution with this project and **Build**.

---

## Loading in Studio Pro

1. Open your app folder (in Studio Pro: **App → Show App Directory in Explorer**).
2. Create `<app>\extensions\clevracrspike\`.
3. Copy the **contents** of `bin\Debug\net10.0\` there (dll, `manifest.json`,
   and the `wwwroot` folder).
   - Or: set the `PostBuild` `Copy` path correctly in `Clevr.AcrSpike.csproj` and uncomment it,
     then the build copies it automatically.
4. Start Studio Pro with extension development enabled:
   ```powershell
   .\studiopro.exe --enable-extension-development
   ```
5. Open your app. In Studio Pro: press **F4** (Synchronize App Directory) to
   (re)load the extension.
6. Open the panel via **Extensions → … → CLEVR ACR Spike** and click **Run command**.

**Expected result (= proof):** the output field shows something like:
```
exitCode: 0
ok: True

--- stdout ---
test

--- stderr ---
```

---

## Reloading after a change

- C# code changed → run `dotnet build` again, copy the output, **F4** in Studio Pro
  (or restart Studio Pro).
- Only `wwwroot` (html/js) changed → copy and close/reopen the pane.

---

## Debugging

- **Webview console** (the JS in the pane): start with
  `.\studiopro.exe --enable-extension-development --webview-remote-debugging`,
  then open `edge://inspect` and attach. (Or call `IWebView.ShowDevTools()` if
  DevTools is permitted.)
- **C# code:** Visual Studio → **Debug → Attach to Process** → `studiopro.exe`,
  breakpoint in `MessageReceived`/`ProcessRunner`.
- **Logs:** `ILogService.Info(...)` (used in the spike) appears in the Studio
  Pro log — **Help → Open Log File Directory** → `log.txt`.

---

## Honest limitation of this proof

I was unable to compile or run the spike **in this environment**: no .NET SDK is
installed (only the .NET runtime host) and no Studio Pro is present. The code has therefore
been written and verified **against the official Mendix API reference** (signatures
of `IWebView.PostMessage`, `MessageReceived`, `DockablePaneExtension`,
`WebServerExtension`, `ILogService` have been verified one by one). Part (a) is
a .NET guarantee in any case. The final 5% — actually getting a green light in Studio Pro
11.10 — you must confirm on your machine with the steps above. Expect a
smooth run; the most likely stumbling block is the NuGet version/Studio
Pro version match, not the assumption itself.
