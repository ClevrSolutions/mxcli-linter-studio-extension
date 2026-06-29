# PH002: Entity names should be unique across the whole project
#
# Ported from ACR project-hygiene rule `Duplicate_entity_names`
# (docs/rules/project-hygiene/duplicateentityname.md).
#
# Two entities with the same simple name in different modules make it hard to know
# which one a reference points at. Names should be unique project-wide.

RULE_ID = "PH002"
RULE_NAME = "DuplicateEntityName"
DESCRIPTION = "Entity names should be unique across the entire project"
CATEGORY = "design"
SEVERITY = "warning"

SKIP_MODULES = ("System", "Administration")

def check():
    violations = []

    # Group qualified names by lowercased simple name.
    groups = {}
    for entity in entities():
        if entity.module_name in SKIP_MODULES:
            continue
        if entity.is_external:
            continue
        key = entity.name.lower()
        if key not in groups:
            groups[key] = []
        groups[key].append(entity)

    for key in groups:
        members = groups[key]
        if len(members) < 2:
            continue
        qnames = [m.qualified_name for m in members]
        for entity in members:
            others = [q for q in qnames if q != entity.qualified_name]
            violations.append(violation(
                message="Entity name '{}' is not unique; also defined as: {}.".format(
                    entity.name, ", ".join(others)),
                location=location(
                    module=entity.module_name,
                    document_type="Entity",
                    document_name=entity.qualified_name,
                ),
                suggestion="Rename one of the entities so each name is unique project-wide.",
            ))
    return violations
