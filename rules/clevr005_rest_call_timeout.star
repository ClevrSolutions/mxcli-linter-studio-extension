# CLEVR005: REST/Web services should have a timeout
#
# Mirrors the CLEVR ACR rule "REST/Web services should have a timeout"
# (Reliability, Critical).
#
# Without a timeout a hung remote system leaves the user waiting indefinitely.
# The docs call out the compounding case: several services called sequentially
# in one request, where any one of them can stall the whole thing.
#
# Checks Studio Pro's "Use a timeout" toggle on a Call REST service activity,
# NOT whether a number is present. Unticking the box leaves the seconds in
# place, so a rule keyed on the value passes calls that have no timeout.
#
# Requires mxcli with activity.use_request_timeout (mendixlabs/mxcli#1181).
#
# Install: copy to .claude/lint-rules/clevr005_rest_call_timeout.star
# Verify:  ./mxcli lint -p <project>.mpr --list-rules

RULE_ID = "CLEVR005"
RULE_NAME = "RestCallNoTimeout"
DESCRIPTION = "Call REST service activities must have a timeout"
CATEGORY = "reliability"
SEVERITY = "error"

# activities_for() needs the deeper catalog build.
REQUIRES = ["full"]

# Action types that make an outbound call. RestCallAction is the inline
# "Call REST service" activity, which is how most projects call REST.
CALL_ACTIONS = ["RestCallAction"]


def check():
    violations = []

    for mf in microflows():
        for act in activities_for(mf.qualified_name):
            if act.action_type not in CALL_ACTIONS:
                continue
            if act.use_request_timeout:
                continue

            violations.append(violation(
                message = "REST call in '{}' has no timeout - a hung remote system blocks the user indefinitely.".format(
                    mf.qualified_name,
                ),
                location = location(
                    module = mf.module_name,
                    document_type = "Microflow",
                    document_name = mf.name,
                ),
                suggestion = "Enable 'Use a timeout' on the Call REST service activity in '{}'.".format(
                    mf.qualified_name,
                ),
            ))

    return violations
