# PERF002: Avoid NOT/OR in XPath Retrieve Constraints
#
# Using OR or NOT in XPath retrieve constraints prevents the database engine
# from using indices efficiently, forcing a full table scan. This is particularly
# harmful for large tables or frequently-executed queries.
#
# Noncompliant (retrieve): [Status = 'Active' or Status = 'Pending']
# Noncompliant (retrieve): [not(IsDeleted = true())]
# Compliant:               Two separate retrieves, or re-model with a computed attribute.
#
# Note: This rule targets RETRIEVE-type XPath only. Security access rules
# legitimately use OR for multi-role membership checks and are excluded.
#
# Reference: ACR notorxpath.md, Mendix performance best practices.

RULE_ID = "PERF002"
RULE_NAME = "NoOrNotXPath"
DESCRIPTION = "Flags OR or NOT operators in retrieve XPath constraints — they prevent index usage and force full table scans"
CATEGORY = "performance"
SEVERITY = "warning"

def check():
    violations = []

    for e in xpath_expressions():
        if e.usage_type != "RETRIEVE":
            continue
        if not e.xpath_expression:
            continue

        ast = parse_xpath(e.xpath_expression)
        result = _find_or_not(ast) or _string_find_or_not(e.xpath_expression)
        if result:
            op_desc = result
            violations.append(violation(
                message="{} operator in retrieve XPath on '{}' in {} '{}' prevents index usage".format(
                    op_desc,
                    e.target_entity,
                    e.document_type.lower(),
                    e.document_qualified_name,
                ),
                location=location(
                    module=e.module_name,
                    document_type=e.document_type.lower(),
                    document_name=e.document_qualified_name,
                    document_id=e.document_id,
                ),
                suggestion="Split the retrieve into separate queries or restructure the condition to allow index usage",
            ))

    return violations

# ---------------------------------------------------------------------------
# AST helpers
# ---------------------------------------------------------------------------

def _find_or_not(root):
    """Walk the AST and return the first OR/NOT operator description found, or None."""
    if root == None:
        return None
    stack = [root]
    for _ in range(256):
        if not stack:
            break
        node = stack.pop()
        if node == None:
            continue
        k = node.kind

        if k in ("null", "string", "number", "bool", "empty", "variable",
                 "attr_path", "qname", "constant", "token", "recovered", "unknown"):
            continue

        if k == "bin":
            if node.op == "or":
                return "OR"
            stack.append(node.left)
            stack.append(node.right)
        elif k == "call":
            if node.name.lower() == "not":
                return "NOT"
            for a in node.args:
                stack.append(a)
        elif k == "unary":
            stack.append(node.operand)
        elif k == "paren":
            stack.append(node.inner)
        elif k == "if":
            stack.append(node.cond)
            stack.append(node.then)
            stack.append(node.else_)

    return None

def _string_find_or_not(xpath):
    """String-based fallback when AST traversal misses wrapper node types."""
    lower = xpath.lower()
    if " or " in lower:
        return "OR"
    if "not(" in lower:
        return "NOT"
    return None
