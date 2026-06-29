# MAINT005: Microflows should not have too many parameters
#
# Ported from ACR maintainability rule `parameteramount.md`.
#
# A microflow with many parameters is hard to call correctly and difficult
# to understand. Introduce a helper entity to group related parameters,
# or split the microflow into smaller pieces.

RULE_ID = "MAINT005"
RULE_NAME = "MaxMicroflowParameters"
DESCRIPTION = "Microflows should not have more than 5 parameters"
CATEGORY = "maintainability"
SEVERITY = "info"

MAX_PARAMETERS = 5
SKIP_MODULES = ("System", "Administration")

def check():
    violations = []
    for mf in microflows():
        if mf.module_name in SKIP_MODULES:
            continue
        if mf.parameter_count > MAX_PARAMETERS:
            violations.append(violation(
                message="Microflow '{}' has {} parameters (max {}). Introduce a helper entity to group parameters.".format(
                    mf.qualified_name, mf.parameter_count, MAX_PARAMETERS),
                location=location(
                    module=mf.module_name,
                    document_type="Microflow",
                    document_name=mf.qualified_name,
                ),
                suggestion="Group related parameters into a non-persistent entity and pass it as a single parameter.",
            ))
    return violations
