# SEC017: Only "admin" roles should manage other users
#
# Port of the ACR rule `SecurityRolesToManageUsers`
# (codereviewer.rulesacr.projectrulesacr.ProjectSecurityRolesToManageUsers,
#  docs/rules/security/adminsmanageusers.md).
#
# Roles without an admin marker in their name (default substring "Admin") should
# not be able to manage other users. The ACR rule inspects the project-level
# "user management" grants per user role.
#
# HEURISTIC: the lint API does not expose per-user-role "can manage these user
# roles" grants, so we approximate the intent at the data layer: a module role
# whose name does NOT contain an admin marker should not have CREATE / WRITE /
# DELETE on the user account entity (Administration.Account or any entity
# extending System.User). Such access effectively lets that role manage users.

RULE_ID = "SEC017"
RULE_NAME = "AdminsManageUsers"
DESCRIPTION = "Only admin-named roles should be able to create/modify user accounts"
CATEGORY = "security"
SEVERITY = "warning"

# Case-insensitive substrings that mark a role as administrative (and thus exempt).
ADMIN_MARKERS = ("admin",)
MANAGE_ACCESS = ("create", "write", "delete")

def _is_admin_role(name):
    low = name.lower()
    for m in ADMIN_MARKERS:
        if m in low:
            return True
    return False

def _is_user_entity(e):
    if e.qualified_name == "Administration.Account":
        return True
    gen = e.generalization
    if gen != None and gen.endswith("System.User"):
        return True
    return False

def check():
    # Collect user-account entities: entities() skips Administration module,
    # so we include Administration.Account explicitly via its known qualified name.
    # System.User entities that are extended by app modules ARE returned by entities().
    user_entity_names = {}
    user_entity_names["Administration.Account"] = "Administration"
    for e in entities():
        if e.is_external:
            continue
        gen = e.generalization
        if gen != None and gen.endswith("System.User"):
            user_entity_names[e.qualified_name] = e.module_name

    violations = []
    for entity_qname, entity_module in user_entity_names.items():
        for perm in permissions_for(entity_qname):
            if perm.access_type.lower() not in MANAGE_ACCESS:
                continue
            if _is_admin_role(perm.module_role_name):
                continue
            # Report violation in the module that owns the non-admin role
            role_module = perm.module_role_name.split(".")[0] if "." in perm.module_role_name else entity_module
            violations.append(violation(
                message="Non-admin role '{}' has {} access on user-account entity '{}' — it can manage other users.".format(
                    perm.module_role_name, perm.access_type, entity_qname
                ),
                location=location(
                    module=role_module,
                    document_type="Entity",
                    document_name=entity_qname,
                ),
                suggestion="Restrict create/modify access on the account entity to administrative roles only, or rename the role to reflect that it is administrative.",
            ))
    return violations
