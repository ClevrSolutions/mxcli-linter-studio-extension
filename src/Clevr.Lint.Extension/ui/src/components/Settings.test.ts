import { describe, expect, it } from "vitest";
import { isPendingChanged, stripRules } from "./Settings";
import type { LinterConfigRule } from "../types";

// Regression coverage for U1/U2 in docs/review_07_07.md: save() must commit the
// stripped rules object (not raw pending with phantom no-op keys), and the
// pending/saved diff must be computed over the same stripped representation on
// both sides so phantom keys don't mask, or fabricate, real changes.

describe("stripRules", () => {
  it("drops entries that are no-ops (enabled left unset, severity left inherit)", () => {
    const rules: Record<string, LinterConfigRule> = {
      NOOP: { enabled: undefined, severity: "inherit" },
      DISABLED: { enabled: false },
      SEVERITY: { severity: "error" },
    };
    expect(stripRules(rules)).toEqual({
      DISABLED: { enabled: false, severity: undefined },
      SEVERITY: { enabled: undefined, severity: "error" },
    });
  });

  it("produces an identical stripped form for a phantom toggle-off-then-on entry", () => {
    // Toggling a rule off then back on in the UI can leave a rule keyed with an
    // explicit `{ enabled: undefined }` instead of no key at all — the stripped
    // form must treat that the same as never having touched the rule.
    const touchedAndReverted: Record<string, LinterConfigRule> = { A: { enabled: undefined } };
    const neverTouched: Record<string, LinterConfigRule> = {};
    expect(stripRules(touchedAndReverted)).toEqual(stripRules(neverTouched));
  });
});

describe("isPendingChanged", () => {
  it("returns false when pending only contains phantom no-op entries", () => {
    const saved: Record<string, LinterConfigRule> = {};
    const pending: Record<string, LinterConfigRule> = { A: { enabled: undefined, severity: "inherit" } };
    expect(isPendingChanged(pending, saved, [], [])).toBe(false);
  });

  it("still detects a real change when a same-count phantom key masks it (U2 scenario)", () => {
    // Saved config has rule B disabled. User clicks "Enable all" (deletes B from
    // pending), then toggles rule A off and back on, leaving a phantom
    // `{ enabled: undefined }` entry for A. Key counts now match (1 vs 1), but the
    // content differs: B's real removal vs. A's no-op addition.
    const saved: Record<string, LinterConfigRule> = { B: { enabled: false } };
    const pending: Record<string, LinterConfigRule> = { A: { enabled: undefined } };
    expect(isPendingChanged(pending, saved, [], [])).toBe(true);
  });

  it("detects a real severity change", () => {
    const saved: Record<string, LinterConfigRule> = { R: { severity: "warning" } };
    const pending: Record<string, LinterConfigRule> = { R: { severity: "error" } };
    expect(isPendingChanged(pending, saved, [], [])).toBe(true);
  });

  it("detects excluded-module changes independently of rules", () => {
    expect(isPendingChanged({}, {}, ["ModuleA"], [])).toBe(true);
    expect(isPendingChanged({}, {}, ["ModuleA"], ["ModuleA"])).toBe(false);
  });
});
