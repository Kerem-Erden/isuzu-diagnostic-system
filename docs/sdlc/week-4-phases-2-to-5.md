# Week 4 SDLC record — phases 2 to 5

## Phase 2: analysis

The week-4 goal was reduced to five observable flows: Mode 01 live data, Mode 03 stored-code scan, reference evaluation, safe Mode 04, and recovery from bad input/disconnection. Review found three material defects in the earlier implementation: transmission completion was treated as success without checking `is_tx_success`, a timed-out asynchronous TWAI frame could outlive stack storage, and Mode 03 CAN decoding omitted the standard DTC-count byte.

Physical testing also isolated an unresolved transceiver-path problem: direct GPIO loopback passed but CRX stayed recessive with the SN65HVD230 path. This blocks honest vehicle acceptance but does not block simulation, parser, state-machine and build validation.

## Phase 3: design

- One gateway source is selected at build time: simulation or vehicle.
- `INFO` makes source, ECU and clear capability explicit; reconnect must match the original identity.
- CAN API calls have one owner task and transmit storage remains valid until the callback completes.
- ISO-TP uses one configured 11-bit physical ECU, an absolute deadline, bounded payloads and sequence validation.
- Clear is a transaction: scan → durable snapshot → confirmation → one write → rescan. Unknown outcomes retain the old snapshot.
- Live warnings require a sustained candidate state and use hysteresis on recovery. Only transitions are logged.

## Phase 4: implementation

Firmware adds corrected TWAI completion/error handling, Mode 01/03/04, bounded ISO-TP, strict serial parsing and safe build profiles. The WPF application adds correlated requests, bounded framing, source-labelled simulation, real DTC scan/knowledge lookup, safe clearing, live policy evaluation, SQLite audit history and reconnect identity checks.

## Phase 5: verification

Automated host tests cover malformed input, line framing, DTC parsing, warning confirmation/hysteresis, clear ordering, audit failure, uncertain post-clear state, ISO-TP filtering/sequence and duplicate clear suppression. CI builds the .NET core checks, Windows WPF application and ESP-IDF simulation, vehicle-safe and vehicle-clear compile profiles.

Physical acceptance remains open. The exact gate and test record are maintained in [`../verification/week-4-verification.md`](../verification/week-4-verification.md). A green CI run is not evidence that the CAN electrical layer or a particular Isuzu ECU has been validated.
