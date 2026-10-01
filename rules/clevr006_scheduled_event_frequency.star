# CLEVR006: Scheduled events should be executed with reasonable delay
#
# Mirrors the CLEVR ACR rule "Scheduled events should be executed with
# reasonable delay" (Performance, Critical).
#
# An event that fires every few seconds or minutes keeps a runtime thread busy
# and can overlap its own previous run. Older Mendix versions allowed a
# per-second schedule, which is the pathological case this rule exists for.
#
# Floor: 5 minutes. Anything firing MORE often than that is flagged; exactly
# 5 minutes passes.
#
# ---------------------------------------------------------------------------
# A caveat worth knowing, because it decides what this rule can and cannot see
# ---------------------------------------------------------------------------
# mxcli derives interval_seconds from the event's Schedule child, and
# deliberately ignores the legacy Interval / IntervalType pair. Its own comment
# gives the reason: Studio Pro writes that pair and does not keep it in sync,
# and Workflow Commons ships an event storing 0/"Minute" beside a DaySchedule
# of 01:00 — so a rule keyed on the legacy pair reads it as "every 0 seconds".
#
# Consequence: Mendix's current schedule kinds bottom out at Minute, so the
# smallest interval this rule can observe is 60s. A genuinely legacy
# per-second event is only visible here if its Schedule child says so. If it
# has no Schedule child at all, interval_seconds is 0 — which is why 0 gets
# its own finding below rather than being skipped. Delete that branch if it
# proves noisy on your projects.
#
# Install: copy to .claude/lint-rules/clevr006_scheduled_event_frequency.star

RULE_ID = "CLEVR006"
RULE_NAME = "ScheduledEventTooFrequent"
DESCRIPTION = "Scheduled events should not run more often than every 5 minutes"
CATEGORY = "performance"
SEVERITY = "error"

# The floor, in seconds. Anything strictly below this is flagged.
MIN_INTERVAL_SECONDS = 300


def check():
    violations = []

    for ev in scheduled_events():
        # A disabled event never runs, so its interval costs nothing.
        if not ev.enabled:
            continue

        interval = ev.interval_seconds

        # 0 means no readable Schedule child. That is not "fine" - it is
        # "unknown", and on an old project it can be exactly the per-second
        # event this rule is meant to catch.
        if interval == 0:
            violations.append(violation(
                message = "Scheduled event '{}' has no readable schedule, so its frequency cannot be checked. On an older project this can be a legacy per-second event.".format(
                    ev.qualified_name,
                ),
                location = location(
                    module = ev.module_name,
                    document_type = "ScheduledEvent",
                    document_name = ev.name,
                ),
                suggestion = "Open the event in Studio Pro and set an explicit schedule of at least {} minutes.".format(
                    MIN_INTERVAL_SECONDS // 60,
                ),
            ))
            continue

        if interval >= MIN_INTERVAL_SECONDS:
            continue

        violations.append(violation(
            message = "Scheduled event '{}' fires every {}s, below the {}s ({} minute) floor - it keeps a runtime thread busy and can overlap its own previous run.".format(
                ev.qualified_name, interval, MIN_INTERVAL_SECONDS,
                MIN_INTERVAL_SECONDS // 60,
            ),
            location = location(
                module = ev.module_name,
                document_type = "ScheduledEvent",
                document_name = ev.name,
            ),
            suggestion = "Raise the interval to at least {} minutes, or move the work to a queue if it genuinely needs to run continuously.".format(
                MIN_INTERVAL_SECONDS // 60,
            ),
        ))

    return violations
