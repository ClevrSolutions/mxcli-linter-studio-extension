// Launches the C# TestHarness for `npm run dev`.
// Defaults to --mock (canned data, no mxcli/project needed). Set CLEVR_DEV_PROJECT to a
// real Mendix project path/.mpr file to scan real data instead.
const { spawn } = require("node:child_process");

const projectPath = process.env.CLEVR_DEV_PROJECT;

const args = ["run", "--project", "src/Clevr.Lint.TestHarness", "--", "--serve"];
if (projectPath) {
  args.push(projectPath);
} else {
  args.push("--mock");
}

const child = spawn("dotnet", args, { stdio: "inherit" });
child.on("exit", (code) => process.exit(code ?? 0));
