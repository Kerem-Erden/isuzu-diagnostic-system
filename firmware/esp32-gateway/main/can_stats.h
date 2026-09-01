#ifndef CAN_STATS_H
#define CAN_STATS_H

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#include "can_bus.h"

#define CAN_STATS_MAX_IDS 64

typedef struct
{
    uint32_t id;
    bool is_extended;

    uint32_t count;

    int64_t first_seen_us;
    int64_t last_seen_us;

    uint8_t dlc;
} can_stats_entry_t;

size_t can_stats_snapshot(can_stats_entry_t *out_entries, size_t max_entries);
bool can_stats_record_frame(const can_bus_frame_t *frame);

#endif // CAN_STATS_H
