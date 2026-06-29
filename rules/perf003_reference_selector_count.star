# PERF003: Pages should not have too many reference selector widgets
#
# Heuristic port of ACR performance rule `referenceselectoramount.md`.
#
# Reference selector widgets issue an unbounded database query at page load
# to populate their dropdown list. Multiple reference selectors on the same
# page multiply the load-time queries and can cause noticeable slowdowns.
#
# HEURISTIC: widget_type matching uses a contains check to cover both
# "ReferenceSelector" and "InputReferenceSetSelector" variants.

RULE_ID = "PERF003"
RULE_NAME = "ReferenceSelectorCount"
DESCRIPTION = "Pages should not have more than 3 reference selector widgets"
CATEGORY = "performance"
SEVERITY = "info"

MAX_SELECTORS = 3
SKIP_MODULES = ("System", "Administration")

def check():
    # Count reference selectors per page
    page_counts = {}
    page_modules = {}
    for w in widgets():
        if w.module_name in SKIP_MODULES:
            continue
        if w.container_type != "Page":
            continue
        if not matches(w.widget_type, ".*ReferenceSelector.*"):
            continue
        name = w.container_qualified_name
        page_counts[name] = page_counts.get(name, 0) + 1
        page_modules[name] = w.module_name

    violations = []
    for page_name, count in page_counts.items():
        if count > MAX_SELECTORS:
            violations.append(violation(
                message="Page '{}' has {} reference selector widgets (max {}). Each issues an unbounded query at page load.".format(
                    page_name, count, MAX_SELECTORS),
                location=location(
                    module=page_modules[page_name],
                    document_type="Page",
                    document_name=page_name,
                ),
                suggestion="Replace reference selectors with a search popup or limit the selectable set using a constrained data source.",
            ))
    return violations
