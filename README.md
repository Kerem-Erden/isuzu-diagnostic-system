# Isuzu Diognastic System 

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


Current Status:

Project planning and initial system definition are in progress


Scope:

The first version will focus on isuzu vehicles, engines and diagnostic
parameters. Support for additional vehicle manufacturers may be added in later
versions.
