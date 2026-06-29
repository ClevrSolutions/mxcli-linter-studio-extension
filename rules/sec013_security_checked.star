# SEC013: Project security must be checked
#
# Port of the ACR rule `SecurityChecked`
# (codereviewer.rulesacr.projectrulesacr.ProjectSecurityChecked,
#  docs/rules/security/securitychecked.md).
#
# Mendix can verify that pages accessible to a role only refer to attributes and
# associations that are also accessible to that role. This "security checked"
# option should always be enabled to avoid security vulnerabilities where a page
# leaks members a role is not allowed to see.

RULE_ID = "SEC013"
RULE_NAME = "ProjectSecurityChecked"
DESCRIPTION = "Project 'security checked' option should be enabled to validate page/role member access"
CATEGORY = "security"
SEVERITY = "warning"

def check():
    sec = project_security()
    if sec == None:
        return []

    if sec.check_security:
        return []

    return [violation(
        message="Project security checking is disabled. Pages may reference attributes or associations a role cannot access.",
        location=location(module="", document_type="security", document_name="ProjectSecurity"),
        suggestion="Enable the 'security checked' option in project security so the modeler validates page member access against role access rules.",
    )]
