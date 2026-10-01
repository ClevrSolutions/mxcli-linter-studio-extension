# CLEVR007: Entity should have a maximum of N calculated attributes
#
# Mirrors the CLEVR ACR rule "Entity should have a maximum of N calculated
# attributes" (Performance, Critical).
#
# A calculated (virtual) attribute runs its microflow every time the object is
# retrieved, so each one multiplies the cost of every retrieve of that entity.
# A handful on one entity turns a cheap list into an expensive one.
#
# Threshold taken from your Dev Suite configuration:
# "Maximum amount of calculated attributes = 3".
#
# Note mxcli's bundled CONV017 is stricter - it flags calculated attributes
# outright. This rule is the ACR threshold version; run whichever matches your
# policy rather than both.
#
# Install: copy to .claude/lint-rules/clevr007_calculated_attribute_count.star

RULE_ID = "CLEVR007"
RULE_NAME = "TooManyCalculatedAttributes"
DESCRIPTION = "An entity should not carry more than the configured number of calculated attributes"
CATEGORY = "performance"
SEVERITY = "error"

# Your ACR setting. More than this on one entity is flagged.
MAX_CALCULATED = 3


def check():
    violations = []

    for e in entities():
        # External entities live in another app; their attributes are not ours.
        if e.is_external:
            continue

        calculated = []
        for attr in attributes_for(e.qualified_name):
            if attr.is_calculated:
                calculated.append(attr.name)

        if len(calculated) <= MAX_CALCULATED:
            continue

        violations.append(violation(
            message = "Entity '{}' has {} calculated attributes (limit {}): {}. Each one runs its microflow on every retrieve of this entity.".format(
                e.qualified_name, len(calculated), MAX_CALCULATED,
                ", ".join(sorted(calculated)),
            ),
            location = location(
                module = e.module_name,
                document_type = "Entity",
                document_name = e.name,
            ),
            suggestion = "Replace the least-used calculated attributes with stored attributes updated by a microflow.",
        ))

    return violations
