#define _POSIX_C_SOURCE 200809L
#define TRACEPOINT_CREATE_PROBES
#define TRACEPOINT_DEFINE

#include "perfcollect-ust-events.h"

#include <errno.h>
#include <stdio.h>
#include <time.h>

static int pause_milliseconds(long milliseconds)
{
    struct timespec remaining = { milliseconds / 1000, (milliseconds % 1000) * 1000000 };
    while (nanosleep(&remaining, &remaining) != 0)
    {
        if (errno != EINTR)
        {
            perror("nanosleep");
            return 1;
        }
    }
    return 0;
}

int main(int argc, char **argv)
{
    unsigned int attempt;
    unsigned int sequence;

    if (argc != 2 || argv[1][0] == '\0')
    {
        fprintf(stderr, "Usage: %s unique-marker\n", argv[0]);
        return 2;
    }

    /* Registration with the session daemon is asynchronous. Do not emit an
       event until the actual tracepoint is enabled; time out instead of
       reporting a successful producer that recorded nothing. */
    for (attempt = 0; attempt < 750; ++attempt)
    {
        if (tracepoint_enabled(perfcollect_test, tick))
        {
            break;
        }
        if (pause_milliseconds(20) != 0)
        {
            return 1;
        }
    }
    if (!tracepoint_enabled(perfcollect_test, tick))
    {
        fprintf(stderr, "UST tracepoint was not enabled within 15 seconds.\n");
        return 3;
    }

    for (sequence = 0; sequence < 128; ++sequence)
    {
        tracepoint(perfcollect_test, tick, argv[1], sequence);
        if (pause_milliseconds(5) != 0)
        {
            return 1;
        }
    }
    printf("emitted=128 marker=%s\n", argv[1]);
    return 0;
}
