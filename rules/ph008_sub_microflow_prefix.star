# PH008: Sub-microflows should be prefixed with SUB_
#
# Ported (as a heuristic) from ACR project-hygiene rule `MicroflowPrefixSubmicroflows`
# (docs/rules/project-hygiene/microflowprefixsubmicroflows.md).
#
# A microflow that exists only to be called by other microflows (a "sub") should be
# named SUB_... so it is recognisable as a helper rather than an entry point.
#
# HEURISTIC: the API does not label microflows as subs, so we infer it from
# cross-references — a microflow that is CALLED by another microflow and has no UI
# (page/snippet) caller is treated as a sub. Microflows already carrying a recognised
# functional prefix are left alone.

RULE_ID = "PH008"
RULE_NAME = "SubMicroflowPrefix"
DESCRIPTION = "Sub-microflows (called only by other microflows) should start with SUB_"
CATEGORY = "naming"
SEVERITY = "info"

SKIP_MODULES = ("System", "Administration")

# Prefixes that already convey a microflow's role; such flows are exempt.
KNOWN_PREFIXES = (
    "sub_", "nsub_", "act_", "ds_", "och_", "bch_", "oen_", "val_",
    "ws_", "rest_", "ivk_", "sce_", "sch_", "bes_", "nav_",
    "asu_", "bsd_", "hch_",
)
UI_SOURCES = ("page", "snippet", "widget", "nanoflow")

def _has_known_prefix(name):
    low = name.lower()
    for p in KNOWN_PREFIXES:
        if low.startswith(p):
            return True
    return False

def check():
    violations = []
    for mf in microflows():
        if mf.module_name in SKIP_MODULES:
            continue
        if mf.microflow_type.lower() != "microflow":
            continue
        if _has_known_prefix(mf.name):
            continue

        has_microflow_caller = False
        has_ui_caller = False
        for ref in refs_to(mf.qualified_name):
            st = ref.source_type.lower()
            if st == "microflow":
                has_microflow_caller = True
            if st in UI_SOURCES:
                has_ui_caller = True

        if has_microflow_caller and not has_ui_caller:
            violations.append(violation(
                message="Microflow '{}' is only called by other microflows but lacks a SUB_ prefix.".format(
                    mf.name),
                location=location(
                    module=mf.module_name,
                    document_type="Microflow",
                    document_name=mf.qualified_name,
                ),
                suggestion="Rename to 'SUB_{}'.".format(mf.name),
            ))
    return violations
