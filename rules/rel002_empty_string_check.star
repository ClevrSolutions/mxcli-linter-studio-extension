# REL002: Use trim() When Checking for Empty Strings in XPath
#
# Comparing an attribute directly to an empty string literal misses records that
# contain only whitespace. The correct pattern wraps the attribute in trim() so
# that strings like ' ' (space only) are correctly treated as empty.
#
# Noncompliant: [Name = '']
# Noncompliant: [Description != '']
# Compliant:    [trim(Name) = '']
# Compliant:    [trim(Description) != '']
#
# Reference: ACR emptystringcheck.md, Mendix XPath string handling.

RULE_ID = "REL002"
RULE_NAME = "EmptyStringCheck"
DESCRIPTION = "Flags direct comparisons to '' in XPath — use trim(attr) = '' to also match whitespace-only strings"
CATEGORY = "reliability"
SEVERITY = "warning"

def check():
    violations = []

    for e in xpath_expressions():
        if not e.xpath_expression:
            continue

        ast = parse_xpath(e.xpath_expression)
        if _has_bare_empty_string(ast):
            violations.append(violation(
                message="Direct empty-string comparison in {} '{}' may miss whitespace-only records".format(
                    e.document_type.lower(),
                    e.document_qualified_name,
                ),
                location=location(
                    module=e.module_name,
                    document_type=e.document_type.lower(),
                    document_name=e.document_qualified_name,
                    document_id=e.document_id,
                ),
                suggestion="Replace '[Attr = '']' with '[trim(Attr) = '']' to treat whitespace-only values as empty",
            ))

    return violations

# ---------------------------------------------------------------------------
# AST helpers
# ---------------------------------------------------------------------------

def _is_empty_string(node):
    """True for a string literal with an empty value."""
    return node.kind == "string" and node.value == ""

def _is_trim_call(node):
    """True for a call to trim() (case-insensitive)."""
    return node.kind == "call" and node.name.lower() == "trim"

def _is_path_or_trim(node):
    """True for a bare attribute path OR a trim() call wrapping a path (the compliant form)."""
    if _is_trim_call(node):
        return True
    return node.kind in ("variable", "attr_path") or \
           (node.kind == "qname" and node.sub == "")

def _has_bare_empty_string(root):
    """Walk the AST and return True if any = / != comparison pairs an attribute
    (without trim) directly against an empty string literal."""
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

        if k == "bin" and node.op in ("=", "!="):
            left = node.left
            right = node.right
            # Pattern: bare_attr = '' or '' = bare_attr
            if _is_empty_string(right) and not _is_trim_call(left) and _is_path_or_trim(left):
                if not _is_trim_call(left):
                    return True
            if _is_empty_string(left) and not _is_trim_call(right) and _is_path_or_trim(right):
                if not _is_trim_call(right):
                    return True
            stack.append(left)
            stack.append(right)
        elif k == "bin":
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
