# Week 4 verification record

Date: 2026-09-22

## Implemented and reproducible

| Area | Evidence | Result |
|---|---|---|
| Serial parser | malformed IDs/fields, finite live values, oversized framing tests | Host test implemented |
| Mode 03 | count byte, zero/malformed cases, single and multi-frame decode | Host test implemented |
| ISO-TP | target ID filter, absolute timeout, CF sequence, flow-control transmit | Host test implemented |
| Mode 04 | one-shot request, duplicate request-ID suppression | Host test implemented |
| Safe clear UI | fresh scan, durable pre-clear audit, user confirmation, rescan, unknown-result preservation | Desktop core test implemented |
| Live warnings | confirmation duration, hysteresis, stale-gap reset, transition-only logging | Core test + application path |
| Source separation | mandatory INFO handshake and visible simulation/vehicle labelling | Application + firmware |
| Build matrix | .NET core, Windows WPF, ESP-IDF simulation and vehicle profiles | GitHub Actions workflow |

## Physical validation status

- Passive vehicle observation previously detected 500 kbit/s traffic.
- Direct GPIO21→GPIO22 loopback previously passed.
- With the SN65HVD230 module, CTX state changed but CRX remained high during the 1→0→1 bench sequence. The result was `ESP_FAIL` with both tried modules.
- A multimeter/oscilloscope continuity and voltage check was not available.
- Therefore real ECU Mode 01/03/04 validation is **blocked by the physical transceiver path** and remains unverified.

## Required vehicle gate

Do not claim vehicle MVP completion until all items below are recorded with raw logs:

1. Verify 3V3/GND, CTX/CRX continuity and CANH/CANL polarity/termination with instruments.
2. Pass the transceiver 1→0→1 bench test while disconnected from the vehicle.
3. Build vehicle mode with Mode 04 disabled and record a successful physical ECU Mode 01 response.
4. Record a real Mode 03 reply and compare the decoded count/codes with an independent scan tool.
5. On an authorized test vehicle only, enable Mode 04, capture the pre-clear scan, ensure engine stopped/vehicle stationary, confirm once and independently verify the post-clear scan.

Simulation proves application workflow, not CAN wiring, ECU compatibility or the safety of clearing a real vehicle.
