# MAINT002: Modules should not contain too many microflows
#
# Ported from ACR maintainability rule `flowsamount.md`.
#
# A module with a very large number of microflows becomes hard to navigate
# and understand. Consider splitting the module into smaller, focused modules
# or reorganising microflows into sub-modules.

RULE_ID = "MAINT002"
RULE_NAME = "MaxMicroflowsPerModule"
DESCRIPTION = "Modules should not contain more than 50 microflows"
CATEGORY = "maintainability"
SEVERITY = "info"

MAX_MICROFLOWS = 50
SKIP_MODULES = ("System", "Administration")

def check():
    # Count microflows + nanoflows per module
    counts = {}
    first_mf = {}
    for mf in microflows():
        if mf.module_name in SKIP_MODULES:
            continue
        mod = mf.module_name
        counts[mod] = counts.get(mod, 0) + 1
        if mod not in first_mf:
            first_mf[mod] = mf

    violations = []
    for mod, count in counts.items():
        if count > MAX_MICROFLOWS:
            mf = first_mf[mod]
            violations.append(violation(
                message="Module '{}' contains {} microflows/nanoflows (max {}). Consider splitting into smaller modules.".format(
                    mod, count, MAX_MICROFLOWS),
                location=location(
                    module=mf.module_name,
                    document_type="Microflow",
                    document_name=mf.qualified_name,
                ),
                suggestion="Split '{}' into smaller, focused modules.".format(mod),
            ))
    return violations
