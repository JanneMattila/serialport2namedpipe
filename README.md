# Serial port to named pipe connector

Serial port (e.g., COM1) to named pipe (e.g., \\.\pipe\com1) connector

## Prior art

There are several projects that do similar thing:

[COMpipe](https://github.com/tdhoward/COMpipe)

[Convey](https://github.com/weltling/convey)

However, they do not support running them as Windows Service.

Main use case for this connector is to run
[Home Assistant Operating System](https://www.home-assistant.io/installation/windows)
in Hyper-V VM and connect it to serial devices (e.g., Zigbee dongle) on the host.

## Installation

Create COM port for virtual machine and map that to named pipe:

```powershell
Set-VMComPort -VMName "home assistant" -Number 1 -Path \\.\pipe\com1
```

Validate the mapping:

```powershell
Get-VMComPort -VMName "home assistant"

VMName         Name  Path
------         ----  ----
Home assistant COM 1 \\.\pipe\com1
Home assistant COM 2
```

```powershell
sc.exe create binpath="C:\Path\To\SerialPort2NamedPipeConnector.exe"
```

### Links

[Create a Windows Service using BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
