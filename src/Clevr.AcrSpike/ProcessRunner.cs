using System.Diagnostics;

namespace Clevr.AcrSpike;

/// <summary>
/// Bewijs van aanname (a): een C#-extensiecomponent kan een extern proces starten.
/// Dit is plain .NET 10 (System.Diagnostics.Process) — er is geen Mendix-specifieke
/// API of restrictie. Begint bewust onschuldig (cmd /c echo test).
/// </summary>
public static class ProcessRunner
{
    public record Result(string StdOut, string StdErr, int ExitCode, bool Ok, string? Error);

    /// <summary>
    /// Draait één commando en vangt stdout/stderr/exitcode op.
    ///
    /// CRUCIAAL (deadlock-fix): stdout én stderr worden PARALLEL async leeggetrokken
    /// (ReadToEndAsync) TERWIJL het proces draait. Een proces dat veel output produceert
    /// (mxlint lint → honderden regels) blokkeert anders op een volle OS-pijp en komt
    /// nooit bij exit. Sequentieel lezen (eerst stdout helemaal, dan pas stderr) deadlockt
    /// óók, omdat de stderr-buffer volloopt terwijl stdout wordt gelezen.
    ///
    /// <paramref name="timeoutMs"/> ≤ 0 = oneindig wachten; anders wordt het proces na de
    /// timeout afgebroken (Kill van de hele process-tree) met een diagnostische melding.
    ///
    /// Synchroon van buiten (geschikt om in Task.Run te draaien); blokkeert de aanroepende
    /// thread tot het proces eindigt — maar zonder pipe-deadlock.
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

            // mxcli/mxlint hebben hun cache relatief t.o.v. de working directory nodig.
            // Zonder dit erft het proces de cwd van Studio Pro (verkeerde map).
            if (!string.IsNullOrEmpty(workingDirectory))
                psi.WorkingDirectory = workingDirectory;

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Start het leegtrekken van BEIDE streams meteen (parallel) → geen pipe-deadlock.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            bool exited = timeoutMs > 0 ? process.WaitForExit(timeoutMs) : WaitForExitInfinite(process);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return new Result(
                    GetPartial(stdoutTask), GetPartial(stderrTask), -1, false,
                    $"Proces overschreed de timeout van {timeoutMs} ms en is afgebroken: {fileName} {arguments}");
            }

            // Proces is geëindigd → de read-tasks ronden af (pijp gesloten).
            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            return new Result(stdout, stderr, process.ExitCode, process.ExitCode == 0, null);
        }
        catch (Exception ex)
        {
            // Bv. bestand niet gevonden (binary niet op PATH) — toon de fout in de UI.
            return new Result(string.Empty, string.Empty, -1, false, ex.Message);
        }
    }

    private static bool WaitForExitInfinite(Process process)
    {
        process.WaitForExit();
        return true;
    }

    /// <summary>Best-effort gedeeltelijke output na een timeout/kill — blokkeer niet eeuwig.</summary>
    private static string GetPartial(Task<string> readTask)
    {
        try { return readTask.Wait(2000) ? readTask.Result : ""; }
        catch { return ""; }
    }

    /// <summary>Het commando voor deze spike. Begin onschuldig; switch later naar mxcli.</summary>
    public static Result RunSpikeCommand()
    {
        // Stap 1 (onschuldig, altijd aanwezig op Windows):
        return Run("cmd.exe", "/c echo test");

        // Stap 2 (zodra stap 1 werkt) — haal het commentaar weg om de echte engine te proberen:
        // return Run("mxcli", "--version");
    }
}
