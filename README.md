# xHotspot — Windows Automatic Mobile Hotspot Manager

Build a Windows application from scratch called **xHotspot**.

The purpose of xHotspot is to automatically manage Windows Mobile Hotspot so that my Android phone can reliably connect to my PC's Wi-Fi hotspot after:

* Windows boot
* Windows restart
* Windows wake from sleep
* Wi-Fi reconnect
* Bluetooth reconnect
* temporary Mobile Hotspot failure

The PC itself is connected to the Internet through Wi-Fi.

The phone is an Android phone and is paired with Windows over Bluetooth. Windows Phone Link is also installed, but **Phone Link must NOT be a required dependency**.

The application must use Windows' native networking/Bluetooth capabilities rather than relying on Phone Link.

---

# 1. Development environment

Create a complete **Visual Studio solution** that I can open directly in Visual Studio and build/debug from the IDE.

Use:

* C#
* .NET 8
* Windows 11
* WPF for the desktop/tray UI
* Windows Worker Service / Windows Service architecture for the background service
* MVVM for the WPF UI
* Nullable reference types enabled
* Implicit usings enabled
* Modern SDK-style `.csproj` files

Target:

```text
net8.0-windows
```

The solution must contain all required:

* `.sln`
* `.csproj`
* source files
* app manifest/configuration
* service installation support
* debug configuration
* publish configuration
* README
* build scripts

Do NOT give me a project that requires manually creating Visual Studio projects.

I want to open:

```text
xHotspot.sln
```

in Visual Studio and immediately be able to build and debug it.

---

# 2. Solution architecture

Use this general structure:

```text
xHotspot/
│
├── xHotspot.sln
│
├── src/
│   │
│   ├── xHotspot.Service/
│   │   ├── xHotspot.Service.csproj
│   │   ├── Program.cs
│   │   ├── Worker.cs
│   │   ├── Services/
│   │   ├── Bluetooth/
│   │   ├── Hotspot/
│   │   ├── Network/
│   │   ├── Recovery/
│   │   ├── Configuration/
│   │   └── Logging/
│   │
│   ├── xHotspot.App/
│   │   ├── xHotspot.App.csproj
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Models/
│   │   ├── Services/
│   │   └── Resources/
│   │
│   └── xHotspot.Core/
│       ├── xHotspot.Core.csproj
│       ├── Models/
│       ├── Interfaces/
│       ├── State/
│       └── Configuration/
│
├── installer/
│
├── scripts/
│
├── docs/
│
├── tests/
│   └── xHotspot.Tests/
│
├── Directory.Build.props
├── Directory.Build.targets
├── README.md
└── LICENSE
```

Keep the architecture clean and maintainable.

---

# 3. IMPORTANT: do not fake Windows APIs

Before implementing the actual hotspot functionality, investigate the current Windows 11 APIs available for controlling Mobile Hotspot.

Do NOT assume that an old command such as:

```text
netsh wlan start hostednetwork
```

is appropriate.

Windows Mobile Hotspot and legacy Hosted Network are not necessarily the same thing.

Use the supported Windows networking APIs where possible.

If a Windows API is undocumented or unavailable to third-party applications, clearly isolate that implementation behind an interface.

The application must not pretend something works when Windows does not expose the required functionality.

If administrative privileges are required for a particular operation, document that clearly and design around it appropriately.

---

# 4. Core concept

The application should behave as a **state machine**, not a simple polling loop.

Create a state model such as:

```text
Stopped
Starting
WaitingForNetwork
WaitingForPhone
EnablingHotspot
HotspotRunning
PhoneConnected
Recovering
Error
```

The service should react to events whenever possible.

Polling may be used as a **fallback/recovery mechanism**, but do not implement the entire application as:

```csharp
while (true)
{
    Thread.Sleep(...);
}
```

Use event-driven Windows APIs where possible.

---

# 5. Startup behavior

When Windows starts:

1. Start the xHotspot service automatically.
2. Wait for Windows networking to initialize.
3. Wait for the Wi-Fi adapter to become available.
4. Determine whether the PC has Internet connectivity.
5. Initialize Bluetooth monitoring.
6. Determine whether the configured phone is currently available.
7. If the phone is available, ensure Mobile Hotspot is running.
8. If the phone is not available, remain idle and monitor for it.

Do not continuously enable/disable the hotspot unnecessarily.

---

# 6. Resume-from-sleep behavior

This is extremely important.

When Windows wakes from sleep:

1. Detect resume.
2. Allow a short stabilization period for networking/Bluetooth.
3. Re-check:

   * Wi-Fi adapter
   * Internet connectivity
   * Bluetooth
   * configured phone
   * Mobile Hotspot state
4. Repair anything that did not recover correctly.
5. Enable Mobile Hotspot if the phone is available and the hotspot is off.

The application must be resilient to Windows returning from sleep with networking temporarily unavailable.

---

# 7. Bluetooth phone detection

Allow the user to select a specific paired Bluetooth device.

Example:

```text
Galaxy S23 FE
```

Do NOT simply detect any Bluetooth device.

Store the selected device identifier, preferably using a stable Windows Bluetooth device identifier rather than the friendly name alone.

Friendly name:

```text
Galaxy S23 FE
```

Stable identifier:

```text
<Windows device identifier>
```

The friendly name is for display only.

The service should detect:

```text
Phone became available
Phone became unavailable
Bluetooth adapter unavailable
Bluetooth adapter restored
```

Avoid excessive polling.

---

# 8. Phone detection should NOT depend entirely on Bluetooth

Bluetooth is the primary proximity/presence signal, but it should not be treated as perfect.

Windows can temporarily lose Bluetooth during:

* sleep/resume
* driver restart
* Bluetooth adapter reset
* Windows Update
* airplane mode
* phone Bluetooth changes

Therefore the recovery system must be able to recover from temporary Bluetooth loss.

---

# 9. Mobile Hotspot management

Create an abstraction:

```csharp
public interface IHotspotManager
{
    Task<HotspotStatus> GetStatusAsync();
    Task<bool> EnableAsync();
    Task<bool> DisableAsync();
}
```

Do not tightly couple the entire application to the Windows implementation.

Create a Windows implementation:

```text
WindowsHotspotManager
```

The implementation should:

* determine current Mobile Hotspot state
* enable Mobile Hotspot
* disable Mobile Hotspot if explicitly requested
* detect failures
* report meaningful errors

Do not repeatedly toggle the hotspot.

If it is already running:

```text
Do nothing.
```

---

# 10. Network monitoring

Create:

```text
INetworkMonitor
```

and a Windows implementation.

Monitor:

* Wi-Fi adapter availability
* connected SSID
* Internet connectivity
* network changes
* adapter changes

The PC's Wi-Fi connection is the upstream Internet connection.

The expected topology is:

```text
Internet
    ↓
PC Wi-Fi
    ↓
Windows Mobile Hotspot
    ↓
Android phone
```

The application must never accidentally disconnect the PC's upstream Wi-Fi connection merely to start the hotspot.

---

# 11. Phone Wi-Fi connection detection

Where Windows exposes the information, determine whether the configured phone actually connected to the hotspot.

The state should distinguish:

```text
Phone detected over Bluetooth
```

from:

```text
Phone connected to hotspot Wi-Fi
```

Example:

```text
Bluetooth:
    Connected

Hotspot:
    Running

Phone Wi-Fi:
    Connected
```

---

# 12. Recovery engine

Create a dedicated recovery component.

Example:

```text
IRecoveryManager
RecoveryManager
```

It should handle situations such as:

### Case A

```text
Phone present
Hotspot OFF
Internet ON
```

Action:

```text
Enable hotspot
```

### Case B

```text
Phone present
Hotspot ON
Phone not connected
```

Action:

```text
Wait
```

Do not immediately restart the hotspot.

### Case C

```text
Phone present
Hotspot unexpectedly OFF
```

Action:

```text
Enable hotspot
```

### Case D

```text
PC wakes from sleep
Bluetooth temporarily unavailable
```

Action:

```text
Wait for Bluetooth recovery
```

### Case E

```text
PC has no Internet
```

Action:

```text
Do not repeatedly toggle hotspot.
Wait for network recovery.
```

### Case F

```text
Hotspot operation fails
```

Action:

```text
Record detailed error.
Use controlled retry/backoff.
```

---

# 13. Retry strategy

Do NOT hammer Windows APIs continuously.

Use exponential or staged backoff.

For example:

```text
Attempt 1: immediately
Attempt 2: 5 seconds
Attempt 3: 15 seconds
Attempt 4: 30 seconds
Attempt 5: 60 seconds
```

Then continue at a reasonable interval.

Reset the retry state after successful recovery.

---

# 14. Configuration

Store configuration in a sensible per-machine location.

Configuration should include:

```json
{
  "Enabled": true,
  "AutoStartHotspot": true,
  "StartAfterBoot": true,
  "RecoverAfterSleep": true,
  "RecoverAfterNetworkChange": true,
  "RecoverAfterBluetoothReconnect": true,
  "PhoneDeviceId": "",
  "PhoneName": "",
  "RetryIntervalSeconds": 30,
  "StartupDelaySeconds": 10
}
```

Do not hard-code the phone's identity.

The user must select it from the UI.

---

# 15. WPF user interface

Create a clean modern Windows UI.

Main screen:

```text
xHotspot

Automatic Mobile Hotspot Manager

Service
● Running

Internet
● Connected
Wi-Fi: <SSID>

Phone
Galaxy S23 FE
Bluetooth: Connected

Mobile Hotspot
● Running

Phone Wi-Fi
● Connected
```

Include a clear status indicator.

---

# 16. Settings

Provide:

### General

```text
[x] Enable automatic hotspot management

[x] Start automatically with Windows

[x] Recover after sleep/wake

[x] Recover after network changes

[x] Recover after Bluetooth reconnect
```

### Phone

Show paired Bluetooth devices and allow the user to select one.

Example:

```text
Detected Bluetooth devices

○ Galaxy Watch
● Galaxy S23 FE
○ Other device
```

Use the stable device identifier internally.

### Diagnostics

Display:

```text
Service status
Bluetooth status
Selected phone
Wi-Fi status
Internet status
Hotspot status
Phone connection status
Last recovery
Last error
```

---

# 17. System tray

The WPF application should minimize to the Windows system tray.

Tray menu:

```text
xHotspot

Status: Running

Open xHotspot
Enable Hotspot
Disable Hotspot
Pause Automatic Management
Resume Automatic Management
Settings
View Logs
Exit UI
```

Important:

**Exit UI must NOT stop the Windows service.**

The service must continue running independently.

---

# 18. Windows Service

Install xHotspot as a real Windows service.

Service name:

```text
xHotspot
```

Display name:

```text
xHotspot Automatic Hotspot Manager
```

Startup type:

```text
Automatic
```

Prefer delayed automatic startup if appropriate so Windows networking has time to initialize.

The service must automatically recover from crashes.

Configure Windows service recovery:

```text
First failure  → Restart service
Second failure → Restart service
Subsequent    → Restart service
```

Do not create a service that requires the WPF application to be running.

---

# 19. Logging

Use structured logging.

Log:

* service startup
* service shutdown
* Bluetooth state changes
* selected phone
* Wi-Fi state changes
* Internet state changes
* hotspot state changes
* recovery attempts
* failures
* Windows resume events
* configuration changes

Example:

```text
2026-10-02 19:03:22 INFO  Service started
2026-10-02 19:03:27 INFO  Wi-Fi connected
2026-10-02 19:03:29 INFO  Bluetooth phone detected: Galaxy S23 FE
2026-10-02 19:03:29 INFO  Mobile Hotspot is OFF
2026-10-02 19:03:30 INFO  Enabling Mobile Hotspot
2026-10-02 19:03:32 INFO  Mobile Hotspot enabled
2026-10-02 19:03:37 INFO  Phone connected to hotspot
```

Use rolling logs so they cannot grow indefinitely.

---

# 20. Diagnostics

Provide a diagnostics page capable of showing:

```text
Windows version
.NET version
xHotspot version

Wi-Fi adapter
Wi-Fi state
Wi-Fi SSID
Internet connectivity

Bluetooth adapter
Bluetooth state

Selected phone
Phone Bluetooth state

Mobile Hotspot state

Service state
```

Also provide:

```text
Copy diagnostics
Open log folder
```

---

# 21. Testing architecture

Create interfaces so the state machine can be unit tested without requiring actual Bluetooth or Mobile Hotspot hardware.

Create mock implementations:

```text
MockBluetoothMonitor
MockHotspotManager
MockNetworkMonitor
```

Test scenarios including:

1. PC boots with phone present.
2. PC boots without phone.
3. Phone appears later.
4. Phone disappears.
5. Phone returns.
6. PC wakes from sleep.
7. Wi-Fi temporarily disappears.
8. Internet disappears.
9. Hotspot is already running.
10. Hotspot unexpectedly stops.
11. Hotspot enable fails.
12. Bluetooth temporarily disappears.
13. Service restarts.
14. Multiple recovery events happen close together.

The state machine must not perform duplicate operations when multiple events arrive simultaneously.

---

# 22. Concurrency

This is important.

Bluetooth, networking, Windows resume events, and hotspot state changes can all occur simultaneously.

Prevent race conditions such as:

```text
Bluetooth event
     +
Network event
     +
Resume event
     ↓
three simultaneous EnableHotspot() calls
```

Use an appropriate synchronization mechanism so only one recovery operation can modify hotspot state at a time.

---

# 23. Installation

Create an installation mechanism suitable for a normal Windows application.

The installer must:

1. Install the service.
2. Configure automatic startup.
3. Install the WPF UI.
4. Create Start Menu shortcuts.
5. Optionally create a desktop shortcut.
6. Start the service.
7. Allow uninstall to cleanly remove the service.

Do not require the user to manually run `sc.exe` commands.

Also provide developer scripts for:

```text
Install-Service.ps1
Uninstall-Service.ps1
Start-Service.ps1
Stop-Service.ps1
```

for development/testing.

---

# 24. Visual Studio debugging

This is mandatory.

Configure Visual Studio so I can debug the application normally.

Because Windows Services cannot simply behave like normal WPF applications during debugging, provide a developer/debug mode.

When launched under the Visual Studio debugger, the service host should be capable of running as a normal console process.

For example:

```text
xHotspot.Service.exe --console
```

should run the service logic interactively.

This allows:

```text
Visual Studio
    ↓
Start Debugging
    ↓
xHotspot.Service --console
```

without requiring the service to be installed.

The production installation should run it as a real Windows Service.

---

# 25. Visual Studio solution configuration

Create configurations for:

```text
Debug
Release
```

Set the WPF project as the convenient startup project for UI development.

Provide an easy way to debug the service.

If appropriate, create a solution-level launch configuration or documented startup procedure so I can debug:

```text
xHotspot.App
```

and:

```text
xHotspot.Service --console
```

from Visual Studio.

---

# 26. Build scripts

Create:

```text
Build.ps1
Clean.ps1
Test.ps1
Publish.ps1
Install-Service.ps1
Uninstall-Service.ps1
```

The scripts should:

* validate prerequisites
* restore NuGet packages
* build the solution
* run tests
* publish Release builds
* report errors clearly

Do not hide build errors.

---

# 27. Versioning

Start at:

```text
1.0.0
```

Keep the version centralized so updating it does not require changing it in multiple unrelated files.

Display the version in the UI.

---

# 28. Security

Follow least privilege wherever possible.

Do not run the entire application as Administrator unless Windows requires it.

Do not store passwords.

Do not collect telemetry.

Do not send diagnostics anywhere.

All logging must remain local.

---

# 29. Reliability requirements

The primary goal is reliability.

The application must tolerate:

* Windows boot
* Windows login
* Windows logout
* sleep
* hibernate
* wake
* Wi-Fi reconnect
* Bluetooth reconnect
* Bluetooth adapter restart
* phone leaving range
* phone returning
* temporary Internet outage
* Mobile Hotspot unexpectedly stopping
* service restart
* Windows Update/reboot

The application should favor:

```text
Detect → verify → repair → verify
```

rather than:

```text
Detect → blindly toggle
```

---

# 30. Do not make assumptions about Windows

Before finalizing the implementation:

* Verify the actual Windows 11 APIs available for Mobile Hotspot control.
* Verify Bluetooth device discovery APIs.
* Verify how Mobile Hotspot state can be queried.
* Verify how Windows exposes hotspot-connected clients.
* Verify which operations require elevation.
* Verify behavior after sleep/resume.

If an API has limitations, document the limitation and implement the best supported approach.

Do not substitute legacy `netsh hostednetwork` functionality for Windows Mobile Hotspot unless it is explicitly verified to work on the target Windows 11 configuration.

---

# 31. Final deliverable

The completed repository must be immediately usable.

I should be able to:

1. Open:

```text
xHotspot.sln
```

2. Restore NuGet packages.
3. Build the solution.
4. Run unit tests.
5. Press F5 to debug the WPF UI.
6. Debug the service in console mode.
7. Publish a Release build.
8. Install the Windows service.
9. Reboot Windows.
10. Have xHotspot automatically start.
11. Have it detect my configured phone.
12. Automatically enable Mobile Hotspot.
13. Connect the phone.
14. Recover after sleep/wake.

---

# 32. Agent instructions

Do not stop after creating a skeleton.

Implement the actual working application.

If a Windows API is unavailable or behaves differently than expected, investigate it and adapt the architecture rather than replacing the requested functionality with a fake implementation.

Create all Visual Studio files.

Create all source files.

Create all project files.

Create the unit tests.

Create the build/install scripts.

Create the README.

At the end, provide:

```text
1. Complete project tree
2. What was implemented
3. Windows APIs used
4. How to open/build in Visual Studio
5. How to debug the service
6. How to install the service
7. How to configure the phone
8. Known Windows limitations
9. Testing performed
```

The final project must be a real, buildable Visual Studio solution rather than pseudocode or a conceptual example.

after you're done edit README.md with the project info.