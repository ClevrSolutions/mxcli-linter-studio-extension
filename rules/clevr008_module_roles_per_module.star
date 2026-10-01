# CLEVR008: Project role should have at most one module role per module
#
# Mirrors the CLEVR ACR rule "Project role should have at most one module role
# per module" (Performance, Critical).
#
# When a user role carries two module roles from the same module, the runtime
# evaluates both sets of access rules for every object and unions the result.
# That costs performance, and it makes the effective permissions hard to reason
# about: neither module role alone tells you what the user can do.
#
# Note this is NOT mxcli's bundled CONV008, which checks the other direction -
# that each module role maps to exactly one user role. Both can be useful; they
# catch different mistakes.
#
# Install: copy to .claude/lint-rules/clevr008_module_roles_per_module.star

RULE_ID = "CLEVR008"
RULE_NAME = "MultipleModuleRolesPerModule"
DESCRIPTION = "A user role should carry at most one module role from any single module"
CATEGORY = "performance"
SEVERITY = "error"


def check():
    violations = []

    # user role -> module -> list of module roles
    byRole = {}
    order = []

    for rm in role_mappings():
        key = rm.user_role_name
        if key not in byRole:
            byRole[key] = {}
            order.append(key)

        mod = rm.module_name
        if mod not in byRole[key]:
            byRole[key][mod] = []
        byRole[key][mod].append(rm.module_role_name)

    for userRole in order:
        for mod in sorted(byRole[userRole].keys()):
            roles = byRole[userRole][mod]
            if len(roles) <= 1:
                continue

            violations.append(violation(
                message = "User role '{}' carries {} module roles from '{}': {}. The runtime evaluates every one of them on each object.".format(
                    userRole, len(roles), mod, ", ".join(sorted(roles)),
                ),
                location = location(
                    module = "",
                    document_type = "security",
                    document_name = "ProjectSecurity",
                ),
                suggestion = "Give '{}' a single module role in '{}', merging the access rules if both are genuinely needed.".format(
                    userRole, mod,
                ),
            ))

    return violations
