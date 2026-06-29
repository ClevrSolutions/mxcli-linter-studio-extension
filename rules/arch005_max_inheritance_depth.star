# ARCH005: Entity inheritance chains should not be too deep
#
# Ported from ACR architecture rule `multilevelinheritance.md`.
#
# Deep inheritance hierarchies are hard to understand and to maintain.
# Mendix specialization is useful for polymorphism but should be limited
# to at most 2 levels (grandparent → parent → child).

RULE_ID = "ARCH005"
RULE_NAME = "MaxInheritanceDepth"
DESCRIPTION = "Entity inheritance chains should not exceed 2 levels"
CATEGORY = "architecture"
SEVERITY = "warning"

MAX_DEPTH = 2
SKIP_MODULES = ("System", "Administration")
MAX_WALK = 20   # guard against cycles in malformed data

def check():
    # Build qualified-name → entity lookup for chain traversal
    by_qname = {}
    for e in entities():
        by_qname[e.qualified_name] = e

    violations = []
    for e in entities():
        if e.is_external:
            continue
        if e.module_name in SKIP_MODULES:
            continue
        if e.entity_type != "Persistent":
            continue
        if not e.generalization:
            continue

        # Walk the generalization chain and count levels
        depth = 0
        current_qname = e.generalization
        for _ in range(MAX_WALK):
            if not current_qname or current_qname not in by_qname:
                break
            depth += 1
            current_qname = by_qname[current_qname].generalization

        if depth > MAX_DEPTH:
            violations.append(violation(
                message="Entity '{}' has an inheritance depth of {} (max {}). Flatten the hierarchy.".format(
                    e.qualified_name, depth, MAX_DEPTH),
                location=location(
                    module=e.module_name,
                    document_type="Entity",
                    document_name=e.qualified_name,
                ),
                suggestion="Reduce the specialization chain to at most {} levels.".format(MAX_DEPTH),
            ))
    return violations
