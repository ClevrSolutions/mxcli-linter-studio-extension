# PH005: Folders should not contain too many documents
#
# Ported from ACR project-hygiene rule `Folder_with_too_many_items`
# (docs/rules/project-hygiene/largefolder.md).
#
# A folder crammed with documents is hard to navigate. Split it into sub-folders.
# NOTE: the Starlark API exposes the folder path of each document but not folders
# themselves, so this counts documents per (module, folder). Sub-folders are counted
# under their own path, not their parent.

RULE_ID = "PH005"
RULE_NAME = "LargeFolder"
DESCRIPTION = "A folder should not contain an excessive number of documents"
CATEGORY = "quality"
SEVERITY = "info"

# ACR default for this rule is configurable; 25 documents per folder is a sane cap.
MAX_ITEMS = 25
SKIP_MODULES = ("System", "Administration")

def _tally(elements, counts):
    for el in elements:
        if el.module_name in SKIP_MODULES:
            continue
        key = el.module_name + "|" + el.folder
        counts[key] = counts.get(key, 0) + 1

def check():
    counts = {}
    _tally(microflows(), counts)
    _tally(pages(), counts)
    _tally(entities(), counts)
    _tally(snippets(), counts)
    _tally(enumerations(), counts)

    violations = []
    for key in counts:
        n = counts[key]
        if n <= MAX_ITEMS:
            continue
        parts = key.split("|", 1)
        module = parts[0]
        folder = parts[1] if len(parts) > 1 else ""
        folder_label = folder if folder != "" else "(module root)"
        violations.append(violation(
            message="Folder '{}' in module '{}' contains {} documents (max {}).".format(
                folder_label, module, n, MAX_ITEMS),
            location=location(
                module=module,
                document_type="folder",
                document_name=module + "/" + folder,
            ),
            suggestion="Split the documents into sub-folders by responsibility.",
        ))
    return violations
