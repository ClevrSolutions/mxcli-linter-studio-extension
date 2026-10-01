# CLEVR009: Entity access default rights for new members should be None or Read
#
# Mirrors the CLEVR ACR rule of the same name (Security, Blocker).
#
# Every entity access rule has a "Default rights for new members" setting -
# None / Read / Read-Write. It decides what access a newly added attribute or
# association inherits automatically. Left on Read-Write, adding an attribute
# silently grants write access to that role, with nobody deciding so.
#
# It is about the NEXT attribute someone adds, not the ones already there, so
# it cannot be inferred from the member rows - the setting is the only source
# of truth.
#
# Your Dev Suite configuration:
#   Allow default [Read-Only]        = Yes   -> only ReadWrite is a violation
#   Excluded project roles           = .Debug
#
# Requires mxcli with permission.default_member_access_rights
# (mendixlabs/mxcli#1179, merged 25 Sep 2026).
#
# On an older mxcli the field does not exist and every row would raise
# "permission struct has no .default_member_access_rights attribute". That
# happens in practice: an IDE extension can bundle its own mxcli, older than
# the one you build. The guard below makes the rule stand down quietly on such
# a binary rather than reporting an error against every access rule.
#
# The trade-off is deliberate but worth knowing: on an old binary this rule
# reports NOTHING, which looks identical to a clean project. Trust it only
# from an mxcli you know is current.
#
# Install: copy to .claude/lint-rules/clevr009_default_member_rights.star

RULE_ID = "CLEVR009"
RULE_NAME = "UnsafeDefaultMemberRights"
DESCRIPTION = "Entity access rules should default new members to None or Read, never Read-Write"
CATEGORY = "security"
SEVERITY = "error"

# Your ACR setting "Allow default [Read-Only] = Yes": ReadOnly passes.
# Set this to ["None"] for the stricter reading the ACR docs mention.
ALLOWED_DEFAULTS = ["None", "ReadOnly", ""]

# Your ACR setting "Excluded project roles = .Debug". Matched case-insensitively
# as a substring of the QUALIFIED module role name, so ".Debug" covers every
# module's Debug role (MyModule.Debug, Sales.Debug, ...). Adjust if your
# intention was exact role names instead.
EXCLUDED_ROLE_FRAGMENTS = [".debug"]


def _excluded(moduleRoleName):
    lowered = moduleRoleName.lower()
    for frag in EXCLUDED_ROLE_FRAGMENTS:
        if frag in lowered:
            return True
    return False


def check():
    violations = []

    # permissions() is NOT scoped the way entities() is: it returns rows for
    # platform and marketplace modules too. Those are read-only in practice, so
    # a finding on Administration or NanoflowCommons is noise nobody can act
    # on. Restrict to the entities entities() actually returns.
    inScope = {}
    for e in entities():
        if not e.is_external:
            inScope[e.qualified_name] = True

    # Stand down on an mxcli that predates the field (see the header).
    for probe in permissions():
        if not hasattr(probe, "default_member_access_rights"):
            return []
        break

    # One finding per entity + role, not per access type: the setting belongs
    # to the rule, and the catalog repeats it across every row that rule makes.
    seen = {}

    for p in permissions():
        if p.element_type != "ENTITY":
            continue
        if p.element_name not in inScope:
            continue
        if p.default_member_access_rights in ALLOWED_DEFAULTS:
            continue
        if _excluded(p.module_role_name):
            continue

        key = p.element_name + "|" + p.module_role_name
        if key in seen:
            continue
        seen[key] = True

        violations.append(violation(
            message = "Access rule for '{}' on '{}' defaults new members to {} - an attribute added later becomes writable without anyone deciding so.".format(
                p.module_role_name, p.element_name, p.default_member_access_rights,
            ),
            location = location(
                module = p.module_name,
                document_type = "Entity",
                document_name = p.element_name,
            ),
            suggestion = "Set 'Default rights for new members' to None or Read on that access rule.",
        ))

    return violations
