import type { Violation } from "../types";
import { groupByRule } from "../utils/grouping";
import { RuleCard } from "./RuleCard";

interface Props {
  category: string;
  items: Violation[];
  interactive?: boolean;
  isFixed?: boolean;
}

export function CategoryGroup({ category, items, interactive = true, isFixed = false }: Props) {
  const rules = groupByRule(items).sort((a, b) =>
    a.rule.ruleId.localeCompare(b.rule.ruleId)
  );

  return (
    <div className="mb-4">
      <div className="flex items-center gap-2 mb-2 pb-1 border-b border-clevr-border">
        <h3 className="text-[13px] font-semibold m-0">{category}</h3>
        <span className="ml-auto text-[12px] text-clevr-muted">{items.length} improvements</span>
      </div>
      {rules.map((r) => (
        <RuleCard key={r.rule.ruleId} rule={r.rule} items={r.items} interactive={interactive} isFixed={isFixed} />
      ))}
    </div>
  );
}
