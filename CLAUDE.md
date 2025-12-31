# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

dingoConfig is a .NET 10.0 Blazor Server application for managing dingo CAN devices (dingoPDM, dingoPDM-Max, CANBoard) through various communication adapters (USB, SLCAN, PCAN, Simulated). The system handles real-time CAN data at 1,000-3,000 messages/second, provides device configuration management (~100 parameters per device), and offers web-based monitoring with timer-based UI updates at 20 Hz.

**Important**: This is a **pure Blazor Server application** using interactive server-side rendering. There are NO REST API controllers - all interactions happen through Blazor components with direct service injection.

## Build and Development Commands

### Build the solution
```bash
dotnet build dingoConfig.sln
```

### Run the application
```bash
dotnet run --project api/api.csproj
```

### Publish self-contained executables
```bash
# Windows/Linux/macOS builds
./publish.sh    # Linux/macOS
./publish.ps1   # Windows
```

### Build specific projects
```bash
dotnet build api/api.csproj
dotnet build domain/domain.csproj
dotnet build infrastructure/infrastructure.csproj
dotnet build application/application.csproj
```

### Clean build artifacts
```bash
dotnet clean
```

## Architecture

This project follows **Clean Architecture** with clear separation of concerns across four active layers (contracts layer is currently empty):

### Layer Structure and Dependencies

```
api (Presentation - Blazor Server)
├── depends on: application, infrastructure
├── Components/
│   ├── Pages/ - 7 pages (Home, Devices, Device, CanLog, GlobalLog, Error, NotFound)
│   ├── Shared/ - Device views (BasePdmDeviceView, PdmDeviceView, PdmMaxDeviceView, CanboardDeviceView)
│   ├── Shared/ - Toolbars (AdapterToolbarControl, FileToolbar)
│   ├── Dialogs/ - 3 dialogs (OpenFileDialog, SaveAsDialog, SettingsDialog)
│   ├── Functions/ - 14 configuration grids for device functions
│   └── Layout/ - MainLayout, NavMenu, ReconnectModal
├── Services/
│   └── NotificationService - Combines Snackbar + GlobalLogger
└── Program.cs - DI setup, middleware configuration

application (Business Logic)
├── depends on: domain
├── Services/
│   ├── DeviceManager - Device lifecycle, request/response tracking
│   ├── ConfigFileManager - JSON persistence, file state management
│   ├── CanMsgLogger - CAN message logging with CSV output
│   └── GlobalLogger - Application-wide logging system
└── Models/
    ├── LogEntry, LogLevel - Global logging models
    ├── CanLogEntry, DataDirection - CAN logging models
    ├── ConfigFile - JSON configuration structure
    └── NumberFormat - Display formatting options

domain (Core Domain)
├── no dependencies
├── Interfaces/
│   ├── IDevice - Device abstraction (Guid-based identity)
│   ├── ICommsAdapter - Communication adapter interface
│   ├── ICommsAdapterManager - Adapter selection/lifecycle
│   └── IDeviceFunction - Function interface
├── Devices/
│   ├── dingoPdm/PdmDevice - FULLY IMPLEMENTED (1,073 lines)
│   ├── dingoPdmMax/PdmMaxDevice - FULLY IMPLEMENTED (inherits PdmDevice)
│   ├── Canboard/CanboardDevice - STUB ONLY
│   └── dingoPdm/Functions/ - 9 function types
├── Models/
│   ├── CanFrame - CAN message (Id, Len, Payload)
│   ├── DeviceCanFrame - Request tracking wrapper
│   └── CanFrameEventArgs - Event args
├── Common/
│   └── DbcSignalCodec - DBC signal encoding/decoding
└── Enums/ - 17 total enums for CAN protocol

infrastructure (Implementation)
├── depends on: domain, application
├── Adapters/
│   ├── UsbAdapter - Serial SLCAN adapter (FULL)
│   ├── SlcanAdapter - SLCAN protocol (FULL)
│   ├── PcanAdapter - Peak PCAN hardware (FULL)
│   └── SimAdapter - Simulation adapter (STUB)
├── Comms/
│   └── CommsAdapterManager - Runtime adapter selection
├── BackgroundServices/
│   └── CommsDataPipeline - Bidirectional TX/RX with Channels
└── Logging/
    └── GlobalLoggerProvider - ILoggerProvider bridge to GlobalLogger

contracts (DTOs)
└── EMPTY - No DTOs currently used (Blazor uses domain models directly)
```

**Key Principles**:
- Domain layer has NO dependencies
- No REST API - pure Blazor Server architecture
- Direct service injection in components (no controller layer)
- Timer-based UI updates (20 Hz polling)
- JSON-based configuration persistence

## Core Components

### 1. CommsAdapterManager (Runtime Adapter Selection)

**Location**: `infrastructure/Comms/CommsAdapterManager.cs`

Allows runtime selection and hot-swapping of communication adapters without recompiling. Manages adapter lifecycle and forwards DataReceived events to subscribers.

**Key Methods**:
- `ConnectAsync(ICommsAdapter, string port, CanBitRate, CancellationToken)`: Initialize and start adapter
- `DisconnectAsync()`: Stop and cleanup current adapter
- `GetAvailablePorts()`: Enumerate serial ports for USB/SLCAN
- Events: `DataReceived`, `Connected`

**Available Adapters**:
- UsbAdapter - Serial port (115200 baud, SLCAN protocol)
- SlcanAdapter - SLCAN protocol
- PcanAdapter - Peak PCAN hardware (Peak.PCANBasic.NET v4.10.0.964)
- SimAdapter - Simulation (stub only)

**Usage Pattern**:
```csharp
// In Blazor component
@inject ICommsAdapterManager AdapterManager
@inject UsbAdapter UsbAdapter

private async Task ConnectAsync()
{
    var (success, error) = await AdapterManager.ConnectAsync(
        UsbAdapter,
        selectedPort,
        CanBitRate.BitRate500K,
        CancellationToken.None
    );
}
```

### 2. CommsDataPipeline (Bidirectional TX/RX)

**Location**: `infrastructure/BackgroundServices/CommsDataPipeline.cs`

Processes all CAN communication using System.Threading.Channels for high-performance async processing at 3000 msg/s.

**Architecture**:
- **RX Channel**: 10,000 capacity, drops oldest on full
- **TX Channel**: 10,000 capacity, drops oldest on full
- Runs as IHostedService (background service)
- Non-blocking channel-based architecture

**Data Flow**:
1. Subscribes to `ICommsAdapterManager.DataReceived`
2. Writes incoming frames to RX channel
3. Reads from RX channel and routes to `DeviceManager.OnCanDataReceived()`
4. DeviceManager queues TX messages via `SetTransmitCallback()`
5. TX channel feeds messages to adapter via `WriteAsync()`

**Integration with DeviceManager**:
```csharp
// In CommsDataPipeline.StartAsync()
_deviceManager.SetTransmitCallback(frame =>
    _txChannel.Writer.TryWrite(frame)
);
```

**Important**: Pipeline subscribes to `ICommsAdapterManager.DataReceived`, NOT directly to adapters (adapters are selected at runtime).

### 3. DeviceManager (Device Lifecycle & Request/Response Tracking)

**Location**: `application/Services/DeviceManager.cs`

Central service for device management and CAN communication coordination. Handles device creation, polymorphic operations, and request/response tracking with timeout/retry logic.

**Key Responsibilities**:
- **Device Registry**: `Dictionary<Guid, IDevice>` - all devices keyed by Guid
- **Request Queue**: `ConcurrentDictionary<(BaseId, Prefix, Index), DeviceCanFrame>` - pending messages
- **Timeout Management**: 500ms timeout, max 20 retries
- **Polymorphic Operations**: Calls interface methods uniformly (no type checking)

**Key Methods**:
- `AddDevice(string deviceType, string name, int baseId)`: Create and register device
- `RemoveDevice(Guid id)`: Remove device from registry
- `GetDevice(Guid id)` / `GetDevice<T>(Guid id)`: Retrieve devices
- `GetAllDevices()`: Get all devices (for UI display)
- `OnCanDataReceived(CanFrame frame)`: Route incoming CAN data to devices
- `SetTransmitCallback(Action<CanFrame>)`: Connect to TX channel
- Device operations: `UploadConfig()`, `DownloadConfig()`, `BurnSettings()`, `Sleep()`, `RequestVersion()`

**Request/Response Tracking**:
- **Queue Key**: `(BaseId, Prefix, Index)` uniquely identifies pending messages
- **Timeout**: 500ms per attempt
- **Retries**: Max 20 attempts (not 3 as originally planned)
- **Response Handling**: Devices call `queue.TryRemove()` in their `Read()` method
- **Concurrency**: Thread-safe via ConcurrentDictionary

**Design Pattern**:
All devices implement the full `IDevice` interface. DeviceManager calls methods polymorphically without runtime type checks. Devices provide no-op implementations for unused functionality.

### 4. ConfigFileManager (JSON Persistence)

**Location**: `application/Services/ConfigFileManager.cs`

Manages configuration persistence to JSON files with type-safe device serialization.

**Features**:
- **Working Directory**: Default `~/Documents/dingoConfig` (configurable)
- **File Format**: JSON with separate lists per device type
- **State Tracking**: Current filename, unsaved changes flag
- **Event System**: `StateChanged` event for UI updates

**JSON Structure**:
```json
{
  "PdmDevices": [...],
  "PdmMaxDevices": [...],
  "CanboardDevices": [...]
}
```

**Why Separate Lists**: Preserves polymorphic properties during serialization (e.g., PdmMaxDevice has 4 outputs vs PdmDevice's 8).

**File Operations**:
- `NewFile()`: Clear all devices
- `OpenFile(string path)`: Load from JSON
- `SaveFile(string path)`: Save to JSON
- `Initialize()`: Create working directory

### 5. GlobalLogger (Application-Wide Logging)

**Location**: `application/Services/GlobalLogger.cs`

**NEW FEATURE** - Comprehensive logging system integrated with ASP.NET Core logging pipeline.

**Features**:
- **In-Memory Buffer**: 50,000 entries (drop-oldest policy)
- **CSV File Output**: Optional, single file per session
- **Log Levels**: Debug, Info, Warning, Error
- **Filtering**: By level, source, and category
- **UI Viewer**: `/global-log` page with MudDataGrid

**Integration**:
```csharp
// In Program.cs
builder.Services.AddSingleton<GlobalLogger>();
builder.Logging.Services.AddSingleton<ILoggerProvider>(sp =>
    new GlobalLoggerProvider(sp.GetRequiredService<GlobalLogger>()));
```

**Captures Logs From**:
- ASP.NET Core framework (via ILoggerProvider)
- DeviceManager, ConfigFileManager, CanMsgLogger (via ILogger)
- UI components (via direct GlobalLogger injection)

**CSV Format**: `Timestamp,Level,Source,Category,Message,Exception`

### 6. NotificationService (Unified Snackbar + Logging)

**Location**: `api/Services/NotificationService.cs`

**NEW FEATURE** - Convenience service that combines MudBlazor Snackbar notifications with GlobalLogger logging in a single call.

**Usage**:
```csharp
@inject NotificationService Notification

Notification.NewSuccess("Configuration saved!");
Notification.NewError("Failed to connect", exception);
Notification.NewWarning("Connection lost");
Notification.NewInfo("Processing...", logOnly: true); // Log without snackbar
```

**Methods**:
- `NewInfo(string message, bool logOnly = false)`
- `NewSuccess(string message, bool logOnly = false)`
- `NewWarning(string message, bool logOnly = false)`
- `NewError(string message, Exception? exception = null, bool logOnly = false)`

**Benefits**:
- Single method call for both UI feedback and logging
- All user-facing messages automatically logged
- Consistent source ("UI") and category ("Notification") in logs

### 7. CanMsgLogger (CAN Message Logging)

**Location**: `application/Services/CanMsgLogger.cs`

Tracks and logs CAN messages with summary and full history modes.

**Features**:
- **Message Summary**: One entry per CAN ID (O(1) lookups via ConcurrentDictionary)
- **Full History**: Last 100,000 messages
- **CSV Output**: Configurable format (hex/decimal)
- **Direction Tracking**: RX/TX
- **Auto-Refresh UI**: 20 Hz timer-based updates

**Number Formats**:
- CAN ID: Hex or Decimal
- Payload: Hex or Decimal
- Configurable via UI dropdown

## Dependency Injection Setup

**Location**: `api/Program.cs`

### Service Lifetimes

**Singleton** (one instance for app lifetime):
```csharp
builder.Services.AddSingleton<ICommsAdapterManager, CommsAdapterManager>();
builder.Services.AddSingleton<DeviceManager>();
builder.Services.AddSingleton<ConfigFileManager>();
builder.Services.AddSingleton<CanMsgLogger>();
builder.Services.AddSingleton<GlobalLogger>();
```

**Transient** (new instance per resolution):
```csharp
builder.Services.AddTransient<UsbAdapter>();
builder.Services.AddTransient<SlcanAdapter>();
builder.Services.AddTransient<PcanAdapter>();
builder.Services.AddTransient<SimAdapter>();
```

**Scoped** (one instance per circuit/user):
```csharp
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<HttpClient>(...);
```

**Hosted Service** (background service):
```csharp
builder.Services.AddHostedService<CommsDataPipeline>();
```

**Logging Provider** (factory pattern):
```csharp
builder.Logging.Services.AddSingleton<ILoggerProvider>(sp =>
    new GlobalLoggerProvider(sp.GetRequiredService<GlobalLogger>()));
```

### Why Transient Adapters?

Adapters are transient because only ONE is active at a time (selected via CommsAdapterManager). Each connection resolves a fresh adapter instance, preventing state leakage between connections.

## Data Flow Patterns

### RX Pipeline (Receive CAN Data)
```
CAN Adapter (3000 msg/s)
    ↓
CommsAdapterManager.DataReceived event
    ↓
CommsDataPipeline.OnDataReceived() writes to RX Channel (10K capacity)
    ↓
ProcessRxPipelineAsync() reads from channel
    ↓
CanMsgLogger.LogMessage() (summary + history)
    ↓
DeviceManager.OnCanDataReceived(frame)
    ↓
Find device by BaseId range
    ↓
Device.Read(frame, ref queue) - parses data, updates state
    ↓
If response message: removes from _requestQueue
    ↓
Device updates cyclic/config properties
    ↓
UI components poll device state at 20 Hz via Timer
    ↓
StateHasChanged() triggers Blazor re-render
```

### TX Pipeline (Send CAN Messages)
```
Component calls DeviceManager operation (e.g., BurnSettings)
    ↓
DeviceManager creates DeviceCanFrame(s) with timer
    ↓
Message added to _requestQueue with key (BaseId, Prefix, Index)
    ↓
Timer.Start() - 500ms timeout
    ↓
_transmitCallback(frame) writes to TX Channel
    ↓
CommsDataPipeline.ProcessTxPipelineAsync() reads from channel
    ↓
Adapter.WriteAsync(frame) transmits over CAN
    ↓
Device responds with matching Prefix
    ↓
Response arrives → RX Pipeline → Device.Read() calls queue.TryRemove()
    ↓
Timer cancelled, message complete
    ↓
[If timeout] Retry (max 20 attempts) or log error
```

### Runtime Adapter Connection Flow
```
User selects "USB" in AdapterToolbarControl
    ↓
Component calls AdapterManager.ConnectAsync(usbAdapter, port, bitRate)
    ↓
Manager calls adapter.InitAsync() (configure port/bitrate)
    ↓
Manager calls adapter.StartAsync() (begin reading)
    ↓
Manager subscribes to adapter.DataReceived
    ↓
Manager raises Connected event
    ↓
CommsDataPipeline receives data via manager.DataReceived
    ↓
UI polls manager.IsConnected at 20 Hz
```

### UI Update Pattern (Timer-based Polling)

**NOT using SignalR** - Uses Blazor Server's built-in circuit with timer-based polling:

```csharp
// In component
private Timer? _refreshTimer;

protected override void OnInitialized()
{
    _refreshTimer = new Timer(_ =>
    {
        InvokeAsync(() =>
        {
            LoadData();  // Poll service state
            StateHasChanged();  // Trigger re-render
        });
    }, null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
}
```

**Update Frequencies**:
- Device views: 20 Hz (50ms)
- CAN Log: 10 Hz (100ms)
- Global Log: 10 Hz (100ms)
- NavMenu device list: 1 Hz (1000ms)

## Device Implementation

### Implemented Devices

**PdmDevice** (dingoPDM) - FULLY IMPLEMENTED
- 2 digital inputs
- 8 outputs (5A-26A configurable current limits)
- 32 CAN inputs
- 16 virtual inputs
- 4 flashers
- 4 counters
- 32 conditions
- 1 wiper function
- 1 starter disable function
- Complete CAN message parsing (1,073 lines)

**PdmMaxDevice** (dingoPDM-Max) - FULLY IMPLEMENTED
- Inherits from PdmDevice
- 4 outputs (26A fixed current limits)
- All other features same as PdmDevice
- Custom message parsing for Max-specific frames

**CanboardDevice** (CANBoard) - STUB ONLY
- Interface implemented with no-op methods
- Placeholder for future implementation

### Device Functions

**Location**: `domain/Devices/dingoPdm/Functions/`

All functions implement `IDeviceFunction` interface:
- **Input**: Digital input configuration (mode, pull, edge)
- **Output**: Output configuration (current limit, reset mode, flash)
- **CanInput**: CAN input mapping (ID, byte, bit)
- **VirtualInput**: Software-defined inputs
- **Flasher**: Blink pattern configuration
- **Counter**: Event counting with reset
- **Condition**: Conditional logic (operators, conditionals)
- **Wiper**: Wiper control (mode, speed, intervals)
- **StarterDisable**: Starter lockout configuration

### Device Enums

**Location**: `domain/Devices/dingoPdm/Enums/`

**CAN Protocol**:
- MessagePrefix: Config, State, CANIN, etc.
- MessageType: Request, Response
- MessageSrc: Normal, Bootloader

**Device State**:
- DeviceState: Sleep, Awake, Error, etc.
- OutState: Off, On, ShortCircuit, OpenLoad, Overcurrent

**Input Configuration**:
- InputMode: Digital, Frequency, AnalogVoltage, AnalogResistive
- InputEdge: Rising, Falling, Both
- InputPull: None, Pullup, Pulldown

**Wiper Control**:
- WiperMode: Off, Low, High, Int
- WiperState: Park, Moving, Delay
- WiperSpeed: Slow, Fast

**Condition Logic**:
- Operator: Equal, NotEqual, GreaterThan, LessThan, etc.
- Conditional: And, Or
- VarMap: Maps variables to indices

**Output Control**:
- ResetMode: None, Latch, AutoReset

### Adding New Device Types

1. **Create Device Class** in `domain/Devices/{DeviceType}/`
   - Inherit from base or implement `IDevice` directly
   - Implement all interface methods (use no-ops for unused)
   - Parse incoming CAN messages in `Read()` method
   - Create outgoing messages in operation methods

2. **Add to ConfigFile Model** in `application/Models/ConfigFile.cs`
   ```csharp
   public List<NewDeviceType> NewDevices { get; set; } = new();
   ```

3. **Update ConfigFileManager** serialization logic

4. **Create Device View** in `api/Components/Shared/{DeviceName}DeviceView.razor`

5. **Register in DeviceManager** `AddDevice()` switch statement

## UI Component Patterns

### Component Structure

All device views follow this pattern:
```razor
@inject DeviceManager DeviceManager
@inject NotificationService Notification
@rendermode InteractiveServer
@implements IDisposable

<MudContainer>
    <!-- UI elements -->
</MudContainer>

@code {
    [Parameter] public Guid DeviceId { get; set; }
    private IDevice? _device;
    private Timer? _refreshTimer;

    protected override void OnInitialized()
    {
        _device = DeviceManager.GetDevice(DeviceId);
        _refreshTimer = new Timer(_ => {
            InvokeAsync(() => {
                _device = DeviceManager.GetDevice(DeviceId);
                StateHasChanged();
            });
        }, null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
    }

    public void Dispose() => _refreshTimer?.Dispose();
}
```

### Key Patterns

**Timer-based Polling**: Use `Timer` with `InvokeAsync` and `StateHasChanged()`

**Service Injection**: Inject services directly (no controller layer)

**MudBlazor Components**: Use MudDataGrid, MudButton, MudTextField, etc.

**Notifications**: Use `NotificationService` for user feedback + logging

**Dialogs**: Use `IDialogService` for modals

**File Operations**: Use `ConfigFileManager` with UI event handling

## Important Implementation Notes

### 1. Blazor Server Architecture

This is a **pure Blazor Server application** with NO REST API:
- All user interactions via Blazor components
- Direct service injection (no controller layer)
- Timer-based UI updates (not SignalR)
- Contracts layer is empty (no DTOs needed)

### 2. PCAN Adapter DLC Handling

**Critical**: PCAN library's `msg.Data` always returns 8 bytes regardless of DLC. Must slice payload:
```csharp
var payload = new byte[msg.DLC];
Array.Copy(msg.Data, payload, msg.DLC);
```

This prevents length mismatches when parsing CAN messages with fewer than 8 bytes.

### 3. Request/Response Tracking

**Queue Key**: `(BaseId, Prefix, Index)` uniquely identifies messages
**Timeout**: 500ms per attempt
**Max Retries**: 20 attempts (configurable in DeviceManager)
**Response Handling**: Device `Read()` method calls `queue.TryRemove(key, out _)`

### 4. Device Polymorphism

DeviceManager calls `IDevice` methods uniformly without type checking:
```csharp
// No type checking needed
foreach (var device in _devices.Values)
{
    device.Read(frame, ref _requestQueue);
}
```

Devices implement full interface, provide no-ops for unused methods.

### 5. Channel Capacity

Both RX and TX channels use **10,000 capacity** with `BoundedChannelFullMode.DropOldest`. Monitor for dropped messages if CAN traffic exceeds capacity.

### 6. Circular Buffer Updates

NOT YET IMPLEMENTED - When added, throttle to ~10 Hz (not every message) to conserve memory.

### 7. Configuration Persistence

**Format**: JSON with separate device type lists
**Location**: `~/Documents/dingoConfig/*.json`
**Structure**: `{ "PdmDevices": [...], "PdmMaxDevices": [...] }`
**Why Separate Lists**: Preserves polymorphic properties during serialization

### 8. Logging Architecture

**Two-Tier System**:
1. **Global Application Log**: ASP.NET Core + custom logs → GlobalLogger → UI + CSV
2. **CAN Message Log**: CAN frames → CanMsgLogger → UI + CSV

Both use same UI pattern: MudDataGrid with filters, auto-refresh, CSV export.

### 9. Graceful Shutdown

NavMenu includes shutdown button that calls `IHostApplicationLifetime.StopApplication()` with confirmation dialog. Configured for 5-second shutdown timeout.

### 10. Auto Browser Launch

Non-development builds automatically open browser on startup (localhost:5000). Handles port-in-use errors gracefully by opening browser to existing instance.

### 11. Working Directory

Default: `~/Documents/dingoConfig`
- Auto-created on startup
- Configurable via SettingsDialog
- Stores configuration JSON files
- Stores log CSV files

### 12. DbcSignalCodec Usage

Use for CAN signal encoding/decoding:
```csharp
// Extract signal from payload
var value = DbcSignalCodec.ExtractSignal(
    payload,
    startBit: 0,
    length: 16,
    byteOrder: ByteOrder.LittleEndian,
    isSigned: false,
    factor: 0.1,
    offset: 0
);

// Insert signal into payload
DbcSignalCodec.InsertSignal(
    payload,
    value,
    startBit: 0,
    length: 16,
    byteOrder: ByteOrder.LittleEndian,
    factor: 0.1,
    offset: 0
);
```

## Common Tasks

### Adding a New UI Page

1. Create `api/Components/Pages/PageName.razor`
2. Add `@page "/route"` directive
3. Add navigation link to `NavMenu.razor`
4. Use `@rendermode InteractiveServer` for stateful components
5. Inject required services via `@inject`

### Adding a New Service

1. Create service class in `application/Services/` or `api/Services/`
2. Register in `api/Program.cs` with appropriate lifetime
3. Inject where needed via DI
4. Use `ILogger<T>` for logging (auto-captured by GlobalLogger)

### Adding a New Device Operation

1. Add method to `IDevice` interface
2. Implement in all device classes (use no-op for unsupported)
3. Add operation call to `DeviceManager`
4. Create UI button/handler in device view component
5. Use `NotificationService` for user feedback

### Adding CAN Message Parsing

1. Define message in device class
2. Add parsing logic to `Read()` method
3. Update device properties based on message content
4. Remove from `_requestQueue` if response message:
   ```csharp
   queue.TryRemove((BaseId, prefix, index), out _);
   ```

### Debugging CAN Communication

1. Check Global Log (`/global-log`) for connection/adapter errors
2. Check CAN Log (`/can-log`) for message traffic
3. Monitor Queue count in NavMenu (should return to 0)
4. Check DeviceManager logging for timeout/retry messages
5. Verify adapter RxTimeDelta < 500ms (connection health)

## Key Design Decisions

### Why Blazor Server Instead of REST API?

- Simpler architecture (no DTO mapping layer)
- Direct service access in components
- Real-time updates via timer polling (adequate for 20 Hz refresh)
- Reduced complexity (no API versioning, no AutoMapper, no controllers)

### Why Timer Polling Instead of SignalR?

- Adequate performance for 20 Hz updates
- Simpler code (no hub infrastructure)
- Blazor Server circuit already maintains WebSocket connection
- Fewer moving parts, easier debugging

### Why Channel-based Pipeline?

System.Threading.Channels provides high-performance async processing for 3000 msg/s without blocking. Bounded channels with DropOldest prevent backpressure from blocking CAN adapter.

### Why Runtime Adapter Selection?

Users need hardware flexibility. CommsAdapterManager allows hot-swapping between USB, SLCAN, PCAN, and Sim without recompiling or restarting.

### Why No Database?

JSON files provide adequate persistence for configuration. In-memory state for real-time data. CSV files for message logging. Avoids database complexity for MVP.

### Why Clean Architecture?

Clear separation of concerns. Domain layer has no dependencies. Infrastructure implements domain interfaces. Easy to swap implementations (e.g., different adapters) without changing business logic.

## Reference Documentation

For detailed technical specification including:
- Complete CAN protocol details
- Request/response tracking implementation
- Device-specific parameter mappings
- Phase-by-phase implementation guide
- Success criteria and testing strategy

See: `dingoconfig-spec.md` in the repository root.
