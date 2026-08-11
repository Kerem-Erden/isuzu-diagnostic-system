# Isuzu Diognostic System 

ESP32 tabanli CAN/OBD-II arac ariza teshis, cozum onerisi sistemi ve Windows uygulamasi.

## Project Objective 

Bu projenin amaci, ilk asamada Isuzu araclardan canlis teshis verilerini 
ve Diagnostic Trouble Code (DTC) bilgilerini okuyabilen 
bir sistem gelistirmektir.

Aractan alinan veriler ESP32 uzerinden Windows masaustu uygulamasina
aktarilacaktir. Uygulama, DTC aciklamalarini, muhtemel nedenlerini, onerilen 
kontrol adimlarini ve ilgili canli verileri kullaniciya sunacaktir.

## Planned Core Features

- ESP32 ile CAN/OBD-II haberlesmesi
- Windows uygulamasi ile USB serial haberlesmesi
- Canli motor verilerinin goruntulenmesi
- DTC kodlarinin okunmasi
- DTC aciklamalarinin ve cozum onerilerinin gosterilmesi
- DTC kayitlarinin silinmesi
- Okunan DTC ve DTC'lerin iliskili oldugu canli verilerin oncelikli gosterilmesi
- Canli verilerin referans degerlerle karsilastirilmasi
- Hysteresis ve sure tabanli uyari yontemi
- Teshis ve uyari gecmisinin yerel olarak saklanmasi

## Initial Technology Stack

### Embedded Firmware

- ESP32
- C
- ESP-IDF
- ESP-IDF TWAI driver
- SN65HVD230 CAN transceiver

### Desktop Application

- C#
- .NET
- Windows desktop user interface
- USB Serial communication 

### Data Storage

- SQLite
- Offline DTC and diagnostic database

## Initial System Architecture

```text
Vehicle ECU
    |
    | CAN / OBD-II
    v
SN65HVD230 CAN Transceiver
    |
    v
ESP32 Gateway
    |
    | USB Serial
    v
Windows Desktop Application
    |
    v
SQLite Diagnostic Database
```

## Current Status

Week 2 desktop application integration has been completed.

Implemented so far:

- ESP32 ↔ Windows USB serial communication
- Request/response protocol with request IDs
- PING/PONG gateway handshake
- START / STOP / STATUS gateway commands
- Live data streaming and parsing
- Vehicle selection and diagnostic session management
- Diagnostic dashboard navigation
- Live Data screen with last-received timestamp
- DTC list and DTC detail screens
- Possible causes, diagnostic steps, solutions and related live-data metadata
- Mock DTC rescan and clear-memory workflow
- Vehicle Information screen
- Developer Console
- Automatic serial-port discovery
- Connection heartbeat/watchdog
- Connection-loss detection
- Automatic gateway reconnection

Real vehicle CAN/OBD-II communication and ECU data reading are not implemented yet.
The current DTC and live-data values are development/mock data used to validate
the desktop and serial communication architecture.

## Scope

Scope:

The first version will focus on isuzu vehicles, engines and diagnostic
parameters. Support for additional vehicle manufacturers may be added in later
versions.
