# PH011: Microflows should not call themselves directly
#
# Ported from ACR architecture rule `microflowcallsself.md`.
#
# A microflow that calls itself creates an unconditional recursive loop.
# Mendix microflows do not support recursion — a self-call will cause a
# stack overflow at runtime. Use a loop activity instead.

RULE_ID = "PH011"
RULE_NAME = "SelfCallMicroflow"
DESCRIPTION = "Microflows must not call themselves (causes runtime stack overflow)"
CATEGORY = "project-hygiene"
SEVERITY = "warning"

SKIP_MODULES = ("System", "Administration")

def check():
    violations = []
    for mf in microflows():
        if mf.module_name in SKIP_MODULES:
            continue
        for ref in refs_to(mf.qualified_name):
            if ref.source_name == mf.qualified_name:
                violations.append(violation(
                    message="Microflow '{}' calls itself directly and will cause a stack overflow at runtime.".format(
                        mf.qualified_name),
                    location=location(
                        module=mf.module_name,
                        document_type="Microflow",
                        document_name=mf.qualified_name,
                    ),
                    suggestion="Replace the self-call with a loop activity to iterate over a list.",
                ))
                break  # one violation per microflow is enough
    return violations
