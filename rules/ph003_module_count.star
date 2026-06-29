# PH003: Project should not contain too many modules
#
# Ported from ACR project-hygiene rule `Large_project`
# (docs/rules/project-hygiene/moduleamount.md).
#
# A large number of modules reduces understandability of the overall application.
# NOTE: the Starlark API cannot distinguish app modules from imported marketplace/
# system modules, so this counts all modules that contain entities/microflows/pages
# except the platform System/Administration modules. Tune MAX_MODULES to taste.

RULE_ID = "PH003"
RULE_NAME = "ModuleCount"
DESCRIPTION = "Project should not contain an excessive number of modules"
CATEGORY = "design"
SEVERITY = "info"

# ACR default for this rule is configurable; 20 is a reasonable starting point.
MAX_MODULES = 20
SKIP_MODULES = ("System", "Administration")

def check():
    modules = {}
    for e in entities():
        modules[e.module_name] = True
    for m in microflows():
        modules[m.module_name] = True
    for p in pages():
        modules[p.module_name] = True

    count = 0
    for name in modules:
        if name in SKIP_MODULES:
            continue
        count += 1

    if count <= MAX_MODULES:
        return []

    return [violation(
        message="Project has {} modules (threshold {}). Too many modules reduce understandability.".format(
            count, MAX_MODULES),
        location=location(module="", document_type="project", document_name="Project"),
        suggestion="Consolidate related modules or split the app into separate projects.",
    )]
