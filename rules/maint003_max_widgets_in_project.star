# MAINT003: Project should not contain too many widgets
#
# Ported from ACR maintainability rule `maintainabilitywidgetamount.md`.
#
# A very high total widget count is a proxy for excessive UI complexity.
# It often indicates that pages have grown too large or that UI reuse
# (via snippets) is insufficient.

RULE_ID = "MAINT003"
RULE_NAME = "MaxWidgetsInProject"
DESCRIPTION = "Project should not contain more than 500 widgets in total"
CATEGORY = "maintainability"
SEVERITY = "info"

MAX_WIDGETS = 500
SKIP_MODULES = ("System", "Administration")

def check():
    count = 0
    for w in widgets():
        if w.module_name in SKIP_MODULES:
            continue
        count += 1

    if count <= MAX_WIDGETS:
        return []

    return [violation(
        message="Project contains {} widgets (max {}). High widget counts indicate excessive UI complexity.".format(
            count, MAX_WIDGETS),
        location=location(module="", document_type="project", document_name="Project"),
        suggestion="Reduce page complexity using snippets, remove unused widgets, or split large pages.",
    )]
