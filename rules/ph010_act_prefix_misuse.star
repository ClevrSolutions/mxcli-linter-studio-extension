# PH010: Flows without a page caller should not use the ACT_ prefix
#
# Ported (as a heuristic) from ACR project-hygiene rule `FlowsWithoutPermissionPrefixes`
# (docs/rules/project-hygiene/microflowunneccesarypermissions.md).
#
# The ACT_ prefix marks a microflow triggered from a page action, which should be
# access-controlled and directly callable from the UI. A flow with no page/UI caller
# is really a sub / scheduled / lifecycle flow and should not carry ACT_.
#
# HEURISTIC: we use page/snippet/navigation cross-references as the proxy for
# "has a UI entry point". Microflow execute permissions are not persistently
# stored in the MPR catalog, so page refs are the practical equivalent.

RULE_ID = "PH010"
RULE_NAME = "ActPrefixMisuse"
DESCRIPTION = "ACT_ microflows should be triggered from a page action; otherwise rename them"
CATEGORY = "naming"
SEVERITY = "info"

SKIP_MODULES = ("System", "Administration")
UI_SOURCES = ("page", "snippet", "widget", "navigation")

def check():
    violations = []
    for mf in microflows():
        if mf.module_name in SKIP_MODULES:
            continue
        if not mf.name.lower().startswith("act_"):
            continue

        has_ui_caller = False
        for ref in refs_to(mf.qualified_name):
            if ref.source_type.lower() in UI_SOURCES:
                has_ui_caller = True
                break

        if not has_ui_caller:
            violations.append(violation(
                message="Microflow '{}' uses the ACT_ prefix but is not triggered from a page.".format(
                    mf.name),
                location=location(
                    module=mf.module_name,
                    document_type="Microflow",
                    document_name=mf.qualified_name,
                ),
                suggestion="Add a page action that calls this microflow, or rename it (e.g. SUB_) if it is not a page action.",
            ))
    return violations
