# SEC014: A strong password policy should be set
#
# Port of the ACR rule `SecurityPasswordPolicy`
# (codereviewer.rulesacr.projectrulesacr.ProjectSecurityPasswordPolicy,
#  docs/rules/security/strongpasswords.md).
#
# A strong password should not be too short AND should include digits, mixed
# case, and symbols. The built-in SEC002 rule already checks the minimum length
# (< 8); this rule complements it by checking the character-class requirements
# (digit / mixed-case / symbol), which SEC002 does not cover.

RULE_ID = "SEC014"
RULE_NAME = "StrongPasswordPolicy"
DESCRIPTION = "Password policy should require digits, mixed case, and symbols"
CATEGORY = "security"
SEVERITY = "warning"

def check():
    MIN_LENGTH = get_option("min_length", 8)
    sec = project_security()
    if sec == None:
        return []

    pp = sec.password_policy
    if pp == None:
        return []

    missing = []
    if not pp.require_digit:
        missing.append("digits")
    if not pp.require_mixed_case:
        missing.append("mixed case")
    if not pp.require_symbol:
        missing.append("symbols")
    if pp.min_length < MIN_LENGTH:
        missing.append("minimum length {} (currently {})".format(MIN_LENGTH, pp.min_length))

    if len(missing) == 0:
        return []

    return [violation(
        message="Password policy is weak — missing: {}.".format(", ".join(missing)),
        location=location(module="", document_type="security", document_name="PasswordPolicy"),
        suggestion="Strengthen the password policy to require digits, mixed case, symbols, and a minimum length of at least {} characters.".format(MIN_LENGTH),
    )]
