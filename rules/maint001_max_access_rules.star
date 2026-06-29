# MAINT001: Entities should not have too many access rules
#
# Ported from ACR maintainability rule `amountaccessrules.md`.
#
# A high number of access rules on a single entity is a signal that the module
# security model is overly granular or that module role decomposition is needed.
# Consider splitting roles or simplifying the access model.

RULE_ID = "MAINT001"
RULE_NAME = "MaxAccessRules"
DESCRIPTION = "Persistent entities should not have more than 8 access rules"
CATEGORY = "maintainability"
SEVERITY = "info"

MAX_ACCESS_RULES = 8
SKIP_MODULES = ("System", "Administration")

def check():
    violations = []
    for e in entities():
        if e.is_external:
            continue
        if e.module_name in SKIP_MODULES:
            continue
        if e.entity_type != "Persistent":
            continue
        if e.access_rule_count > MAX_ACCESS_RULES:
            violations.append(violation(
                message="Entity '{}' has {} access rules (max {}). Consider consolidating module roles.".format(
                    e.qualified_name, e.access_rule_count, MAX_ACCESS_RULES),
                location=location(
                    module=e.module_name,
                    document_type="Entity",
                    document_name=e.qualified_name,
                ),
                suggestion="Consolidate module roles or split this entity to reduce the number of access rules.",
            ))
    return violations
