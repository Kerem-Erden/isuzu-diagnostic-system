# Read-only DTC scan diagnostics

This change adds evidence collection; it does not establish ECU compatibility or fix the unresolved physical transceiver path. The ECU address, bitrate, receive filters, ISO-TP deadline and transmit/retry behavior are unchanged. No automatic CAN recovery or extra diagnostic requests are added.

## Build the diagnostic profile

Use ESP-IDF **6.0.2**, not an older installed SDK. Start with a new config file so an existing vehicle config cannot retain `CLEAR=1`:

```powershell
cd firmware/esp32-gateway
idf.py -B build-diagnostic -D SDKCONFIG=sdkconfig.diagnostic -D SDKCONFIG_DEFAULTS=sdkconfig.defaults.diagnostic build
```

If `sdkconfig.diagnostic` already exists, inspect it: `CONFIG_GATEWAY_ALLOW_CLEAR` must be disabled. Defaults do not override saved values. This command only builds; flashing and any active vehicle test require separate authorization. Never run the GPIO/loopback bench tests while connected to a vehicle.

The diagnostic defaults select 500 kbit/s and physical request `7E0`, expected response `7E8`. These are existing assumptions, not discovered vehicle capabilities. `INFO` must report `SOURCE=VEHICLE;ECU=7E0;CLEAR=0`. This verifies configuration, not ECU communication.

## Collect evidence

1. Before active testing, retain passive CAN evidence and verify the electrical path and vehicle addressing. Obtain approval for the exact single read-only test.
2. For the approved scan, open the desktop DTC screen. It sends **one scan automatically on entry**; do not repeatedly press Rescan. Do not open Live Data first if the approved scope is only Mode 03.
3. After success or failure, click **Copy diagnostic log** and save/paste the text. Include the exact desktop error and firmware/build revision used. Do not press Clear DTC Memory.
4. The log contains the latest 256 non-LIVE records, with UTC timestamps, outgoing request IDs, raw response/debug lines and the DTC screen status. Copy it promptly. It is an in-memory bounded log, not a complete persistent trace; reconnect and repeated scans can evict older records.

Keep a separate passive CAN capture where possible. The firmware intentionally captures only candidate `7E8`-`7EF` IDs; this diagnostic log cannot discover a different extended-addressing protocol. It counts other rejected frames without recording unrelated payloads.

## Interpret the firmware output

`DBG|SCAN|BEGIN` records bitrate and configured IDs. `CAN:DIAG` snapshots before and after show actual controller state, TEC/REC, TX success/failure counts, pending TX and dropped frames. Compare the deltas. A CAN ACK or `TX_OK` still does not prove a positive ECU service response.

Firmware buffers at most 32 request/response/Flow-Control frame records, then prints them **after** the transaction to avoid UART output delaying Flow Control. `TRACE_OMITTED` reports overflow. `TX_REQUEST` and `TX_FLOW_CONTROL` are attempted frames, not proof they reached the bus. `TS`/`START_US` are firmware microseconds; the desktop UTC timestamp is the later serial arrival time.

Every `DBG|...` record is a separate newline-terminated line. It never becomes part of a `RES` payload. `RES|id|ERR|SCAN_...;STAGE=...;NRC=...` identifies the failure boundary:

| Stage | Meaning |
|---|---|
| `CAN_TX` | Request submission/completion failed or timed out; inspect CAN diagnostics to distinguish the cause |
| `RX_WAIT` | No accepted initial reply before the existing deadline |
| `NEGATIVE_RESPONSE` | ECU returned `7F 03 NRC`; `78` means pending, still within the original deadline |
| `ISO_TP_LENGTH` | Invalid or unsupported transport length |
| `FLOW_CONTROL_TX` | Sending FC failed or timed out |
| `ISO_TP_RX` | Reply started; transfer did not finish |
| `ISO_TP_SEQUENCE` | Unexpected CF type or sequence |
| `MODE03_DECODE` | Payload assembled but count/code validation failed |
| `COMPLETE` | Valid payload decoded; `DTCS=` may legitimately be empty |

`NRC=00` means no negative response was recorded; a later successful response may retain an earlier `78`. `PAYLOAD_LEN=0` means no complete payload was captured, not zero stored DTCs. A valid empty Mode 03 payload is `4300` and produces `DTCS=`.

The desktop preserves raw firmware errors and names timed-out request IDs. If knowledge lookup fails after acquisition, received codes remain visible with `DTC description unavailable`. An audit write failure explicitly says the gateway scan returned codes but local history could not be saved; it continues to block any clearing workflow.

Simulation/host tests verify software paths only. They do not prove CAN wiring, transmission success or support by a particular vehicle. No Mode 04, security access, programming, broadcast discovery or repeated-query test is part of this procedure.
