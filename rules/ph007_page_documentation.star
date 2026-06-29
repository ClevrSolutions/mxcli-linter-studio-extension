# PH007: Complex pages should have documentation
#
# Ported from ACR project-hygiene rule `No_page_documentation`
# (docs/rules/project-hygiene/pagedocumentation.md).
#
# Pages with many widgets are hard to understand at a glance and should carry a
# description explaining their purpose (or be broken up using snippets).

RULE_ID = "PH007"
RULE_NAME = "PageDocumentation"
DESCRIPTION = "Pages with many widgets should have documentation"
CATEGORY = "quality"
SEVERITY = "info"

# Only require docs once a page is sufficiently complex.
MIN_WIDGETS = 30
SKIP_MODULES = ("System", "Administration")

def check():
    violations = []
    for page in pages():
        if page.module_name in SKIP_MODULES:
            continue
        if page.widget_count <= MIN_WIDGETS:
            continue
        if page.description and page.description.strip() != "":
            continue
        violations.append(violation(
            message="Page '{}' has {} widgets but no documentation.".format(
                page.name, page.widget_count),
            location=location(
                module=page.module_name,
                document_type="Page",
                document_name=page.qualified_name,
            ),
            suggestion="Add a description, or split the page using snippets.",
        ))
    return violations
