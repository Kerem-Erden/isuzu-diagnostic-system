# Setup, profiles and demo

## Desktop

Install the .NET 10 SDK on Windows 10/11. The application is WPF and is not expected to run on Linux/macOS.

```powershell
dotnet restore desktop-app/IsuzuDiagnostic.Desktop/IsuzuDiagnostic.Desktop/IsuzuDiagnostic.Desktop.csproj
dotnet run --project desktop-app/IsuzuDiagnostic.Desktop/IsuzuDiagnostic.Desktop/IsuzuDiagnostic.Desktop.csproj
```

The knowledge database is copied into the output automatically. On startup the application checks that required tables exist and stops with a clear error if the database is missing or malformed. Session/audit data is stored under `%LOCALAPPDATA%\IsuzuDiagnostic`.

## Firmware profiles

Use ESP-IDF 6.0.2 and an ESP32 target.

Simulation, safe default:

```bash
cd firmware/esp32-gateway
idf.py -D SDKCONFIG=sdkconfig.local -D SDKCONFIG_DEFAULTS=sdkconfig.defaults.simulation build flash monitor
```

Vehicle profile (Mode 04 remains a deliberate build/menuconfig choice):

```bash
idf.py -D SDKCONFIG=sdkconfig.vehicle -D SDKCONFIG_DEFAULTS=sdkconfig.defaults.vehicle menuconfig
idf.py -D SDKCONFIG=sdkconfig.vehicle build flash monitor
```

Before vehicle use, set the bitrate and physical engine ECU request ID. The implementation accepts request IDs `0x7E0`–`0x7E7` and expects the response at request + 8. Do not guess this on a customer vehicle.

## Repeatable MVP demo

1. Start the WPF application and select `DEMO (no vehicle)`.
2. Create/select an Isuzu vehicle profile and connect.
3. Show the `SIMULATION / DEMO` source banner.
4. Open Live Data. Wait through the normal → warning → critical → recovery cycle; show that a one-sample spike does not transition immediately.
5. Open DTCs. Show `P1093`, `P0087` and the safe unknown-code fallback `P3FFF`.
6. Open a known DTC and its related live-data view.
7. Choose Clear. Show the simulation warning, confirm once and show the automatic rescan returning persistent `P0087`.
8. Open diagnostic history and show the pre-clear snapshot, clear acknowledgement, rescan and warning transitions.

This script is the demo-video shot list. No prerecorded video is checked in; recording the UI requires a Windows graphical session. A video made from simulation must retain the source banner and be labelled **SIMULATION — NO VEHICLE**.

## Tests

```bash
tests/firmware/run.sh
dotnet run --project tests/desktop/IsuzuDiagnostic.Core.Tests.csproj -c Release
```

CI builds the WPF project on Windows and ESP-IDF simulation/vehicle profiles. Host tests do not emulate CAN electrical behavior or prove ECU compatibility.
