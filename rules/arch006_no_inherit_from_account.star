# ARCH006: Domain entities should not inherit from Administration.Account
#
# Ported from ACR architecture rule `inheritfromaccount.md`.
#
# Inheriting from Administration.Account couples the domain model to the
# user-management module. Use an association to Account instead, keeping
# your domain entities independent of the platform's authentication model.

RULE_ID = "ARCH006"
RULE_NAME = "NoInheritFromAccount"
DESCRIPTION = "Domain entities should not inherit from Administration.Account"
CATEGORY = "architecture"
SEVERITY = "warning"

SKIP_MODULES = ("System", "Administration")
ACCOUNT_QNAME = "Administration.Account"
MAX_WALK = 20

def check():
    by_qname = {}
    for e in entities():
        by_qname[e.qualified_name] = e

    violations = []
    for e in entities():
        if e.is_external:
            continue
        if e.module_name in SKIP_MODULES:
            continue
        if not e.generalization:
            continue

        # Walk the chain and check for Administration.Account as an ancestor
        current_qname = e.generalization
        for _ in range(MAX_WALK):
            if not current_qname:
                break
            if current_qname == ACCOUNT_QNAME:
                violations.append(violation(
                    message="Entity '{}' inherits from Administration.Account. Use an association instead.".format(
                        e.qualified_name),
                    location=location(
                        module=e.module_name,
                        document_type="Entity",
                        document_name=e.qualified_name,
                    ),
                    suggestion="Remove the specialization and add an association from '{}' to Administration.Account.".format(
                        e.name),
                ))
                break
            if current_qname not in by_qname:
                break
            current_qname = by_qname[current_qname].generalization

    return violations
