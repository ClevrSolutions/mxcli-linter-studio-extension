# CLEVR ACR — Studio Pro extension · installation guide

CLEVR ACR runs **mxcli** plus CLEVR's own calibrated rules over a Mendix project, and shows the
findings as **Improvements** grouped into the six ACR categories: *Project hygiene, Maintainability,
Performance, Architecture, Reliability, Security*. Everything reads through **mxcli** (its catalog,
`describe`, and own lint rules) — **no mxlint, no model-source export step** to install or run. A
**Scan** button (quick) and a **Deepscan** button (adds the deep microflow/expression analysis) do
everything; results merge into a single overview you can filter, navigate, export to HTML, exclude
(with a reason), and turn into Maia prompts.

This guide walks through the full chain in order. Do the steps top to bottom the first time.

---

## ⚠️ Honest warning — third-party alpha/beta tools

CLEVR ACR drives one **unsigned, pre-release, third-party** command-line tool:

- **mxcli** (Mendix Labs) — alpha. It prints its own warning: *"This is a vibe-coded PoC, alpha
  quality, use with caution."* It can **modify or damage project files**.

**Always work with version control, or on a copy of the project.** Commit (or back up) before you
scan, so anything unexpected is easy to revert. These tools are not signed by Microsoft/Mendix, so
Windows SmartScreen and PowerShell execution policy will (correctly) flag them — see *One-time
machine setup*.

---

## 1. Requirements

- **Mendix Studio Pro 11.0 or higher**, on **Windows**. The extension targets the Extensibility API
  11.10 / .NET 10 and will **not load on Mendix 10 or lower**.
- An **existing Mendix project** (a folder containing a `.mpr` file).

---

## 2. One-time machine setup

Do this once per machine.

**(a) Allow local scripts to run.** Open **PowerShell as Administrator** and run:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned
```

This is needed for the install script (and the unsigned mxcli binary). `RemoteSigned`
allows local scripts while still blocking unsigned scripts downloaded from the internet.

**(b) Enable extension development in Studio Pro.** Studio Pro only loads local extensions when this
is on. Either:

- **Edit → Preferences → General / Extensions →** tick **“Enable extension development”**, or
- start Studio Pro once with the flag `--enable-extension-development` on the shortcut.

---

## 3. mxcli (mandatory) — the installer can fetch it for you

The scan needs **mxcli**. You usually don't have to do anything here in advance:

**Recommended — let the installer download it.** When you run `Install-ClevrAcr.ps1` (step 4), if
mxcli isn't already on your `PATH` it offers to download the **latest** release for you. With your
confirmation it downloads `mxcli-windows-amd64.exe`, **verifies its SHA-256 checksum** against the
GitHub release metadata, stores it in `%LOCALAPPDATA%\clevr-acr\mxcli\mxcli.exe`, and writes that
path into `acr-scan-settings.json`. No PATH setup needed. (A previously downloaded copy is reused.)

**Manual alternative.** If you prefer to install it yourself, or have no internet during install:

1. Download the **release binary** `mxcli-windows-amd64.exe` from
   <https://github.com/mendixlabs/mxcli/releases>.
2. Put it in a folder (e.g. `C:\Tools\mxcli\`), and either add that folder to your **PATH** or set its
   full path in `acr-scan-settings.json` (`mxcliPath`).
3. Verify in a **new** terminal window: `mxcli --version`.

> ⚠️ **Use the release binary, not "git clone + make build".** The mxcli repository README also
> describes a developer build route (`git clone … && make build`). **Do not use it on Windows** —
> `make` isn't available there and the build will fail. Always use the prebuilt
> `mxcli-windows-amd64.exe` release asset (the installer does exactly that).

---

## 4. Install the CLEVR ACR extension

1. **Unzip** this package somewhere (e.g. your Downloads folder).
2. **Close Mendix Studio Pro** (so it isn't holding the extension files).
3. Open **PowerShell** in the unzipped package folder (where `Install-ClevrAcr.ps1` lives) and run:

   ```powershell
   .\Install-ClevrAcr.ps1
   ```

   The script asks for your **Mendix project path** (the project folder, or its `.mpr`). It then:
   - copies the extension to `…\<your project>\extensions\clevracr`,
   - finds **mxcli** on your `PATH` — or, if it's missing, offers to download and checksum-verify the
     latest release for you (see step 3),
   - writes a clean `acr-scan-settings.json` for **your** project.

   You can also pass the path directly:

   ```powershell
   .\Install-ClevrAcr.ps1 -ProjectPath "<path to your Mendix project>"
   ```

   If PowerShell still blocks the script (e.g. policy not applied), allow it for this run only:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Install-ClevrAcr.ps1
   ```

   On an upgrade the script preserves your existing, working settings values.

4. **Start Studio Pro** and open the project. Open the panel via **Extensions → CLEVR ACR**.

> **Manual install (alternative):** create an `extensions` folder in your project and copy the
> **entire** `clevracr` folder into it (`…\<project>\extensions\clevracr\Clevr.AcrSpike.dll` and
> everything next to it). Copy the whole folder, never individual DLLs — the extension loads
> `YamlDotNet.dll` at runtime, and without it the expression rules silently produce nothing. Without
> a settings file the extension uses `mxcli` from your `PATH` and scans the currently open app.

---

## 5. Scan

> **No mxlint, no model-source export.** Everything reads through mxcli (catalog + `describe` + its own
> lint rules). There is **no separate tool to install** beyond mxcli (step 3) — just open the panel and scan.

1. **Extensions → CLEVR ACR** to open the panel.
2. Click **Scan** (the quick scan: catalog rules, mxcli's own lint, and manual checks — runs in
   seconds). For the deep microflow/expression analysis (complexity, nested ifs, empty-string checks),
   click **Deepscan** — it scans every microflow/entity and can take a few minutes (a warning is shown).
3. Review the merged improvements (filter by category/severity/source, toggle marketplace modules,
   navigate to documents, exclude with a reason, copy Maia prompts). A quick scan shows a footnote
   noting the deep analysis isn't included.
4. **Export report to HTML** produces a standalone, CLEVR-branded report that opens in your browser.

---

## 6. Configuration — `acr-scan-settings.json`

The installer writes this file for you at `…\extensions\clevracr\acr-scan-settings.json`. It does
**not** ship in the package, so it can never carry someone else's machine paths. You can edit it
afterwards; both fields are optional:

```json
{
  "mxcliPath": "mxcli",
  "projectPath": ""
}
```

| Field         | Meaning                                                                                  |
|---------------|------------------------------------------------------------------------------------------|
| `mxcliPath`   | Path to `mxcli.exe`. Leave as `"mxcli"` if it's on your `PATH`, else give the full path. |
| `projectPath` | Leave empty to scan the **currently open app**. Set a folder or `.mpr` to pin a project. |

---

## 7. Troubleshooting

- **“mxcli could not start” / *The system cannot find the file specified*** → mxcli isn't on your
  `PATH`. Re-check step 3, and open a **new** terminal/Studio Pro after editing PATH. Or set the full
  path in `mxcliPath`.
- **A quick scan shows fewer rules than expected** → that's by design: the deep microflow/expression
  analysis (complexity, nested ifs, empty-string checks, default ReadWrite access) only runs in a
  **Deepscan**. Run a Deepscan for the full set.
- **The panel / Extensions menu item doesn't appear** → extension development isn't enabled, Studio
  Pro wasn't restarted, or you're on Mendix 10 (unsupported — needs 11+). See step 2(b).
- **Diagnostics** → the scan writes a findable debug log under **`<project>\.clevr-acr\`**
  (project dir, rule counts, full exceptions).

---

## What's in this package

```
CLEVR-ACR-extension/
├─ clevracr/                 ← the extension files (copied into your project by the installer)
│  ├─ Clevr.AcrSpike.dll
│  ├─ Clevr.Acr.Normalizer.dll
│  ├─ YamlDotNet.dll          ← required at runtime; copy the whole folder, not individual DLLs
│  ├─ manifest.json
│  ├─ rules.json
│  └─ wwwroot/                ← panel UI + CLEVR logo
├─ Install-ClevrAcr.ps1      ← installer (recommended); writes acr-scan-settings.json
└─ README.md                 ← this guide
```

> `acr-scan-settings.json` is **not** in the package — the installer creates it for your project.
