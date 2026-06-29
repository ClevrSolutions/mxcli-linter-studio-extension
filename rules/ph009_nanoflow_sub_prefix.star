# PH009: Sub-microflows called from a nanoflow should be prefixed with NSub_
#
# Ported (as a heuristic) from ACR project-hygiene rule
# `MicroflowProjectHygieneSubInNanoflow`
# (docs/rules/project-hygiene/microflowprojecthygienesubinnanoflow.md).
#
# A sub-microflow invoked from a nanoflow gets the distinct prefix NSub_ (rather than
# SUB_) so it is clear the helper runs in a nanoflow context.
#
# HEURISTIC: we detect this by checking whether any caller of a microflow is a nanoflow,
# using the reference's source_type. (microflows() does not enumerate nanoflows, so the
# caller's type must come from the reference itself.)

RULE_ID = "PH009"
RULE_NAME = "NanoflowSubPrefix"
DESCRIPTION = "Microflows called from a nanoflow should start with NSub_"
CATEGORY = "naming"
SEVERITY = "info"

SKIP_MODULES = ("System", "Administration")

def check():
    violations = []

    for mf in microflows():
        if mf.module_name in SKIP_MODULES:
            continue
        if mf.microflow_type.lower() != "microflow":
            continue
        if mf.name.lower().startswith("nsub_"):
            continue

        called_from_nanoflow = False
        for ref in refs_to(mf.qualified_name):
            if "nano" in ref.source_type.lower():
                called_from_nanoflow = True
                break

        if called_from_nanoflow:
            violations.append(violation(
                message="Microflow '{}' is called from a nanoflow but lacks an NSub_ prefix.".format(
                    mf.name),
                location=location(
                    module=mf.module_name,
                    document_type="Microflow",
                    document_name=mf.qualified_name,
                ),
                suggestion="Rename to 'NSub_{}'.".format(mf.name),
            ))
    return violations
