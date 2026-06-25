import type { Violation } from "../types";
import { groupByRule } from "../utils/grouping";
import { RuleCard } from "./RuleCard";

interface Props {
  category: string;
  items: Violation[];
  interactive?: boolean;
}

export function CategoryGroup({ category, items, interactive = true }: Props) {
  const rules = groupByRule(items).sort((a, b) => {
    const ak = a.rule.kind === "lint" ? 0 : 1;
    const bk = b.rule.kind === "lint" ? 0 : 1;
    return ak - bk || a.rule.ruleId.localeCompare(b.rule.ruleId);
  });

  return (
    <div className="lint-group">
      <div className="lint-group-head">
        <h3>{category}</h3>
        <span className="lint-group-count">{items.length} improvements</span>
      </div>
      {rules.map((r) => (
        <RuleCard key={r.rule.ruleId} rule={r.rule} items={r.items} interactive={interactive} />
      ))}
    </div>
  );
}
