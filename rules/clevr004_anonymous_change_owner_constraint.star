# CLEVR004: If an anonymous user is allowed to change objects, constrain
#            these objects to the owner
#
# Mirrors the CLEVR ACR rule "If an anonymous user is allowed to change objects,
# constrain these objects to the owner" (Security, ACR 1.10 / May 2020).
#
# Guest users may read global data - that is fine and explicitly out of scope.
# But if a guest can MODIFY or DELETE an entity, the access rule must be
# constrained to the records that guest created, otherwise one anonymous visitor
# can edit or destroy every other visitor's records.
#
# Scope decisions (deliberate):
#   - CREATE is excluded. An XPath constraint cannot govern creation - the
#     object does not exist yet - and create grants are covered by the sibling
#     ACR rule "Anonymous users should only be allowed to create non-persistent
#     entities".
#   - MEMBER_WRITE counts as modification, but findings are reported once per
#     entity rather than once per attribute.
#   - Non-persistent and external entities are skipped: no owner, no database.
#
# "Constrained to the owner" is read strictly: the XPath must pin the row to the
# current user. Two idioms are accepted - System.owner and id - see
# OWNER_PATTERNS below. A custom ownership association such as
# [MyModule.Submission_Account = '[%CurrentUser%]'] is still flagged; add its
# shape to OWNER_PATTERNS if your app models ownership that way.
#
# Install: copy to .claude/lint-rules/clevr004_anonymous_change_owner_constraint.star
# Verify:  mxcli lint -p app.mpr --list-rules

RULE_ID = "CLEVR004"
RULE_NAME = "AnonymousChangeWithoutOwnerConstraint"
DESCRIPTION = "Entities an anonymous user can modify or delete must be constrained to the owner"
CATEGORY = "security"
SEVERITY = "error"

# Access types that count as "changing" an object.
CHANGE_ACCESS = ["WRITE", "DELETE", "MEMBER_WRITE"]

# A constraint counts as owner-scoped if it contains ANY of these, compared
# after lowercasing and stripping whitespace and quotes.
#
#   System.owner = '[%CurrentUser%]'   the generic Mendix ownership idiom
#   id           = '[%CurrentUser%]'   the idiom for System.User specialisations
#                                      such as Administration.Account, where the
#                                      object IS the user - see the stock
#                                      Administration.User rule in any app
OWNER_PATTERNS = [
    "system.owner=[%currentuser%]",
    "id=[%currentuser%]",
]


def _role_qname(perm):
    """Module role name as a qualified 'Module.Role' string.

    user_roles().module_roles yields QUALIFIED names ("Administration.User"),
    but permissions_for().module_role_name can come back UNQUALIFIED ("User").
    Comparing the two directly silently matches nothing and the rule reports a
    clean pass on a genuinely broken app. An access rule always belongs to the
    module that owns the entity, so perm.module_name is the correct qualifier -
    no cross-module collisions.
    """
    name = perm.module_role_name
    if name == None or name == "":
        return ""
    if "." in name:
        return name
    return perm.module_name + "." + name


def _norm(xpath):
    """Lowercase and strip whitespace/quotes so spacing variants compare equal."""
    out = xpath.lower()
    for junk in [" ", "\t", "\n", "\r", "'", "\""]:
        out = out.replace(junk, "")
    return out


def _is_owner_constrained(perm):
    xpath = perm.xpath_constraint
    if xpath == None or xpath == "":
        return False

    normalised = _norm(xpath)
    for pattern in OWNER_PATTERNS:
        if pattern in normalised:
            return True
    return False


def check():
    sec = project_security()
    if sec == None or not sec.enable_guest_access:
        return []

    # Module roles reachable by an anonymous user role.
    anon_module_roles = {}
    for ur in user_roles():
        if ur.is_anonymous:
            for mr in ur.module_roles:
                anon_module_roles[mr] = True

    if len(anon_module_roles) == 0:
        return []

    violations = []

    for e in entities():
        if e.entity_type.lower() != "persistent" or e.is_external:
            continue

        offending_types = {}
        offending_roles = {}

        for perm in permissions_for(e.qualified_name):
            if _role_qname(perm) not in anon_module_roles:
                continue
            if perm.access_type.upper() not in CHANGE_ACCESS:
                continue
            if _is_owner_constrained(perm):
                continue

            offending_types[perm.access_type.upper()] = True
            offending_roles[perm.module_role_name] = True

        if len(offending_types) == 0:
            continue

        violations.append(violation(
            message="Anonymous users can {} objects of '{}' (via role '{}') without an owner constraint - one guest can change or remove records created by another.".format(
                "/".join(sorted(offending_types.keys())),
                e.qualified_name,
                "', '".join(sorted(offending_roles.keys())),
            ),
            location=location(
                module=e.module_name,
                document_type="Entity",
                document_name=e.qualified_name,
            ),
            suggestion="Add the XPath constraint [System.owner = '[%CurrentUser%]'] to the anonymous access rule on '{}', or remove the write/delete grant if guests should not change this data.".format(
                e.qualified_name,
            ),
        ))

    return violations
