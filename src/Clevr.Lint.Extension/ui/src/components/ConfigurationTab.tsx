import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { post } from "../hooks/useMessageBus";

const SOURCE_LABELS: Record<string, string> = {
  path:       "System PATH",
  clevrLint:  "CLEVR Lint",
  custom:     "Custom",
  notFound:   "Not found",
};

export function ConfigurationTab() {
  const state    = useAppState();
  const dispatch = useAppDispatch();
  const info     = state.mxcliInfo;

  const [editMode, setEditMode]   = useState(false);
  const [editPath, setEditPath]   = useState("");

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

  if (info === null) {
    return (
      <section className="lint-settings-section">
        <h3 className="lint-settings-category">mxcli</h3>
        <p className="lint-settings-empty">Loading mxcli information…</p>
      </section>
    );
  }

  const isDownloading = state.mxcliDownloading;
  const progress      = state.mxcliDownloadProgress;
  const downloadLabel = info.found ? "Download latest version" : "Download mxcli";

  return (
    <section className="lint-settings-section">
      <h3 className="lint-settings-category">mxcli</h3>
      <p className="lint-settings-desc">
        CLEVR Lint uses mxcli, the official Mendix Labs linting tool. The version below is
        used for all scans.
      </p>

      <table className="lint-settings-table lint-mxcli-table">
        <tbody>
          <tr>
            <th>Source</th>
            <td>
              <span className={`lint-mxcli-source-badge lint-mxcli-source-${info.source}`}>
                {SOURCE_LABELS[info.source] ?? info.source}
              </span>
            </td>
          </tr>
          <tr>
            <th>Location</th>
            <td className="lint-mxcli-location-cell">
              {editMode ? (
                <div className="lint-mxcli-edit-row">
                  <input
                    type="text"
                    className="lint-mxcli-path-input"
                    value={editPath}
                    onChange={(e) => setEditPath(e.target.value)}
                    placeholder="C:\path\to\mxcli.exe"
                    autoFocus
                    onKeyDown={(e) => { if (e.key === "Enter") applyEdit(); if (e.key === "Escape") cancelEdit(); }}
                  />
                  <button type="button" className="lint-mxcli-apply-btn" onClick={applyEdit} disabled={!editPath.trim()}>Apply</button>
                  <button type="button" className="lint-mxcli-cancel-btn" onClick={cancelEdit}>Cancel</button>
                </div>
              ) : (
                <div className="lint-mxcli-location-row">
                  {info.resolvedPath
                    ? <code className="lint-mxcli-path">{info.resolvedPath}</code>
                    : <span className="lint-settings-empty">—</span>}
                  <div className="lint-mxcli-location-btns">
                    <button type="button" className="lint-mxcli-browse-btn" title="Browse for mxcli.exe" onClick={handleBrowse}>Browse…</button>
                    <button type="button" className="lint-mxcli-browse-btn" title="Type or paste a path" onClick={startEdit}>Set path…</button>
                  </div>
                </div>
              )}
            </td>
          </tr>
          <tr>
            <th>Version</th>
            <td>{info.version ?? <span className="lint-settings-empty">—</span>}</td>
          </tr>
          {info.downloadedAt && (
            <tr>
              <th>Updated</th>
              <td>{info.downloadedAt}</td>
            </tr>
          )}
        </tbody>
      </table>

      {!info.found && (
        <p className="lint-mxcli-warning">
          mxcli was not found. Download it below or point to an existing installation using Browse.
        </p>
      )}

      <div className="lint-mxcli-actions">
        <button
          type="button"
          className="lint-settings-save"
          disabled={isDownloading}
          onClick={handleDownload}
        >
          {isDownloading ? "Downloading…" : downloadLabel}
        </button>

        {isDownloading && progress !== null && (
          <div className="lint-mxcli-progress-wrap">
            <div className="lint-mxcli-progress-bar-track">
              <div className="lint-mxcli-progress-bar-fill" style={{ width: `${progress}%` }} />
            </div>
            <span className="lint-mxcli-progress-label">{progress}%</span>
          </div>
        )}
      </div>
    </section>
  );
}
