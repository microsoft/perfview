#undef TRACEPOINT_PROVIDER
#define TRACEPOINT_PROVIDER perfcollect_test

#undef TRACEPOINT_INCLUDE
#define TRACEPOINT_INCLUDE "./perfcollect-ust-events.h"

#if !defined(PERFCOLLECT_FIXTURE_UST_EVENTS_H) || defined(TRACEPOINT_HEADER_MULTI_READ) || defined(LTTNG_UST_TRACEPOINT_HEADER_MULTI_READ)
#define PERFCOLLECT_FIXTURE_UST_EVENTS_H

#include <lttng/tracepoint.h>

TRACEPOINT_EVENT(
    perfcollect_test,
    tick,
    TP_ARGS(const char *, marker, unsigned int, sequence),
    TP_FIELDS(
        ctf_string(marker, marker)
        ctf_integer(unsigned int, sequence, sequence)
    )
)

#endif

#include <lttng/tracepoint-event.h>
