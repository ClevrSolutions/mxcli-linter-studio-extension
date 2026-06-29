# SEC018: Sub-microflows should not be directly callable from a page
#
# Port of the ACR rule `MicroflowShouldNotHavePermissions`
# (codereviewer.rulesacr.flowrulesacr.meta.MicroflowShouldNotHavePermissions,
#  docs/rules/security/microflowunneccesarypermissions.md).
#
# A microflow used only as a sub-microflow should NOT be directly callable
# from the UI. Such flows follow the SUB_ naming convention. If it needs a
# UI entry point, wrap it in a permission-bearing ACT_ or Nsub_ microflow.
# Note the Nsub_ wrapper convention is explicitly exempt.
#
# This is the security-side complement of PH010, which flags ACT_ flows that
# have NO page caller. SEC018 flags SUB_ flows that HAVE a page caller.
#
# HEURISTIC: page/snippet/navigation cross-references are used as a proxy for
# "has a UI entry point". Microflow execute permissions are not persistently
# stored in the MPR catalog, so page refs are the practical equivalent.

RULE_ID = "SEC018"
RULE_NAME = "SubMicroflowHasPermission"
DESCRIPTION = "Sub-microflows (SUB_) should not be directly callable from a page"
CATEGORY = "security"
SEVERITY = "warning"

SKIP_MODULES = ("System", "Administration")
SUB_PREFIX = "sub_"
UI_SOURCES = ("page", "snippet", "widget", "navigation")

def check():
    violations = []
    for mf in microflows():
        if mf.module_name in SKIP_MODULES:
            continue
        if mf.microflow_type.lower() != "microflow":
            continue
        if not mf.name.lower().startswith(SUB_PREFIX):
            continue

        has_ui_caller = False
        for ref in refs_to(mf.qualified_name):
            if ref.source_type.lower() in UI_SOURCES:
                has_ui_caller = True
                break

        if has_ui_caller:
            violations.append(violation(
                message="Sub-microflow '{}' is directly callable from a page — sub-microflows should not have UI entry points.".format(
                    mf.name
                ),
                location=location(
                    module=mf.module_name,
                    document_type="Microflow",
                    document_name=mf.qualified_name,
                ),
                suggestion="Remove the page action that calls this flow. If it needs a UI entry point, wrap it in an ACT_ or Nsub_ microflow.",
            ))
    return violations
