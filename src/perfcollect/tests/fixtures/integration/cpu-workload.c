#define _POSIX_C_SOURCE 200809L

#include <errno.h>
#include <inttypes.h>
#include <signal.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#include <unistd.h>

static volatile sig_atomic_t stopping;
static volatile uint64_t checksum;

static void stop_work(int signal_number)
{
    (void)signal_number;
    stopping = 1;
}

/* Keep a named, stack-walkable function and observable work under -O2. */
__attribute__((noinline))
void perfcollect_cpu_work(uint64_t seed)
{
    unsigned int iteration;
    for (iteration = 0; iteration < 1000000; ++iteration)
    {
        seed = seed * UINT64_C(6364136223846793005) + UINT64_C(1442695040888963407);
        seed ^= seed >> 17;
    }
    checksum = seed;
}

int main(int argc, char **argv)
{
    struct timespec start;
    struct timespec now;
    struct sigaction action = { 0 };
    char *end;
    long seconds;

    if (argc != 2)
    {
        fprintf(stderr, "Usage: %s duration-seconds (1-120)\n", argv[0]);
        return 2;
    }
    errno = 0;
    seconds = strtol(argv[1], &end, 10);
    if (errno != 0 || *end != '\0' || end == argv[1] || seconds < 1 || seconds > 120)
    {
        fprintf(stderr, "Invalid duration.\n");
        return 2;
    }

    action.sa_handler = stop_work;
    if (sigemptyset(&action.sa_mask) != 0 ||
        sigaction(SIGTERM, &action, NULL) != 0 ||
        sigaction(SIGINT, &action, NULL) != 0 ||
        clock_gettime(CLOCK_MONOTONIC, &start) != 0)
    {
        perror("workload initialization");
        return 1;
    }
    printf("ready pid=%ld duration=%ld\n", (long)getpid(), seconds);
    fflush(stdout);

    now = start;
    while (!stopping && now.tv_sec - start.tv_sec < seconds)
    {
        perfcollect_cpu_work(checksum + (uint64_t)now.tv_nsec);
        if (clock_gettime(CLOCK_MONOTONIC, &now) != 0)
        {
            perror("clock_gettime");
            return 1;
        }
    }
    printf("checksum=%" PRIu64 "\n", checksum);
    return 0;
}
