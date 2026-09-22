# ESP32–desktop serial protocol (MVP)

Transport is UTF-8/ASCII-compatible, 115200 baud, line-oriented. Commands and responses end in a newline. An input line longer than 127 bytes is discarded by firmware through its terminator; the desktop framer has a 1024-character defensive bound.

## Correlated requests

```text
REQ|positive_request_id|COMMAND
RES|same_request_id|OK|PAYLOAD
RES|same_request_id|ERR|ERROR_CODE
```

Exactly three request fields and four response fields are accepted. Embedded `|`, empty payloads, non-positive/non-numeric IDs and oversized lines are rejected. A caller matches only the response carrying its own request ID.

| Command | Successful payload | Effect |
|---|---|---|
| `PING` | `PONG` | Correlated heartbeat |
| `INFO` | `SOURCE=SIMULATION;ECU=DEMO;CLEAR=1` or vehicle identity | Fail-closed source/capability handshake |
| `START` | `STREAMING` | Begin live messages |
| `STOP` | `STOPPED` | Stop live messages |
| `STATUS` | `STATE=IDLE` / `STATE=STREAMING` | Gateway stream state |
| `SCAN_DTC` | `DTCS=P1093,P0087` or `DTCS=` | Fresh stored-code scan |
| `CLEAR_DTC` | `CLEARED` | One accepted Mode 04 operation |

Firmware remembers the last clear request ID and returns `DUPLICATE_CLEAR` instead of repeating that write. The desktop also never automatically retries clear.

## Unsolicited messages

```text
SYS:READY
SYS:REQUEST_TOO_LONG
LIVE:RPM:1800
LIVE:COOLANT_TEMP:82
LIVE:BATTERY_VOLTAGE:28.4
LIVE:SPEED:0
LIVE:ENGINE_LOAD:20
EVT|OBD_ERROR|RPM:ESP_ERR_TIMEOUT
```

Live values use invariant-culture decimal notation and must be finite. A serial `CLEARED` response proves only that the gateway received a positive ECU Mode 04 response; the desktop still rescans and presents remaining stored codes.

## Clear safety contract

The desktop must persist a fresh `SCAN_DTC` result before asking for confirmation. Firmware vehicle mode additionally requires its own successful scan less than 30 seconds old, RPM exactly zero and speed exactly zero. Mode 04 is sent once. A timeout or disconnect produces an **unknown result**, retains the pre-clear snapshot and requires a new scan; it is never displayed as an empty DTC list.
