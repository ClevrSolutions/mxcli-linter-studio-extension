// Launches the C# TestHarness for `npm run dev`.
// Project path resolution order:
//   1. CLEVR_DEV_PROJECT env var
//   2. `projectPath` in src/Clevr.Lint.Extension/lint-scan-settings.json (gitignored)
//   3. neither → --mock (canned data, no mxcli/project needed)
const { spawn } = require("node:child_process");
const fs = require("node:fs");
const path = require("node:path");

function resolveProjectPath() {
  const fromEnv = process.env.CLEVR_DEV_PROJECT;
  if (fromEnv) return { projectPath: fromEnv, source: "CLEVR_DEV_PROJECT env var" };

  const settingsFile = path.join(
    __dirname, "..", "src", "Clevr.Lint.Extension", "lint-scan-settings.json");
  try {
    const settings = JSON.parse(fs.readFileSync(settingsFile, "utf8"));
    const p = settings.projectPath;
    if (typeof p === "string" && p.trim() !== "" && fs.existsSync(p) && fs.statSync(p).isDirectory()) {
      return { projectPath: p, source: "lint-scan-settings.json" };
    }
  } catch {
    // File missing or unparseable — fall through to mock.
  }

  return { projectPath: null, source: "mock data (no project configured)" };
}

const { projectPath, source } = resolveProjectPath();
console.log(`[dev-harness] project source: ${source}${projectPath ? ` — ${projectPath}` : ""}`);

const args = ["run", "--project", "src/Clevr.Lint.TestHarness", "--", "--serve"];
if (projectPath) {
  args.push(projectPath);
} else {
  args.push("--mock");
}

const child = spawn("dotnet", args, { stdio: "inherit" });
child.on("exit", (code) => process.exit(code ?? 0));
