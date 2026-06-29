# SEC015: Anonymous users should only create non-persistent entities
#
# Port of the ACR rule `Entity_AnonymousCreateObject`
# (codereviewer.rulesacr.entityrulesacr.EntityAnonymousCreateObject,
#  docs/rules/security/anonymouscreateobject.md).
#
# If anonymous (guest) users can CREATE persistent objects, a malicious agent
# could create millions of rows and exhaust the database. Note that XPath
# constraints are NOT applied when creating new objects, so a constraint does
# not mitigate this — the create grant itself is the problem.
#
# HEURISTIC: "anonymous" is inferred from user roles flagged is_anonymous and
# the module roles they aggregate (same approach as SEC007). The entity must be
# persistent; non-persistent entities are safe to create.

RULE_ID = "SEC015"
RULE_NAME = "AnonymousCreatePersistent"
DESCRIPTION = "Anonymous users should not have CREATE access on persistent entities (storage-exhaustion DoS)"
CATEGORY = "security"
SEVERITY = "error"

def check():
    sec = project_security()
    if sec == None or not sec.enable_guest_access:
        return []

    anon_module_roles = {}
    for ur in user_roles():
        # ur.is_anonymous is currently always False in mxcli; fall back to role name
        is_anon = ur.is_anonymous or ur.name == "Anonymous"
        if is_anon:
            for mr in ur.module_roles:
                anon_module_roles[mr] = True

    if len(anon_module_roles) == 0:
        return []

    violations = []
    for e in entities():
        if e.entity_type != "Persistent" or e.is_external:
            continue
        for perm in permissions_for(e.qualified_name):
            if perm.access_type.lower() != "create":
                continue
            if perm.module_role_name in anon_module_roles:
                violations.append(violation(
                    message="Entity '{}' grants CREATE to anonymous users (via role '{}'). Guests could create unlimited persistent rows and exhaust the database.".format(
                        e.qualified_name, perm.module_role_name
                    ),
                    location=location(
                        module=e.module_name,
                        document_type="Entity",
                        document_name=e.qualified_name,
                    ),
                    suggestion="Remove CREATE access for the anonymous role, or make the entity non-persistent if guests must create it.",
                ))
    return violations
