# SEC012: Security should be enabled and set to Production
#
# Port of the ACR rule `SecurityLevel`
# (codereviewer.rulesacr.projectrulesacr.ProjectSecurityLevel,
#  docs/rules/security/securitylevel.md).
#
# Security should be considered from the start of a project. Keeping security
# disabled (or below Production) until release leads to "generous" access as
# developers race to clear Mendix access errors. At Production level the modeler
# forces deliberate access decisions from the get-go.
#
# In the model, the Production security level corresponds to "CheckEverything".

RULE_ID = "SEC012"
RULE_NAME = "SecurityLevelProduction"
DESCRIPTION = "Project security should be enabled and set to Production (CheckEverything)"
CATEGORY = "security"
SEVERITY = "error"

def check():
    sec = project_security()
    if sec == None:
        return []

    if sec.security_level == "CheckEverything":
        return []

    return [violation(
        message="Project security level is '{}', not Production. Security is not fully enforced.".format(
            sec.security_level
        ),
        location=location(module="", document_type="security", document_name="ProjectSecurity"),
        suggestion="Set the project security level to Production so access rules are enforced from the start of development.",
    )]
