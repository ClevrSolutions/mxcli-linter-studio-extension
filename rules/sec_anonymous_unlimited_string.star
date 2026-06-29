# SEC010: Unlimited String Attributes Editable by Anonymous Users
#
# Port of the ACR rule `Entity_AnonymousUnlimitedString`
# (codereviewer.rulesacr.entityrulesacr.EntityAnonymousUnlimitedString).
#
# An unbounded string attribute (no max length) that an anonymous/guest user
# can WRITE lets an unauthenticated agent store arbitrarily large values,
# exhausting database storage (a denial-of-service vector).
#
# Compliant = either the anonymous role has no write access to the attribute,
# or the string attribute has a bounded length.
#
# Requires FULL catalog (REFRESH CATALOG FULL) for permission/attribute data.

RULE_ID = "SEC010"
RULE_NAME = "AnonymousUnlimitedString"
DESCRIPTION = "Unlimited-length string attributes should not be writable by anonymous users"
CATEGORY = "security"
SEVERITY = "error"

def check():
    sec = project_security()
    if sec == None or not sec.enable_guest_access:
        return []

    # Module roles assigned to anonymous/guest user roles.
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

        # Unbounded string attributes on this entity (length == 0 means unlimited).
        unlimited_strings = {}
        for attr in attributes_for(e.qualified_name):
            if attr.data_type.lower() == "string" and attr.length == 0:
                unlimited_strings[attr.name] = True

        if len(unlimited_strings) == 0:
            continue

        # Anonymous roles with entity-level write (covers all writable members).
        anon_entity_writers = {}
        # Per-attribute write grants to anonymous roles: attr_name -> [role, ...].
        anon_member_writers = {}

        for perm in permissions_for(e.qualified_name):
            if perm.module_role_name not in anon_module_roles:
                continue
            if perm.access_type == "WRITE" and perm.member_name == "":
                anon_entity_writers[perm.module_role_name] = True
            elif perm.access_type == "MEMBER_WRITE":
                short_name = perm.member_name.split(".")[-1]
                if short_name in unlimited_strings:
                    anon_member_writers.setdefault(short_name, []).append(perm.module_role_name)

        for attr_name in sorted(unlimited_strings):
            writer_roles = {}
            for r in anon_entity_writers:
                writer_roles[r] = True
            for r in anon_member_writers.get(attr_name, []):
                writer_roles[r] = True

            if len(writer_roles) == 0:
                continue

            roles = ", ".join(sorted(writer_roles))
            violations.append(violation(
                message="Unlimited-length string attribute '{}.{}' is writable by anonymous user(s) (via role(s): {}) — an unauthenticated agent could store arbitrarily large values and exhaust storage.".format(
                    e.qualified_name, attr_name, roles
                ),
                location=location(
                    module=e.module_name,
                    document_type="Entity",
                    document_name=e.qualified_name,
                ),
                suggestion="Set a maximum length on '{}', or remove anonymous write access to this attribute.".format(attr_name),
            ))

    return violations
