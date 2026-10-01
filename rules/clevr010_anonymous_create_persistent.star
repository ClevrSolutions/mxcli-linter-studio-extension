# CLEVR010: Anonymous users should only be allowed to create non-persistent
#           entities
#
# Mirrors the CLEVR ACR rule of the same name (Security, Blocker).
#
# ACR's reasoning, verbatim: "Otherwise, a malicious agent could create
# millions of objects causing the database to run out of space."
#
# A non-persistent entity lives only in the session, so creating them costs
# nothing durable. A persistent one is a row, and an unauthenticated visitor
# who can create rows can fill the disk.
#
# Note this is the CREATE counterpart to CLEVR004, which covers write/delete.
# An XPath constraint cannot govern creation, so constraining the entity does
# not help here - the grant itself is the problem.
#
# Install: copy to .claude/lint-rules/clevr010_anonymous_create_persistent.star

RULE_ID = "CLEVR010"
RULE_NAME = "AnonymousCreatesPersistentEntity"
DESCRIPTION = "Anonymous users must not be able to create persistent entities"
CATEGORY = "security"
SEVERITY = "error"


def check():
    sec = project_security()
    if sec == None or not sec.enable_guest_access:
        return []

    # Module roles reachable by an anonymous user role.
    anonRoles = {}
    for ur in user_roles():
        if ur.is_anonymous:
            for mr in ur.module_roles:
                anonRoles[mr] = True

    if len(anonRoles) == 0:
        return []

    # Entity type by qualified name, so a CREATE row can be judged.
    entityType = {}
    external = {}
    for e in entities():
        entityType[e.qualified_name] = e.entity_type
        external[e.qualified_name] = e.is_external

    violations = []
    seen = {}

    for p in permissions():
        if p.element_type != "ENTITY":
            continue
        if p.access_type.upper() != "CREATE":
            continue
        if p.module_role_name not in anonRoles:
            continue

        et = entityType.get(p.element_name)
        if et == None:
            # Not returned by entities() - a platform or marketplace module.
            continue
        if et.lower() != "persistent":
            continue
        if external.get(p.element_name):
            continue

        key = p.element_name + "|" + p.module_role_name
        if key in seen:
            continue
        seen[key] = True

        violations.append(violation(
            message = "Anonymous users can create '{}' (via role '{}'), a persistent entity - an unauthenticated visitor can write unlimited rows to the database.".format(
                p.element_name, p.module_role_name,
            ),
            location = location(
                module = p.module_name,
                document_type = "Entity",
                document_name = p.element_name,
            ),
            suggestion = "Remove the create grant for '{}', or make the entity non-persistent if it only needs to exist for the session.".format(
                p.module_role_name,
            ),
        ))

    return violations
