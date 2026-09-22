# Isuzu Diagnostic System

ESP32 + CAN/OBD-II gateway and a Windows WPF diagnostic application. The MVP can run end-to-end against a clearly labelled simulation source; the vehicle profile implements physically addressed 11-bit ISO 15765 requests for one selected engine ECU.

> Safety status: simulation and host tests are implemented. Vehicle transmit/receive and Mode 04 are **not yet proven on a vehicle** because the current SN65HVD230 bench setup does not reproduce TX on CRX. Do not treat this prototype as a service tool until the hardware gate in [`docs/verification/week-4-verification.md`](docs/verification/week-4-verification.md) passes.

## MVP capabilities

- Explicit `SIMULATION` / `VEHICLE` source identity during handshake
- Mode 01: RPM, coolant temperature, vehicle speed, calculated load, module voltage
- Mode 03: bounded ISO-TP receive and counted stored-DTC decoding
- Safe Mode 04 workflow: fresh scan → durable snapshot → confirmation → one clear request → rescan
- DTC knowledge lookup with a safe `Unknown DTC` fallback
- Live-data policy selection, two-second confirmation, hysteresis and transition logging
- Request-ID correlation, bounded serial framing, watchdog and reconnect identity checks
- SQLite diagnostic event history
- Host regression tests and ESP-IDF simulation/vehicle build profiles

## Quick demo (no vehicle)

Requirements: Windows 10/11 and .NET 10 SDK.

```powershell
git clone https://github.com/Kerem-Erden/isuzu-diagnostic-system.git
cd isuzu-diagnostic-system
dotnet run --project desktop-app/IsuzuDiagnostic.Desktop/IsuzuDiagnostic.Desktop/IsuzuDiagnostic.Desktop.csproj
```

Select `DEMO (no vehicle)`, create a vehicle profile and connect. The title and every diagnostic screen identify the source as simulation. Read DTCs, open the unknown `P3FFF`, inspect related live data, then clear: the simulation intentionally returns persistent `P0087` after the rescan.

Full setup, firmware profiles and the demo recording script are in [`docs/setup-and-demo.md`](docs/setup-and-demo.md).

## Hardware wiring

| SN65HVD230 | ESP32 |
|---|---|
| CTX | GPIO21 |
| CRX | GPIO22 |
| 3V3 | 3V3 |
| GND | GND |

CANH/CANL connect to the vehicle only for an authorized, fused and supervised test. Disconnect the vehicle before enabling the bench GPIO/transceiver test. Verify module voltage, ground, continuity and termination with appropriate instruments.

## Build and test

```bash
tests/firmware/run.sh
dotnet run --project tests/desktop/IsuzuDiagnostic.Core.Tests.csproj -c Release
```

ESP-IDF 6.0.2:

```bash
cd firmware/esp32-gateway
idf.py -D SDKCONFIG=sdkconfig.local -D SDKCONFIG_DEFAULTS=sdkconfig.defaults.simulation build flash monitor
```

The checked-in CI also compiles the WPF application on Windows and both firmware profiles. Vehicle Mode 04 is disabled by default in menuconfig; enable it only after the verification gate and use the explicit physical ECU ID.

## Architecture

```text
Vehicle ECU ─ CAN ─ SN65HVD230 ─ TWAI ─ ESP32 ─ USB serial ─ WPF app ─ SQLite
                                  └── simulation source ────────────┘
```

The serial contract is documented in [`docs/architecture/serial-protocol.md`](docs/architecture/serial-protocol.md). Scope is in [`docs/requirements/mvp-scope.md`](docs/requirements/mvp-scope.md).

## Supported boundary

This MVP supports a configured 11-bit OBD/ISO-TP engine ECU request ID (`0x7E0`–`0x7E7`, response ID + 8). It does not claim J1939, 29-bit addressing, automatic ECU discovery, manufacturer programming, ABS/airbag coverage or professional IDSS compatibility.
