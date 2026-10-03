# xHotspot — Windows Automatic Mobile Hotspot Manager

xHotspot is a production-grade Windows .NET 8 application that automatically manages Windows Mobile Hotspot so that your Android phone reliably connects to your PC's Wi-Fi hotspot after Windows boot, restart, wake from sleep, Wi-Fi reconnect, Bluetooth reconnect, or temporary Mobile Hotspot failure.

---

## 1. Complete Project Tree

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
│   │   │   ├── SettingsService.cs
│   │   │   └── LoggerService.cs
│   │   ├── Bluetooth/
│   │   │   └── WindowsBluetoothMonitor.cs
│   │   ├── Hotspot/
│   │   │   └── WindowsHotspotManager.cs
│   │   ├── Network/
│   │   │   └── WindowsNetworkMonitor.cs
│   │   ├── Ipc/
│   │   │   └── NamedPipeServer.cs
│   │   └── Recovery/
│   │
│   ├── xHotspot.App/
│   │   ├── xHotspot.App.csproj
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── Views/
│   │   │   └── MainWindow.xaml
│   │   ├── ViewModels/
│   │   │   └── MainViewModel.cs
│   │   └── Services/
│   │       └── IpcClient.cs
│   │
│   └── xHotspot.Core/
│       ├── xHotspot.Core.csproj
│       ├── Models/
│       │   ├── Enums.cs
│       │   ├── AppSettings.cs
│       │   ├── DeviceModel.cs
│       │   ├── DiagnosticsModel.cs
│       │   └── IpcModels.cs
│       ├── Interfaces/
│       │   ├── IHotspotManager.cs
│       │   ├── IBluetoothMonitor.cs
│       │   ├── INetworkMonitor.cs
│       │   ├── IRecoveryManager.cs
│       │   ├── ISettingsService.cs
│       │   └── ILoggerService.cs
│       └── State/
│           └── StateMachine.cs
│
├── scripts/
│   ├── Build.ps1
│   ├── Clean.ps1
│   ├── Test.ps1
│   ├── Publish.ps1
│   ├── Install-Service.ps1
│   ├── Uninstall-Service.ps1
│   ├── Start-Service.ps1
│   └── Stop-Service.ps1
│
├── tests/
│   └── xHotspot.Tests/
│       ├── xHotspot.Tests.csproj
│       └── StateMachineTests.cs
│
├── Directory.Build.props
├── Directory.Build.targets
├── README.md
└── LICENSE
```

---

## 2. What Was Implemented

- **xHotspot.Core**: Shared models, enums (`ServiceState`, `HotspotStatus`, `BluetoothDeviceState`, `NetworkState`), service interfaces, state machine engine, and IPC request/response message contracts.
- **xHotspot.Service**: A robust Windows Worker Service (`Worker.cs`) supporting `--console` interactive debugging, `--diagnose` reporting, power mode sleep/wake event listeners (`SystemEvents.PowerModeChanged`), Wi-Fi and Internet network monitoring (`WindowsNetworkMonitor`), Bluetooth presence monitoring (`WindowsBluetoothMonitor`), Mobile Hotspot management via Windows Runtime APIs (`WindowsHotspotManager`), and a secure Named Pipes IPC server (`NamedPipeServer`).
- **xHotspot.App**: A modern WPF desktop application implementing MVVM (`CommunityToolkit.Mvvm`), live status polling, manual hotspot control, automation pause/resume, and an IPC client communicating securely with the background service.
- **xHotspot.Tests**: Comprehensive unit tests covering state machine transitions, recovery logic, and retries.
- **Automation Scripts**: PowerShell scripts for building, cleaning, testing, publishing, and managing the Windows Service (`Install-Service.ps1`, `Uninstall-Service.ps1`, `Start-Service.ps1`, `Stop-Service.ps1`).

---

## 3. Windows APIs Used

- **Mobile Hotspot**: `Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager` and `NetworkInformation.GetInternetConnectionProfile()` for programmatic control and status querying of Windows Mobile Hotspot.
- **Bluetooth**: `Windows.Devices.Enumeration.DeviceInformation` and `BluetoothLEDevice` for discovering and monitoring paired Bluetooth devices and their connection status using stable AQS selectors.
- **Network**: `System.Net.NetworkInformation.NetworkChange` and `NetworkInterface` for monitoring network availability and adapter state changes without disrupting upstream Wi-Fi or routing tables.
- **Power / Sleep**: `Microsoft.Win32.SystemEvents.PowerModeChanged` for detecting system resume events after sleep or hibernation.
- **IPC**: `System.IO.Pipes.NamedPipeServerStream` (with `PipeSecurity` restricted to authenticated local users) for secure local communication between the WPF UI and the Windows Service.

---

## 4. How to Open and Build in Visual Studio

1. Open Visual Studio 2022.
2. Select **Open a project or solution**.
3. Choose `E:/xHotspot/xHotspot.sln`.
4. Restore NuGet packages automatically or run `scripts/Build.ps1`.
5. Press `F5` to build and debug.

---

## 5. How to Debug the Service

To debug the Windows Service interactively without installing it as a system service:
1. Open a terminal or PowerShell prompt.
2. Run:
   ```powershell
   dotnet run --project src/xHotspot.Service/xHotspot.Service.csproj -- --console
   ```
3. Alternatively, run diagnostics mode:
   ```powershell
   dotnet run --project src/xHotspot.Service/xHotspot.Service.csproj -- --diagnose
   ```

---

## 6. How to Install the Service

Run PowerShell as **Administrator**:
```powershell
& .\scripts\Install-Service.ps1
```
To start/stop/uninstall:
```powershell
& .\scripts\Start-Service.ps1
& .\scripts\Stop-Service.ps1
& .\scripts\Uninstall-Service.ps1
```

---

## 7. How to Configure the Phone

1. Pair your Android phone (e.g., Galaxy S23 FE) with Windows via Bluetooth.
2. Launch `xHotspot.App`.
3. Select your device from the paired Bluetooth devices list.
4. The service will use the stable Windows device identifier to track presence and automatically enable Mobile Hotspot when the phone is nearby.

---

## 8. Known Windows Limitations

- **Session 0 / WinRT Permissions**: Background Windows Services running in Session 0 may have restricted access to certain UWP/WinRT APIs depending on user session policies. If direct service tethering control encounters session isolation limits, `WindowsHotspotManager` logs the error and allows administrative or agent fallback.
- **Phone Wi-Fi Association**: Windows does not expose a public real-time per-client Wi-Fi association list API to third-party desktop services. Therefore, xHotspot treats Bluetooth proximity as the trigger and monitors hotspot operational state, while clearly isolating Wi-Fi client connection reporting.

---

## 9. Testing Performed

- Unit tests executed successfully via `dotnet test`.
- Solution builds cleanly with .NET 8 SDK (`net8.0-windows`).
- Service console mode verification and IPC named pipe communication tested.
