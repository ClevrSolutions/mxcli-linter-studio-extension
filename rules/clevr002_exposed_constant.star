# CLEVR002: Constants should not be exposed to the client
#
# Mirrors the CLEVR ACR rule "Constants should not be exposed to the client"
# (Security), run at its default configuration: the sensitive-keyword list is
# empty, which in ACR means *every* constant exposed to the client is a
# violation - not only ones whose name looks secret.
#
# Why: when "Exposed to client" is on, Mendix Runtime sends the constant's
# value to the browser so nanoflows and page expressions can read it. Anything
# in that value is readable by anyone with devtools.
#
# https://docs.mendix.com/refguide/constants#4-2-exposed-to-client
#
# Install: copy to .claude/lint-rules/clevr002_exposed_constant.star
# Verify:  mxcli lint -p app.mpr --list-rules
#
# Scope note: constants() already skips the System module. Marketplace modules
# (Atlas_Core, Administration, ...) are NOT skipped by default - add them to
# excludeModules in .claude/lint-config.yaml if their constants create noise.
#
# Severity note: 'warning' rather than 'error' because with no keyword filter
# this fires on legitimately public constants too (AppBaseUrl and friends).
# Each finding needs a human "reviewed, this one is fine" decision, which is a
# poor fit for a build blocker. CLEVR003 (SensitiveConstant) is the keyword-
# targeted counterpart and is the one set to 'error'.

RULE_ID = "CLEVR002"
RULE_NAME = "ExposedConstant"
DESCRIPTION = "Constants should not be exposed to the client - the Runtime sends their value to the browser"
CATEGORY = "security"
SEVERITY = "warning"


def check():
    violations = []

    for c in constants():
        if not c.exposed_to_client:
            continue

        violations.append(violation(
            message = "Constant '{}' is exposed to the client, so its value is sent to the browser and is readable by anyone.".format(c.qualified_name),
            location = location(
                module = c.module_name,
                document_type = "constant",
                document_name = c.name,
            ),
            # Studio Pro only - MDL has no statement for the exposed-to-client flag.
            suggestion = "Turn off 'Exposed to client' on {} in Studio Pro. If a nanoflow or page expression genuinely needs the value, read it through a microflow instead.".format(c.qualified_name),
        ))

    return violations
