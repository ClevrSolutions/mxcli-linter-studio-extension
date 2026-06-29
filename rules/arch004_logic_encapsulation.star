# ARCH004: Microflow logic should stay within module boundaries
#
# Heuristic port of ACR architecture rule `logicencapsulation.md`.
#
# When a module calls out to many other modules, it becomes tightly coupled
# to the rest of the application and cannot be understood or changed in
# isolation. Aim to keep the majority of microflow calls within the same module.
#
# HEURISTIC: uses module_dependencies() which counts all reference types
# (not only microflow calls). Modules with fewer than MIN_EDGES outgoing
# references are skipped to avoid noise on small modules.

RULE_ID = "ARCH004"
RULE_NAME = "LogicEncapsulation"
DESCRIPTION = "Most microflow calls should stay within the module (max 20% cross-module)"
CATEGORY = "architecture"
SEVERITY = "warning"

MAX_CROSS_RATIO = 0.20   # 20% of outgoing call edges may be cross-module
MIN_EDGES = 5            # ignore modules with fewer than this many outgoing edges
SKIP_MODULES = ("System", "Administration")

def check():
    # Accumulate total and cross-module edge counts per source module
    total = {}
    cross = {}
    for dep in module_dependencies():
        src = dep.source_module
        if src in SKIP_MODULES:
            continue
        total[src] = total.get(src, 0) + dep.edges
        if dep.target_module != dep.source_module and dep.target_module not in SKIP_MODULES:
            cross[src] = cross.get(src, 0) + dep.edges

    violations = []
    for mod, t in total.items():
        if t < MIN_EDGES:
            continue
        c = cross.get(mod, 0)
        ratio = c / t
        if ratio > MAX_CROSS_RATIO:
            violations.append(violation(
                message="Module '{}' routes {}% of its references cross-module ({} of {} edges, max {}%). Keep logic local.".format(
                    mod, int(ratio * 100), c, t, int(MAX_CROSS_RATIO * 100)),
                location=location(
                    module=mod,
                    document_type="project",
                    document_name=mod,
                ),
                suggestion="Move shared logic into the owning module or introduce a dedicated integration module.",
            ))
    return violations
