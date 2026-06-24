using System.Diagnostics;

namespace Clevr.AcrSpike;

/// <summary>
/// Proof of assumption (a): a C# extension component can start an external process.
/// This is plain .NET 10 (System.Diagnostics.Process) — there is no Mendix-specific
/// API or restriction. Intentionally starts innocuous (cmd /c echo test).
/// </summary>
public static class ProcessRunner
{
    public record Result(string StdOut, string StdErr, int ExitCode, bool Ok, string? Error);

    /// <summary>
    /// Runs a single command and captures stdout/stderr/exitcode.
    ///
    /// CRUCIAL (deadlock fix): stdout and stderr are drained PARALLEL async
    /// (ReadToEndAsync) WHILE the process is running. A process that produces a lot of output
    /// (mxlint lint → hundreds of lines) would otherwise block on a full OS pipe and never
    /// reach exit. Reading sequentially (first stdout completely, then stderr) also deadlocks,
    /// because the stderr buffer fills up while stdout is being read.
    ///
    /// <paramref name="timeoutMs"/> ≤ 0 = wait indefinitely; otherwise the process is
    /// killed after the timeout (Kill of the entire process tree) with a diagnostic message.
    ///
    /// Synchronous from the outside (suitable to run inside Task.Run); blocks the calling
    /// thread until the process finishes — but without pipe deadlock.
    /// </summary>
    public static Result Run(string fileName, string arguments, string? workingDirectory = null, int timeoutMs = 0)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // mxcli needs its cache relative to the working directory.
            // Without this the process inherits the cwd of Studio Pro (wrong directory).
            if (!string.IsNullOrEmpty(workingDirectory))
                psi.WorkingDirectory = workingDirectory;

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Start draining BOTH streams immediately (parallel) → no pipe deadlock.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            bool exited = timeoutMs > 0 ? process.WaitForExit(timeoutMs) : WaitForExitInfinite(process);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return new Result(
                    GetPartial(stdoutTask), GetPartial(stderrTask), -1, false,
                    $"Process exceeded the timeout of {timeoutMs} ms and was killed: {fileName} {arguments}");
            }

            // Process has finished → the read tasks complete (pipe closed).
            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            return new Result(stdout, stderr, process.ExitCode, process.ExitCode == 0, null);
        }
        catch (Exception ex)
        {
            // E.g. file not found (binary not on PATH) — show the error in the UI.
            return new Result(string.Empty, string.Empty, -1, false, ex.Message);
        }
    }

    private static bool WaitForExitInfinite(Process process)
    {
        process.WaitForExit();
        return true;
    }

    /// <summary>Best-effort partial output after a timeout/kill — do not block forever.</summary>
    private static string GetPartial(Task<string> readTask)
    {
        try { return readTask.Wait(2000) ? readTask.Result : ""; }
        catch { return ""; }
    }

    /// <summary>The command for this spike. Start innocuous; switch to mxcli later.</summary>
    public static Result RunSpikeCommand()
    {
        // Step 1 (innocuous, always present on Windows):
        return Run("cmd.exe", "/c echo test");

        // Step 2 (once step 1 works) — remove the comment to try the real engine:
        // return Run("mxcli", "--version");
    }
}
