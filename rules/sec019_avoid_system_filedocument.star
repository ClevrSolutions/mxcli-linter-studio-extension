# SEC019: Don't create records directly on System.FileDocument / System.Image
#
# Port of the ACR rule `SecurityAvoidSystemFileDocument`
# (codereviewer.rulesacr.flowrulesacr.action.SecurityAvoidSystemFileDocument,
#  docs/rules/security/securityavoidsystemfiledocument.md).
#
# You cannot change the security of the System module, and these base entities
# have had security issues. Instead of creating records directly against
# System.FileDocument or System.Image, inherit them into your own entity and set
# proper access rules. This rule flags create/change activities that target the
# system base entities directly.
#
# HEURISTIC + NOTE: requires FULL catalog (REFRESH CATALOG FULL) so that
# activities_for() returns activities with entity_ref. Without FULL catalog the
# rule yields nothing rather than false positives.

RULE_ID = "SEC019"
RULE_NAME = "AvoidSystemFileDocument"
DESCRIPTION = "Create/change activities should target an inherited entity, not System.FileDocument/System.Image directly"
CATEGORY = "security"
SEVERITY = "warning"

SYSTEM_BASE_ENTITIES = ("System.FileDocument", "System.Image")
CREATE_CHANGE_ACTIONS = ("createchangeaction", "createobjectaction", "changeobjectaction")

def _targets_system_base(entity_ref):
    if entity_ref == None:
        return False
    for base in SYSTEM_BASE_ENTITIES:
        if entity_ref == base or entity_ref.endswith(base):
            return True
    return False

def check():
    violations = []
    for mf in microflows():
        for act in activities_for(mf.qualified_name):
            if act.action_type == None:
                continue
            if act.action_type.lower() not in CREATE_CHANGE_ACTIONS:
                continue
            if not _targets_system_base(act.entity_ref):
                continue
            violations.append(violation(
                message="Microflow '{}' creates/changes '{}' directly. System base entities cannot be secured.".format(
                    mf.qualified_name, act.entity_ref
                ),
                location=location(
                    module=mf.module_name,
                    document_type="Microflow",
                    document_name=mf.qualified_name,
                ),
                suggestion="Create an entity that inherits from {} with its own access rules, and create/change that instead.".format(act.entity_ref),
            ))
    return violations
