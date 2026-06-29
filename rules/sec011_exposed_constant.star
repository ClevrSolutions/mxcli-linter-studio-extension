# SEC011: Constants Should Not Be Exposed to the Client
#
# Port of the ACR rule `Constant_ExposedConstant`
# (codereviewer.rulesacr.constantrulesacr.ConstantExposed).
#
# When a constant is exposed to the client, the Mendix runtime sends its value
# to the browser so it is accessible from nanoflows and page expressions.
# Constants holding sensitive values (passwords, keys, tokens, etc.) must
# never be exposed this way.
#
# Configure SENSITIVE_KEYWORDS to restrict violations to constants whose names
# match at least one keyword. Set it to [] to flag every exposed constant.

RULE_ID = "SEC011"
RULE_NAME = "ExposedConstant"
DESCRIPTION = "Constants exposed to the client should not contain sensitive values (passwords, keys, tokens, etc.)"
CATEGORY = "security"
SEVERITY = "error"

# Substrings matched against the constant name (case-insensitive).
# Leading/trailing * wildcards are stripped before matching.
# Set to [] to flag ALL constants that are exposed to the client.
SENSITIVE_KEYWORDS = ["password", "key", "secret", "token", "credential"]

SKIP_MODULES = ("System", "Administration")

def _is_sensitive(name):
    name_lower = name.lower()
    for kw in SENSITIVE_KEYWORDS:
        if kw.strip("*").lower() in name_lower:
            return True
    return False

def check():
    violations = []
    for c in constants():
        if c.module_name in SKIP_MODULES:
            continue
        if not c.exposed_to_client:
            continue

        if len(SENSITIVE_KEYWORDS) == 0 or _is_sensitive(c.name):
            kw_hint = " (name matches sensitive keyword)" if len(SENSITIVE_KEYWORDS) > 0 else ""
            violations.append(violation(
                message="Constant '{}' is exposed to the client{} — its value is sent to the browser and accessible from nanoflows and page expressions.".format(
                    c.qualified_name, kw_hint
                ),
                location=location(
                    module=c.module_name,
                    document_type="Constant",
                    document_name=c.qualified_name,
                ),
                suggestion="Disable 'Exposed to client' on '{}', or move the sensitive value to a Java action that never reaches the client.".format(c.qualified_name),
            ))
    return violations
