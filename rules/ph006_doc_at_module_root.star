# PH006: Only folders should be direct children of a module root
#
# Ported from ACR project-hygiene rule
# `Items_other_than_folders_as_childs_to_the_module_root`
# (docs/rules/project-hygiene/documentdirectchild.md).
#
# Documents placed directly under the module root (no folder) clutter the module.
# Every document should live inside a folder.

RULE_ID = "PH006"
RULE_NAME = "DocumentAtModuleRoot"
DESCRIPTION = "Documents should live in folders, not directly under the module root"
CATEGORY = "quality"
SEVERITY = "info"

SKIP_MODULES = ("System", "Administration")

def _check_elements(elements, doc_type, violations):
    for el in elements:
        if el.module_name in SKIP_MODULES:
            continue
        if el.folder and el.folder.strip() != "":
            continue
        violations.append(violation(
            message="{} '{}' sits directly under the module root; move it into a folder.".format(
                doc_type, el.name),
            location=location(
                module=el.module_name,
                document_type=doc_type,
                document_name=el.qualified_name,
            ),
            suggestion="Place this document inside a folder within the module.",
        ))

def check():
    violations = []
    _check_elements(microflows(), "Microflow", violations)
    _check_elements(pages(), "Page", violations)
    _check_elements(snippets(), "Snippet", violations)
    _check_elements(enumerations(), "Enumeration", violations)
    return violations
