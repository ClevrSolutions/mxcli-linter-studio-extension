using System.Runtime.CompilerServices;

// Lets Clevr.Lint.TestHarness reuse internal helpers (e.g. NativeFileDialog) instead of
// duplicating them or having to make them public just for the dev harness.
[assembly: InternalsVisibleTo("Clevr.Lint.TestHarness")]
