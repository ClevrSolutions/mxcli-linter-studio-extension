import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { post } from "../hooks/useMessageBus";
import type { RuleSource } from "../types";

export function RuleSourcesTab() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [newUrl, setNewUrl] = useState("");
  const [newLabel, setNewLabel] = useState("");

  function addSource() {
    const trimmedUrl = newUrl.trim();
    if (!trimmedUrl) return;
    const source: RuleSource = {
      id: crypto.randomUUID(),
      url: trimmedUrl,
      label: newLabel.trim() || undefined,
    };
    const updated = [...state.ruleSources, source];
    dispatch({ type: "ADD_RULE_SOURCE", source });
    post("SaveRuleSources", { sources: updated });
    setNewUrl("");
    setNewLabel("");
    fetchSource(source.id, source.url, false);
  }

  function removeSource(id: string) {
    const updated = state.ruleSources.filter((s) => s.id !== id);
    dispatch({ type: "REMOVE_RULE_SOURCE", id });
    post("SaveRuleSources", { sources: updated });
  }

  function fetchSource(id: string, url: string, replaceExisting: boolean) {
    dispatch({ type: "SET_RULE_SOURCE_FETCH_STATUS", id, status: { fetching: true } });
    post("FetchRuleSource", { id, url, replaceExisting });
  }

  function deleteSourceFiles(id: string, url: string) {
    dispatch({ type: "SET_RULE_SOURCE_FETCH_STATUS", id, status: { fetching: true } });
    post("DeleteRuleSourceFiles", { id, url });
  }

  const isValidUrl = newUrl.trim().startsWith("https://github.com/");

  return (
    <section className="lint-settings-section">
      <h3 className="lint-settings-category">Lint rule sources</h3>
      <p className="lint-settings-desc">
        Add GitHub directory URLs as rule sources. Files are copied into{" "}
        <code>.claude/lint-rules</code> in your Mendix project folder.
        Use <strong>Add files</strong> to copy only new files, or{" "}
        <strong>Replace files</strong> to overwrite existing ones.
      </p>

      <div className="lint-source-add-form">
        <input
          type="url"
          className="lint-source-url-input"
          placeholder="https://github.com/org/repo/tree/main/path/to/rules"
          value={newUrl}
          onChange={(e) => setNewUrl(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") addSource(); }}
        />
        <input
          type="text"
          className="lint-source-label-input"
          placeholder="Label (optional)"
          value={newLabel}
          onChange={(e) => setNewLabel(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") addSource(); }}
        />
        <button
          type="button"
          className="lint-settings-save"
          onClick={addSource}
          disabled={!newUrl.trim() || !isValidUrl}
          title={newUrl.trim() && !isValidUrl ? "URL must start with https://github.com/" : undefined}
        >
          Add source
        </button>
      </div>
      {newUrl.trim() && !isValidUrl && (
        <p className="lint-mxcli-warning" style={{ marginTop: 0, marginBottom: 8 }}>
          URL must start with <code>https://github.com/</code> and point to a directory in the tree view.
        </p>
      )}

      {state.ruleSources.length === 0 ? (
        <p className="lint-settings-empty">
          No rule sources added yet. Paste a GitHub tree URL above to get started.
        </p>
      ) : (
        <div className="lint-source-list">
          {state.ruleSources.map((source) => {
            const status = state.ruleSourceFetchStatus[source.id];
            const isFetching = status?.fetching === true;

            return (
              <div key={source.id} className="lint-source-row">
                <div className="lint-source-info">
                  {source.label && <span className="lint-source-label">{source.label}</span>}
                  <code className="lint-mxcli-path">{source.url}</code>
                </div>
                <div className="lint-source-actions">
                  <button
                    type="button"
                    className="lint-mxcli-browse-btn"
                    disabled={isFetching}
                    onClick={() => fetchSource(source.id, source.url, false)}
                    title="Copy new files only — skip files that already exist"
                  >
                    Add files
                  </button>
                  <button
                    type="button"
                    className="lint-mxcli-browse-btn"
                    disabled={isFetching}
                    onClick={() => fetchSource(source.id, source.url, true)}
                    title="Copy all files, overwriting existing ones"
                  >
                    Replace files
                  </button>
                  <button
                    type="button"
                    className="lint-source-delete-btn"
                    disabled={isFetching}
                    onClick={() => deleteSourceFiles(source.id, source.url)}
                    title="Delete the files that came from this source from your project"
                  >
                    Delete files
                  </button>
                  <button
                    type="button"
                    className="lint-source-remove-btn"
                    disabled={isFetching}
                    onClick={() => removeSource(source.id)}
                    title="Remove this source from the list"
                  >
                    Remove
                  </button>
                </div>
                {status && (
                  <div className="lint-source-status">
                    {status.fetching && (
                      <span className="lint-source-status-fetching">
                        {status.progress ?? "Fetching…"}
                      </span>
                    )}
                    {!status.fetching && status.error && (
                      <span className="lint-source-status-error">{status.error}</span>
                    )}
                    {!status.fetching && status.lastResult && (() => {
                      const r = status.lastResult;
                      const hasFailed = r.failed > 0;
                      return (
                        <>
                          <span className={hasFailed ? "lint-source-status-warn" : "lint-source-status-ok"}>
                            Copied: {r.copied} · Skipped: {r.skipped}
                            {hasFailed && ` · Failed: ${r.failed}`}
                          </span>
                          {r.errors.length > 0 && (
                            <ul className="lint-source-error-list">
                              {r.errors.map((e, i) => (
                                <li key={i} className="lint-source-status-error">{e}</li>
                              ))}
                            </ul>
                          )}
                        </>
                      );
                    })()}
                    {!status.fetching && status.lastDeleteResult && (
                      <span className="lint-source-status-ok">
                        Deleted: {status.lastDeleteResult.deleted}
                        {status.lastDeleteResult.notFound > 0 && ` · Not found: ${status.lastDeleteResult.notFound}`}
                      </span>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
