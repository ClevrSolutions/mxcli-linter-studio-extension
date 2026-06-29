# PH001: Attribute names should not start with their entity name
#
# Ported from ACR project-hygiene rule `Attributes_may_not_start_with_their_entity_names`
# (docs/rules/project-hygiene/attributenameentity.md).
#
# An attribute is already scoped to its entity, so repeating the entity name as a
# prefix (e.g. Customer.CustomerName) is redundant and hurts readability.

RULE_ID = "PH001"
RULE_NAME = "AttributeNotEntityPrefixed"
DESCRIPTION = "Attribute names should not start with their entity name"
CATEGORY = "naming"
SEVERITY = "info"

SKIP_MODULES = ("System", "Administration")

def check():
    violations = []
    for entity in entities():
        if entity.module_name in SKIP_MODULES:
            continue
        if entity.is_external:
            continue
        ename = entity.name.lower()
        if len(ename) < 3:
            # Very short entity names produce too many false positives.
            continue
        for attr in attributes_for(entity.qualified_name):
            aname = attr.name.lower()
            # Flag only when the entity name is a prefix AND something follows it,
            # so an attribute named exactly like the entity is reported too.
            if aname.startswith(ename) and len(attr.name) >= len(entity.name):
                violations.append(violation(
                    message="Attribute '{}.{}' starts with its entity name; the prefix is redundant.".format(
                        entity.name, attr.name),
                    location=location(
                        module=entity.module_name,
                        document_type="Entity",
                        document_name=entity.qualified_name,
                    ),
                    suggestion="Rename to '{}' (drop the '{}' prefix).".format(
                        attr.name[len(entity.name):] or attr.name, entity.name),
                ))
    return violations
