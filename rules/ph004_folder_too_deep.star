# PH004: Folder structure should not be nested too deeply
#
# Ported from ACR project-hygiene rule `Folder_structure_too_deep`
# (docs/rules/project-hygiene/foldertoodeep.md).
#
# Deeply nested folders make documents hard to find and the module hard to maintain.

RULE_ID = "PH004"
RULE_NAME = "FolderTooDeep"
DESCRIPTION = "Folder nesting depth should not exceed the configured maximum"
CATEGORY = "quality"
SEVERITY = "info"

# ACR default for this rule is configurable; 5 levels is a common ceiling.
MAX_DEPTH = 5
SKIP_MODULES = ("System", "Administration")

def _depth(folder):
    if not folder:
        return 0
    d = 0
    for seg in folder.replace("\\", "/").split("/"):
        if seg.strip() != "":
            d += 1
    return d

def _check_elements(elements, doc_type, violations, seen):
    for el in elements:
        if el.module_name in SKIP_MODULES:
            continue
        depth = _depth(el.folder)
        if depth <= MAX_DEPTH:
            continue
        # Report each distinct folder once per module to avoid one violation per document.
        key = el.module_name + "|" + el.folder
        if key in seen:
            continue
        seen[key] = True
        violations.append(violation(
            message="Folder '{}' in module '{}' is nested {} levels deep (max {}).".format(
                el.folder, el.module_name, depth, MAX_DEPTH),
            location=location(
                module=el.module_name,
                document_type=doc_type,
                document_name=el.qualified_name,
            ),
            suggestion="Flatten the folder structure to at most {} levels.".format(MAX_DEPTH),
        ))

def check():
    violations = []
    seen = {}
    _check_elements(microflows(), "Microflow", violations, seen)
    _check_elements(pages(), "Page", violations, seen)
    _check_elements(snippets(), "Snippet", violations, seen)
    _check_elements(enumerations(), "Enumeration", violations, seen)
    return violations
