# MAINT004: Modules should not have too many security roles
#
# Ported from ACR maintainability rule `modulerolesamount.md`.
#
# A module with many module roles requires a correspondingly complex set of
# access rules across all entities, microflows, and pages. Keep role counts
# low to make the security model understandable and auditable.

RULE_ID = "MAINT004"
RULE_NAME = "MaxModuleRoles"
DESCRIPTION = "Modules should not have more than 5 module roles"
CATEGORY = "maintainability"
SEVERITY = "info"

MAX_ROLES = 5
SKIP_MODULES = ("System", "Administration")

def check():
    counts = {}
    first_role = {}
    for role in module_roles():
        if role.module_name in SKIP_MODULES:
            continue
        mod = role.module_name
        counts[mod] = counts.get(mod, 0) + 1
        if mod not in first_role:
            first_role[mod] = role

    violations = []
    for mod, count in counts.items():
        if count > MAX_ROLES:
            role = first_role[mod]
            violations.append(violation(
                message="Module '{}' has {} module roles (max {}). Simplify the security model.".format(
                    mod, count, MAX_ROLES),
                location=location(
                    module=role.module_name,
                    document_type="ModuleRole",
                    document_name=role.name,
                ),
                suggestion="Consolidate roles in '{}' to reduce access rule complexity.".format(mod),
            ))
    return violations
