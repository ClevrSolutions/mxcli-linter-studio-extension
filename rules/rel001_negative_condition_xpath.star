# REL001: Use not() Instead of != in XPath Conditions
#
# In Mendix XPath, `[Attribute != 'value']` silently excludes records where
# Attribute is empty/NULL. The semantically correct way to exclude a specific
# value while still including NULL records is `[not(Attribute = 'value')]`.
#
# Noncompliant: [Status != 'Cancelled']
# Compliant:    [not(Status = 'Cancelled')]
#
# This is a reliability (correctness) concern, distinct from PERF001 which flags
# enum negations for index-usage performance reasons.
#
# Reference: ACR negativeconditionxpath.md, Mendix XPath NULL semantics.

RULE_ID = "REL001"
RULE_NAME = "NegativeConditionXPath"
DESCRIPTION = "Flags != comparisons in XPath constraints — use not(attr = value) to correctly include NULL records"
CATEGORY = "reliability"
SEVERITY = "warning"

def check():
    violations = []

    for e in xpath_expressions():
        if not e.xpath_expression:
            continue

        ast = parse_xpath(e.xpath_expression)
        if _has_neq_pattern(ast):
            violations.append(violation(
                message="Negative condition '!=' in {} '{}' excludes NULL records — use not(attr = value) instead".format(
                    e.document_type.lower(),
                    e.document_qualified_name,
                ),
                location=location(
                    module=e.module_name,
                    document_type=e.document_type.lower(),
                    document_name=e.document_qualified_name,
                    document_id=e.document_id,
                ),
                suggestion="Replace '[Attr != value]' with '[not(Attr = value)]' to include records where Attr is empty",
            ))

    return violations

# ---------------------------------------------------------------------------
# AST helpers
# ---------------------------------------------------------------------------

def _is_path(node):
    """True for an attribute or association path node."""
    return node.kind in ("variable", "attr_path") or \
           (node.kind == "qname" and node.sub == "")

def _is_value(node):
    """True for a literal value: string, number, boolean, empty, or enum qname."""
    return node.kind in ("string", "number", "bool", "empty") or \
           (node.kind == "qname" and node.sub != "")

def _is_attr_value_pair(left, right):
    """True when one side is a path and the other is a value literal."""
    return (_is_path(left) and _is_value(right)) or \
           (_is_value(left) and _is_path(right))

def _has_neq_pattern(root):
    """Walk the AST and return True if any != comparison on an attr/value pair is found."""
    if root == None:
        return False
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
            if node.op == "!=" and _is_attr_value_pair(node.left, node.right):
                return True
            stack.append(node.left)
            stack.append(node.right)
        elif k == "unary":
            stack.append(node.operand)
        elif k == "call":
            for a in node.args:
                stack.append(a)
        elif k == "paren":
            stack.append(node.inner)
        elif k == "if":
            stack.append(node.cond)
            stack.append(node.then)
            stack.append(node.else_)

    return False
