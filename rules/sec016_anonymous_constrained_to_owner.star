# SEC016: Anonymous changes should be constrained to the owner
#
# Port of the ACR rule `SecurityAnonymousUserCreateConstrainedObjects`
# (codereviewer.rulesacr.entityrulesacr.SecurityAnonymousUserCreateConstrainedObjects,
#  docs/rules/security/securityanonymoususercreateconstrainedobjects.md).
#
# A guest user should only see/change what they created, not other users' data.
# If an anonymous role can WRITE (modify/delete) an entity, that access should be
# row-scoped with an XPath constraint to the owner; otherwise one guest can alter
# every other guest's records.
#
# HEURISTIC: "anonymous" is inferred from is_anonymous user roles and their module
# roles (same as SEC007/SEC015). We flag a WRITE grant to an anonymous role that
# has no XPath constraint. We cannot verify the constraint actually references the
# owner, only that some row-level constraint exists.

RULE_ID = "SEC016"
RULE_NAME = "AnonymousWriteUnconstrained"
DESCRIPTION = "Anonymous WRITE access should be constrained (to the owner) so guests cannot change others' data"
CATEGORY = "security"
SEVERITY = "warning"

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
            if perm.access_type.lower() != "write":
                continue
            if perm.module_role_name not in anon_module_roles:
                continue
            if perm.is_constrained:
                continue
            violations.append(violation(
                message="Entity '{}' grants WRITE to anonymous users (via role '{}') with no XPath constraint — a guest can modify records created by other users.".format(
                    e.qualified_name, perm.module_role_name
                ),
                location=location(
                    module=e.module_name,
                    document_type="Entity",
                    document_name=e.qualified_name,
                ),
                suggestion="Add an XPath constraint limiting the anonymous role to records it owns (e.g. constrain on the owner association), or remove the WRITE grant.",
            ))
    return violations
