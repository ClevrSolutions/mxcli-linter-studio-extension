import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { KNOWN_RULE_SOURCES } from "../constants";
import { post } from "../hooks/useMessageBus";
import type { RuleSource } from "../types";
import { btnPrimary, btnSecondary, inputBase, sectionHeading, settingsDesc, settingsEmpty, warningBox } from "../utils/classes";

const selectPreset =
  "border border-clevr-border rounded px-2 py-0.5 text-[12px] bg-white outline-none focus:border-clevr-accent";

const btnDeleteFile =
  "px-3 py-1.5 text-[12px] font-medium bg-white text-sev-major border border-sev-major rounded-[6px] cursor-pointer hover:bg-[#fff7e6] disabled:opacity-45 disabled:cursor-not-allowed";

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
    const updated = [...state.config.ruleSources, source];
    dispatch({ type: "ADD_RULE_SOURCE", source });
    post("SaveRuleSources", { sources: updated });
    setNewUrl("");
    setNewLabel("");
    fetchSource(source.id, source.url, false);
  }

  function removeSource(id: string) {
    const updated = state.config.ruleSources.filter((s) => s.id !== id);
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
    <section className="mb-6">
      <h3 className={sectionHeading}>Lint rule sources</h3>
      <p className={settingsDesc}>
        Add GitHub directory URLs as rule sources. Files are copied into{" "}
        <code>.claude/lint-rules</code> in your Mendix project folder.
        Use <strong>Add files</strong> to copy only new files, or{" "}
        <strong>Replace files</strong> to overwrite existing ones.
      </p>

      <div className="mb-3">
        <select
          className={selectPreset}
          value=""
          onChange={(e) => {
            const preset = KNOWN_RULE_SOURCES[Number(e.target.value)];
            if (!preset) return;
            setNewUrl(preset.url);
            setNewLabel(preset.label);
          }}
        >
          <option value="" disabled>Add a known source…</option>
          {KNOWN_RULE_SOURCES.map((preset, i) => (
            <option key={preset.url} value={i}>{preset.label}</option>
          ))}
        </select>
      </div>

      <div className="flex gap-2 mb-3 flex-wrap">
        <input
          type="url"
          className={`${inputBase} flex-1 min-w-[200px]`}
          placeholder="https://github.com/org/repo/tree/main/path/to/rules"
          value={newUrl}
          onChange={(e) => setNewUrl(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") addSource(); }}
        />
        <input
          type="text"
          className={`${inputBase} w-[140px]`}
          placeholder="Label (optional)"
          value={newLabel}
          onChange={(e) => setNewLabel(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") addSource(); }}
        />
        <button
          type="button"
          className={btnPrimary}
          onClick={addSource}
          disabled={!newUrl.trim() || !isValidUrl}
          title={newUrl.trim() && !isValidUrl ? "URL must start with https://github.com/" : undefined}
        >
          Add source
        </button>
      </div>
      {newUrl.trim() && !isValidUrl && (
        <p className={`${warningBox} mt-0 mb-2`}>
          URL must start with <code>https://github.com/</code> and point to a directory in the tree view.
        </p>
      )}

      {state.config.ruleSources.length === 0 ? (
        <p className={settingsEmpty}>
          No rule sources added yet. Paste a GitHub tree URL above to get started.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          {state.config.ruleSources.map((source) => {
            const status = state.config.ruleSourceFetchStatus[source.id];
            const isFetching = status?.fetching === true;

            return (
              <div key={source.id} className="border border-clevr-border rounded-lg p-3">
                <div className="flex items-baseline gap-2 mb-2 flex-wrap">
                  {source.label && <span className="font-semibold text-[12px]">{source.label}</span>}
                  <code className="font-mono text-[12px] text-clevr-muted">{source.url}</code>
                </div>
                <div className="flex gap-2 flex-wrap">
                  <button
                    type="button"
                    className={btnSecondary}
                    disabled={isFetching}
                    onClick={() => fetchSource(source.id, source.url, false)}
                    title="Copy new files only — skip files that already exist"
                  >
                    Add files
                  </button>
                  <button
                    type="button"
                    className={btnSecondary}
                    disabled={isFetching}
                    onClick={() => fetchSource(source.id, source.url, true)}
                    title="Copy all files, overwriting existing ones"
                  >
                    Replace files
                  </button>
                  <button
                    type="button"
                    className={btnDeleteFile}
                    disabled={isFetching}
                    onClick={() => deleteSourceFiles(source.id, source.url)}
                    title="Delete the files that came from this source from your project"
                  >
                    Delete files
                  </button>
                  <button
                    type="button"
                    className={btnSecondary}
                    disabled={isFetching}
                    onClick={() => removeSource(source.id)}
                    title="Remove this source from the list"
                  >
                    Remove
                  </button>
                </div>
                {status && (
                  <div className="mt-2 text-[12px]">
                    {status.fetching && (
                      <span className="text-clevr-muted italic">
                        {status.progress ?? "Fetching…"}
                      </span>
                    )}
                    {!status.fetching && status.error && (
                      <span className="text-sev-critical">{status.error}</span>
                    )}
                    {!status.fetching && status.lastResult && (() => {
                      const r = status.lastResult;
                      const hasFailed = r.failed > 0;
                      return (
                        <>
                          <span className={hasFailed ? "text-sev-major" : "text-green-action"}>
                            Copied: {r.copied} · Skipped: {r.skipped}
                            {hasFailed && ` · Failed: ${r.failed}`}
                          </span>
                          {r.errors.length > 0 && (
                            <ul className="mt-1 pl-4 text-sev-critical list-disc">
                              {r.errors.map((e, i) => (
                                <li key={i}>{e}</li>
                              ))}
                            </ul>
                          )}
                        </>
                      );
                    })()}
                    {!status.fetching && status.lastDeleteResult && (() => {
                      const r = status.lastDeleteResult;
                      const hasFailed = r.failed > 0;
                      return (
                        <>
                          <span className={hasFailed ? "text-sev-major" : "text-green-action"}>
                            Deleted: {r.deleted}
                            {r.notFound > 0 && ` · Not found: ${r.notFound}`}
                            {hasFailed && ` · Failed: ${r.failed}`}
                          </span>
                          {r.errors.length > 0 && (
                            <ul className="mt-1 pl-4 text-sev-critical list-disc">
                              {r.errors.map((e, i) => (
                                <li key={i}>{e}</li>
                              ))}
                            </ul>
                          )}
                        </>
                      );
                    })()}
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
