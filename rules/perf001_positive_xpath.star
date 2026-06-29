# PERF001: Prefer Positive XPath Conditions
#
# Negative conditions (!=, not()) in database retrieve constraints prevent the
# database from using indices, since it cannot index "what is not there".
# This specifically applies to enumeration attribute comparisons.
#
# Noncompliant: [EnumAttribute != Module.Enumeration.C]
# Compliant:    [EnumAttribute = Module.Enumeration.A or EnumAttribute = Module.Enumeration.B]
#
# Reference: Mendix performance best practices, negative XPath constraints.

RULE_ID = "PERF001"
RULE_NAME = "PositiveXPathCondition"
DESCRIPTION = "Flags negative enumeration XPath conditions (!=, not) in retrieve/security constraints — negative conditions prevent index usage"
CATEGORY = "performance"
SEVERITY = "warning"

def check():
    violations = []

    for e in xpath_expressions():
        if e.usage_type not in ("RETRIEVE", "SECURITY"):
            continue
        if not e.xpath_expression:
            continue

        ast = parse_xpath(e.xpath_expression)
        if _has_enum_negation(ast):
            violations.append(violation(
                message="Negative XPath condition in {} '{}' prevents index usage on '{}'".format(
                    e.document_type.lower(), e.document_qualified_name, e.target_entity
                ),
                location=location(
                    module=e.module_name,
                    document_type=e.document_type.lower(),
                    document_name=e.document_qualified_name,
                    document_id=e.document_id,
                ),
                suggestion="Rewrite '{}' using positive conditions (= and or) so the database can use indices".format(
                    e.xpath_expression
                ),
            ))

    return violations

# ---------------------------------------------------------------------------
# AST helpers
# ---------------------------------------------------------------------------

def _is_attr(node):
    """True for an attribute or association path: bare identifier (variable), attr_path, or 2-part qualified name."""
    return node.kind == "variable" or node.kind == "attr_path" or (node.kind == "qname" and node.sub == "")

def _is_enum_or_string(node):
    """True for an enumeration value (3-part qname) or a string literal."""
    return node.kind == "string" or (node.kind == "qname" and node.sub != "")

def _is_attr_enum_pair(left, right):
    """True when one side is an attribute path and the other is an enum/string literal."""
    return (_is_attr(left) and _is_enum_or_string(right)) or \
           (_is_enum_or_string(left) and _is_attr(right))

def _is_neq_pattern(node):
    """Matches: attr != Enum.Value"""
    return (node.kind == "bin" and node.op == "!=" and
            _is_attr_enum_pair(node.left, node.right))

def _is_not_eq_pattern(node):
    """Matches: not(attr = Enum.Value)"""
    if node.kind != "call" or node.name.lower() != "not":
        return False
    for arg in node.args:
        if (arg.kind == "bin" and arg.op == "=" and
                _is_attr_enum_pair(arg.left, arg.right)):
            return True
    return False

def _has_enum_negation(root):
    """Walks the AST using an explicit stack (no recursion, no while — Starlark restrictions).
    Uses for _ in range(N) as a bounded loop over a mutable work list.
    """
    if root == None:
        return False
    stack = [root]
    for _ in range(256):  # XPath ASTs are shallow in practice; 256 is a safe upper bound
        if not stack:
            break
        node = stack.pop()
        if node == None:
            continue
        k = node.kind

        # Leaf nodes — no children to inspect
        if k in ("null", "string", "number", "bool", "empty", "variable",
                 "attr_path", "qname", "constant", "token", "recovered", "unknown"):
            continue

        # Pattern check at this node
        if _is_neq_pattern(node) or _is_not_eq_pattern(node):
            return True

        # Push children onto the stack
        if k == "bin":
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
