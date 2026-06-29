# ARCH007: Domain entities should not inherit from arbitrary System entities
#
# Ported from ACR architecture rule `inheritfromsystem.md`.
#
# Inheriting from System module entities (other than the explicitly supported
# System.FileDocument and System.Image) creates a hidden dependency on
# Mendix platform internals that may break across upgrades.
#
# System.FileDocument and System.Image are intentional extension points
# for file/image handling and are therefore exempt from this rule.

RULE_ID = "ARCH007"
RULE_NAME = "NoInheritFromSystem"
DESCRIPTION = "Domain entities should not inherit from System entities (except FileDocument/Image)"
CATEGORY = "architecture"
SEVERITY = "warning"

SKIP_MODULES = ("System", "Administration")
ALLOWED_SYSTEM_BASES = ("System.FileDocument", "System.Image")
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

        current_qname = e.generalization
        for _ in range(MAX_WALK):
            if not current_qname:
                break
            if current_qname not in by_qname:
                # Unknown entity — check if it looks like a System entity by name
                if current_qname.startswith("System.") and current_qname not in ALLOWED_SYSTEM_BASES:
                    violations.append(violation(
                        message="Entity '{}' inherits from System entity '{}' which is not a supported extension point.".format(
                            e.qualified_name, current_qname),
                        location=location(
                            module=e.module_name,
                            document_type="Entity",
                            document_name=e.qualified_name,
                        ),
                        suggestion="Only System.FileDocument and System.Image are supported base entities. Remove the specialization from '{}'.".format(
                            e.name),
                    ))
                break
            ancestor = by_qname[current_qname]
            if ancestor.module_name == "System" and current_qname not in ALLOWED_SYSTEM_BASES:
                violations.append(violation(
                    message="Entity '{}' inherits from System entity '{}' which is not a supported extension point.".format(
                        e.qualified_name, current_qname),
                    location=location(
                        module=e.module_name,
                        document_type="Entity",
                        document_name=e.qualified_name,
                    ),
                    suggestion="Only System.FileDocument and System.Image are supported base entities. Remove the specialization from '{}'.".format(
                        e.name),
                ))
                break
            current_qname = ancestor.generalization

    return violations
