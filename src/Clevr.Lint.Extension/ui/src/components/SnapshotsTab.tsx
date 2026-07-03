import { useState } from "react";
import { useAppDispatch, useAppState } from "../context/AppContext";
import { post } from "../hooks/useMessageBus";
import { relativeTime } from "../utils/time";
import { moduleOf } from "../utils/origins";
import { btnPrimary, btnSecondary, sectionHeading, settingsDesc, settingsEmpty } from "../utils/classes";
import { ConfirmDialog } from "./dialogs/ConfirmDialog";
import type { BaselineEntry } from "../types";

function snapshotStats(b: BaselineEntry) {
  const modules = new Set(b.violations.map(moduleOf));
  const rules = new Set(b.violations.map((v) => v.ruleId));
  return { moduleCount: modules.size, ruleCount: rules.size, violationCount: b.violations.length };
}

export function SnapshotsTab() {
  const state = useAppState();
  const dispatch = useAppDispatch();
  const [pendingRemoveId, setPendingRemoveId] = useState<string | null>(null);

  const canSave = state.scan.scanHasRun && !state.scan.scanStreaming;

  function saveSnapshot() {
    post("SaveBaseline", { violations: state.scan.violations, savedAt: Date.now() });
  }

  function removeSnapshot(id: string) {
    post("DeleteBaseline", { id });
  }

  return (
    <section className="mb-6">
      <div className="flex items-start justify-between gap-3 mb-3">
        <div>
          <h3 className={sectionHeading}>Snapshots</h3>
          <p className={`${settingsDesc} m-0`}>
            A snapshot saves the current scan result so future scans can highlight only new or fixed
            violations. Up to 5 snapshots are kept, stored in{" "}
            <code>.clevr-lint/baselines.json</code> in your project directory.
          </p>
        </div>
        <button
          type="button"
          className={`${btnPrimary} shrink-0`}
          disabled={!canSave}
          title={canSave ? "Save the current scan result as a new snapshot" : "Run a scan first"}
          onClick={saveSnapshot}
        >
          Save current scan
        </button>
      </div>

      {state.baseline.baselines.length === 0 ? (
        <p className={settingsEmpty}>No snapshots saved yet — run a scan and save one to get started.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {state.baseline.baselines.map((b) => {
            const { moduleCount, ruleCount, violationCount } = snapshotStats(b);
            return (
              <div
                key={b.id}
                className="flex items-center justify-between gap-3 border border-clevr-border rounded-lg px-3 py-2"
              >
                <div className="flex flex-col gap-1">
                  <span className="text-[12px]">
                    {relativeTime(new Date(b.savedAt).getTime())}
                    {b.gitRevision ? ` (${b.gitRevision})` : ""}
                  </span>
                  <span className="text-[11px] text-clevr-muted">
                    {violationCount} violation{violationCount === 1 ? "" : "s"} · {ruleCount} rule{ruleCount === 1 ? "" : "s"} · {moduleCount} module{moduleCount === 1 ? "" : "s"}
                  </span>
                </div>
                <button
                  type="button"
                  className={btnSecondary}
                  title="Remove this snapshot"
                  onClick={() => setPendingRemoveId(b.id)}
                >
                  Remove
                </button>
              </div>
            );
          })}
        </div>
      )}

      {pendingRemoveId && (
        <ConfirmDialog
          title="Remove snapshot"
          message="This snapshot will be permanently deleted. This can't be undone."
          confirmLabel="Remove snapshot"
          onConfirm={() => removeSnapshot(pendingRemoveId)}
          onClose={() => setPendingRemoveId(null)}
        />
      )}
    </section>
  );
}
