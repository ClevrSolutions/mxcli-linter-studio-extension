# CLEVR001: A strong password policy should be set
#
# Fails when the project's password policy is still at the Mendix defaults,
# i.e. nobody has deliberately hardened it.
#
# Mendix defaults (Project Security > Password policy):
#   min_length = 8, require_digit = False, require_mixed_case = False,
#   require_symbol = False
#
# Install: copy to .claude/lint-rules/clevr001_strong_password_policy.star
# Verify:  mxcli lint -p app.mpr --list-rules
#
# Note: CLEVR001 overlaps slightly with the bundled SEC005 (strict mode) only
# in spirit, not in checks. Drop the security-level block below if you would
# rather keep level enforcement in a separate rule.

RULE_ID = "CLEVR001"
RULE_NAME = "WeakPasswordPolicy"
DESCRIPTION = "Project security must define a deliberately hardened password policy, not the Mendix defaults"
CATEGORY = "security"
SEVERITY = "error"

# Mendix out-of-the-box values. A policy still sitting on these counts as "not set".
DEFAULT_MIN_LENGTH = 8


def _loc():
    return location(module = "", document_type = "security", document_name = "ProjectSecurity")


def check():
    loc = _loc()
    sec = project_security()
    if sec == None:
        # No MPR reader available - cannot verify, so do not fail the build here.
        return []

    violations = []

    # A password policy is only enforced when security is actually on.
    if sec.security_level == "CheckNothing":
        return [violation(
            message = "Project security level is Off, so no password policy is enforced.",
            location = loc,
            suggestion = "alter project security level production",
        )]

    pp = sec.password_policy

    if pp.min_length <= DEFAULT_MIN_LENGTH:
        violations.append(violation(
            message = "Password minimum length is {}, the Mendix default. A strong policy must raise it explicitly.".format(pp.min_length),
            location = loc,
            suggestion = "alter project security password policy minimum length 12",
        ))

    missing = []
    if not pp.require_digit:
        missing.append("digit")
    if not pp.require_mixed_case:
        missing.append("mixed case")
    if not pp.require_symbol:
        missing.append("symbol")

    if len(missing) == 3:
        violations.append(violation(
            message = "Password policy requires no character classes at all - it is still the Mendix default.",
            location = loc,
            suggestion = "alter project security password policy require digit, require mixed case, require symbol",
        ))
    elif len(missing) > 0:
        violations.append(violation(
            message = "Password policy does not require: {}.".format(", ".join(missing)),
            location = loc,
            suggestion = "Enable the remaining character-class requirements in Project Security > Password policy.",
        ))

    return violations
