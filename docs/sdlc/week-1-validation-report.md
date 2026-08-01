# Week 1 Validation Report

**Project:** Isuzu Diagnostic System  
**Date:** 2026-08-02  
**Milestone:** Initial ESP32-to-Desktop Communication Prototype  
**Status:** Passed

## 1. Validation Scope

This report documents the validation of the first-week MVP prototype.

The tested system consists of:

- ESP32 gateway firmware developed with ESP-IDF
- USB serial communication through the CH340 interface
- Windows desktop application developed with C# and WPF
- Line-based serial protocol
- Simulated vehicle live-data messages

The current live-data values are simulated and are not yet read from a vehicle CAN bus.


## 2. Validated Communication Flow

```text
ESP32 firmware
    ↓
USB / CH340 serial interface
    ↓
Windows COM port
    ↓
System.IO.Ports SerialPort
    ↓
Receive buffer
    ↓
Line extraction
    ↓
Protocol parser
    ↓
WPF live-data display


## 3. Validated Protocol Messages

The desktop application successfully received and processed:

SYS:READY
LIVE:RPM:<value>
LIVE:COOLANT_TEMP:<value>
LIVE:BATTERY_VOLTAGE:<value>

Example:

LIVE:RPM:750
LIVE:COOLANT_TEMP:86
LIVE:BATTERY_VOLTAGE:13.8


## 4. Test Results
Test	                    Expected Result	                                                        Result
ESP-IDF baseline build	    Firmware builds successfully	                                        Passed
Firmware flash	            Firmware is written to the ESP32	                                    Passed
Serial output	            ESP32 messages appear on the serial monitor	                            Passed
COM port discovery	        Available Windows serial ports are listed	                            Passed
Desktop connection	        Application connects to the selected COM port	                        Passed
Raw serial display	        Incoming serial text is displayed	                                    Passed
Partial-message buffering	Incomplete serial fragments are preserved until a newline arrives	    Passed
Protocol parsing	        RPM, coolant temperature, and battery voltage are parsed	            Passed
Live-data display	        Parsed values update the corresponding WPF controls	                    Passed
Manual disconnect	        Serial port closes and live values are cleared	                        Passed
Reconnection	            Application reconnects after disconnecting	                            Passed
USB removal detection	    Physical device removal is detected	                                    Passed
USB reconnection	        Device can be refreshed and reconnected	                                Passed
Window-close cleanup	    Closing the application releases the COM port	                        Passed
Raw-output limit	        Old raw log content is removed after the configured limit	            Passed
Receive-buffer limit	    An oversized incomplete message cannot grow the buffer indefinitely	    Passed


## 5. Reliability Controls Added

The desktop application currently includes:

Safe COM port opening and disposal
Serial event unsubscription
UI-thread dispatching
Partial serial-message buffering
Line-based message extraction
Culture-independent number parsing
Invalid-message rejection
Raw-output character limit
Incomplete-message buffer limit
Physical COM device removal monitoring
Live-value reset after disconnection


## 6. Current Limitations

The prototype currently has the following limitations:

Vehicle data is simulated by the ESP32 firmware.
CAN and OBD-II communication are not implemented yet.
DTC reading and clearing are not implemented.
Desktop-to-ESP32 commands are not implemented.
The serial protocol does not yet include checksums or message sequence numbers.
Raw serial output is intended only for development and diagnostics.
Persistent logging and database storage are not implemented.


##7. Validation Result

The first-week prototype satisfies its intended milestone:

The ESP32 firmware and the Windows desktop application can communicate through USB serial, and simulated live-data messages can be safely received, parsed, and displayed.
