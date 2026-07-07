import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { post } from "../hooks/useMessageBus";
import { btnPrimary, btnSecondary, inputBase, sectionHeading, settingsDesc, settingsEmpty, tableBase, warningBox } from "../utils/classes";

const LOG_LEVEL_OPTIONS = ["error", "info", "trace"] as const;
const LOG_LEVEL_LABELS: Record<string, string> = {
  error: "Error (default)",
  info:  "Info",
  trace: "Trace (verbose — includes full mx diff output)",
};

const SOURCE_LABELS: Record<string, string> = {
  path:       "System PATH",
  clevrLint:  "CLEVR Lint",
  custom:     "Custom",
  notFound:   "Not found",
};

const SOURCE_BADGE_COLORS: Record<string, string> = {
  path:      "bg-[#e7eef6] text-clevr-accent",
  clevrLint: "bg-[#d9f5e8] text-green-action",
  custom:    "bg-[#fff3cd] text-[#856404]",
  notFound:  "bg-[#f8d7da] text-sev-blocker",
};

export function ConfigurationTab() {
  const state    = useAppState();
  const dispatch = useAppDispatch();
  const info     = state.config.mxcliInfo;
  const logLevel = state.config.logLevel;

  const [editMode, setEditMode] = useState(false);
  const [editPath, setEditPath] = useState("");

  function handleLogLevelChange(level: string) {
    dispatch({ type: "SET_LOG_LEVEL", level });
    post("SetLogLevel", { level });
  }

  function handleDownload() {
    dispatch({ type: "MXCLI_DOWNLOAD_STARTED" });
    post("DownloadMxcli");
  }

  function handleBrowse() {
    post("BrowseMxcliPath");
  }

  function startEdit() {
    setEditPath(info?.resolvedPath ?? "");
    setEditMode(true);
  }

  function cancelEdit() {
    setEditMode(false);
    setEditPath("");
  }

  function applyEdit() {
    if (editPath.trim()) {
      post("SetMxcliPath", { path: editPath.trim() });
    }
    setEditMode(false);
    setEditPath("");
  }

  const logLevelSection = (
    <section className="mb-6">
      <h3 className={sectionHeading}>Debug log</h3>
      <p className={settingsDesc}>
        Diagnostics are written to <code className="font-mono text-[12px]">.clevr-lint\clevr-lint-debug.log</code> in
        the project folder. Higher levels write more (Trace includes full mx diff output per scan) — leave this on
        Error unless you're troubleshooting.
      </p>
      <select
        className={`${inputBase} w-auto`}
        value={logLevel}
        onChange={(e) => handleLogLevelChange(e.target.value)}
      >
        {LOG_LEVEL_OPTIONS.map((level) => (
          <option key={level} value={level}>{LOG_LEVEL_LABELS[level]}</option>
        ))}
      </select>
    </section>
  );

  if (info === null) {
    return (
      <>
        <section className="mb-6">
          <h3 className={sectionHeading}>mxcli</h3>
          <p className={settingsEmpty}>Loading mxcli information…</p>
        </section>
        {logLevelSection}
      </>
    );
  }

  const isDownloading = state.config.mxcliDownloading;
  const progress      = state.config.mxcliDownloadProgress;
  const downloadLabel = info.found ? "Download latest version" : "Download mxcli";
  const badgeColor    = SOURCE_BADGE_COLORS[info.source] ?? "bg-clevr-card text-clevr-muted";

  return (
    <>
    <section className="mb-6">
      <h3 className={sectionHeading}>mxcli</h3>
      <p className={settingsDesc}>
        CLEVR Lint uses mxcli, the official Mendix Labs linting tool. The version below is
        used for all scans.
      </p>

      <table className={`${tableBase} mb-4`}>
        <tbody>
          <tr>
            <th className="text-left py-1.5 pr-4 text-clevr-muted font-medium text-[12px] border-b border-clevr-border w-[90px]">Source</th>
            <td className="py-1.5 border-b border-clevr-border">
              <span className={`inline-block px-2 py-px rounded text-[11px] font-semibold ${badgeColor}`}>
                {SOURCE_LABELS[info.source] ?? info.source}
              </span>
            </td>
          </tr>
          <tr>
            <th className="text-left py-1.5 pr-4 text-clevr-muted font-medium text-[12px] border-b border-clevr-border">Location</th>
            <td className="py-1.5 border-b border-clevr-border">
              {editMode ? (
                <div className="flex gap-2 items-center">
                  <input
                    type="text"
                    className={`${inputBase} flex-1 font-mono`}
                    value={editPath}
                    onChange={(e) => setEditPath(e.target.value)}
                    placeholder="C:\path\to\mxcli.exe"
                    autoFocus
                    onKeyDown={(e) => { if (e.key === "Enter") applyEdit(); if (e.key === "Escape") cancelEdit(); }}
                  />
                  <button type="button" className={btnPrimary} onClick={applyEdit} disabled={!editPath.trim()}>Apply</button>
                  <button type="button" className={btnSecondary} onClick={cancelEdit}>Cancel</button>
                </div>
              ) : (
                <div className="flex items-center gap-3 flex-wrap">
                  {info.resolvedPath
                    ? <code className="font-mono text-[12px]">{info.resolvedPath}</code>
                    : <span className={settingsEmpty}>—</span>}
                  <div className="flex gap-2">
                    <button type="button" className={btnSecondary} title="Browse for mxcli.exe" onClick={handleBrowse}>Browse…</button>
                    <button type="button" className={btnSecondary} title="Type or paste a path" onClick={startEdit}>Set path…</button>
                  </div>
                </div>
              )}
            </td>
          </tr>
          <tr>
            <th className="text-left py-1.5 pr-4 text-clevr-muted font-medium text-[12px] border-b border-clevr-border">Version</th>
            <td className="py-1.5 border-b border-clevr-border">
              {info.version ?? <span className={settingsEmpty}>—</span>}
            </td>
          </tr>
          {info.downloadedAt && (
            <tr>
              <th className="text-left py-1.5 pr-4 text-clevr-muted font-medium text-[12px] border-b border-clevr-border">Updated</th>
              <td className="py-1.5 border-b border-clevr-border">{info.downloadedAt}</td>
            </tr>
          )}
        </tbody>
      </table>

      {!info.found && (
        <p className={warningBox}>
          mxcli was not found. Download it below or point to an existing installation using Browse.
        </p>
      )}

      <div className="flex items-center gap-3 mt-3">
        <button
          type="button"
          className={btnPrimary}
          disabled={isDownloading}
          onClick={handleDownload}
        >
          {isDownloading ? "Downloading…" : downloadLabel}
        </button>

        {isDownloading && progress !== null && (
          <div className="flex items-center gap-3 flex-1">
            <div className="flex-1 h-[6px] bg-clevr-border rounded-[3px] overflow-hidden">
              <div
                className="h-full bg-clevr-accent rounded-[3px] transition-[width] duration-150 ease-linear"
                style={{ width: `${progress}%` }}
              />
            </div>
            <span className="text-[12px] text-clevr-muted tabular-nums w-[32px]">{progress}%</span>
          </div>
        )}
      </div>
    </section>
    {logLevelSection}
    </>
  );
}
