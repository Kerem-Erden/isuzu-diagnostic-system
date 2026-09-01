#include "can_stats.h"
#include "can_bus.h"

typedef struct
{
    bool used;
    can_stats_entry_t stats;
} can_stats_slot_t;

static can_stats_slot_t s_entries[CAN_STATS_MAX_IDS];

static can_stats_slot_t *find_entry(uint32_t id, bool is_extended)
{
    for (size_t i = 0; i < CAN_STATS_MAX_IDS; i++)
    {
        if (!s_entries[i].used)
        {
            continue;
        }

        if (s_entries[i].stats.id == id && s_entries[i].stats.is_extended == is_extended)
        {
            return &s_entries[i];
        }
    }

    return NULL;
}

static can_stats_slot_t *find_free_entry(void)
{
    for (size_t i = 0; i < CAN_STATS_MAX_IDS; i++)
    {
        if (!s_entries[i].used)
        {
            return &s_entries[i];
        }
    }

    return NULL;
}

/*
 * Record one received CAN frame in the statistics table.
 *
 * If this identifier is seen for the first time, a new slot is initialized.
 * Otherwise the existing slot is updated with the new frame count and
 * latest reception timestamp.
 *
 * Returns false if the input frame is invalid or if the statistics table
 * has no free slot remaining.
 */

bool can_stats_record_frame(const can_bus_frame_t *frame)
{
    if (frame == NULL)
    {
        return false;
    }

    /*
    * Search for an existing statistics slot that matches both the CAN identifier
    * value and the identifier format. Standard and extended frames with the same
    * numeric ID are treated as different entries.
    */

    can_stats_slot_t *entry = find_entry(frame->id, frame->is_extended);

    if (entry == NULL)
    {
        entry = find_free_entry();

        if (entry == NULL)
        {
            return false;
        }

        entry->used = true;
        
        entry->stats.id = frame->id;
        entry->stats.is_extended = frame->is_extended;
        entry->stats.count = 1;
        entry->stats.first_seen_us = frame->timestamp_us;
        entry->stats.last_seen_us = frame->timestamp_us;
        entry->stats.dlc = frame->dlc;

        return true;
    }

    entry->stats.count++;
    entry->stats.last_seen_us = frame->timestamp_us;
    entry->stats.dlc = frame->dlc;

    return true;
}

/*
 * Copy the currently collected CAN statistics into a caller-provided buffer.
 *
 * The internal table is not exposed directly so other modules cannot modify
 * the statistics module's private state.
 *
 * Returns the number of entries copied.
 */

size_t can_stats_snapshot(can_stats_entry_t *out_entries,  size_t max_entries)
{
    if (out_entries == NULL || max_entries == 0)
    {
        return 0;
    }

    size_t copied = 0;

    for (size_t i = 0; i < CAN_STATS_MAX_IDS; i++)
    {
        if (!s_entries[i].used)
        {
            continue;
        }

        if (copied >= max_entries)
        {
            break;
        }

        out_entries[copied] = s_entries[i].stats;
        copied++;
    }

    return copied;
}