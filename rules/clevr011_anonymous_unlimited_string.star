# CLEVR011: Unlimited string attributes should not be editable by anonymous
#           users
#
# Mirrors the CLEVR ACR rule of the same name (Security, Blocker).
#
# ACR's reasoning, verbatim: "Otherwise, a malicious agent could set a very
# long value for the attribute causing the database to run out of space."
#
# In Mendix a String attribute with length 0 means unlimited. Combined with
# write access for an unauthenticated visitor, a single request can store an
# arbitrarily large value.
#
# Install: copy to .claude/lint-rules/clevr011_anonymous_unlimited_string.star

RULE_ID = "CLEVR011"
RULE_NAME = "AnonymousWritesUnlimitedString"
DESCRIPTION = "Anonymous users must not have write access to unlimited-length string attributes"
CATEGORY = "security"
SEVERITY = "error"


def _lastSegment(qualified):
    idx = qualified.rfind(".")
    if idx < 0:
        return qualified
    return qualified[idx + 1:]


def check():
    sec = project_security()
    if sec == None or not sec.enable_guest_access:
        return []

    anonRoles = {}
    for ur in user_roles():
        if ur.is_anonymous:
            for mr in ur.module_roles:
                anonRoles[mr] = True

    if len(anonRoles) == 0:
        return []

    violations = []
    seen = {}

    for p in permissions():
        if p.element_type != "ENTITY":
            continue
        if p.access_type.upper() != "MEMBER_WRITE":
            continue
        if p.module_role_name not in anonRoles:
            continue
        if p.member_name == "":
            continue

        # permission.member_name is qualified (Module.Entity.Attribute), while
        # attribute.name is the bare name.
        attrName = _lastSegment(p.member_name)

        for attr in attributes_for(p.element_name):
            if attr.name != attrName:
                continue
            if attr.data_type.lower() != "string":
                break
            # 0 is Mendix's "unlimited".
            if attr.length != 0:
                break

            key = p.element_name + "|" + attrName + "|" + p.module_role_name
            if key in seen:
                break
            seen[key] = True

            violations.append(violation(
                message = "Anonymous users can write '{}.{}' (via role '{}'), an unlimited-length string - a single request can store an arbitrarily large value.".format(
                    p.element_name, attrName, p.module_role_name,
                ),
                location = location(
                    module = p.module_name,
                    document_type = "Entity",
                    document_name = p.element_name,
                ),
                suggestion = "Give '{}' an explicit maximum length, or remove write access for '{}'.".format(
                    attrName, p.module_role_name,
                ),
            ))
            break

    return violations
