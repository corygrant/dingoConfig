# dingoConfig - Technical Specification

**Version**: 1.0  
**Last Updated**: October 24, 2025  
**Purpose**: Technical specification for Claude Code implementation

---

## Project Overview

dingoConfig is a .NET web application for managing dingo CAN devices (dingoPDM, dingoPDM-Max, CANBoard) through various CAN adapters (USB, SLCAN, PCAN, Simulated). The application provides:

- Real-time monitoring of cyclic CAN data
- Device configuration management (read/write ~100 parameters per device)
- Short-term charting (last 1 hour, in-memory circular buffers)
- Message logging to CSV files with automatic rotation
- Web-based dashboard with SignalR real-time updates
- Runtime CAN adapter selection

---

## System Requirements

- **Device Types**: dingoPDM, dingoPDM-Max, CANBoard
- **Adapter Types**: USB, SLCAN, PCAN, Simulated (selected at runtime)
- **CAN Message Rate**: 1,000-3,000 messages/second
- **Cyclic Parameters**: ~10 parameters per device (real-time monitoring)
- **Configuration Parameters**: ~90 parameters per device (on-demand read/write via CAN)
- **Web Clients**: Single concurrent user (MVP)
- **UI Update Rate**: 20 Hz (50ms intervals via SignalR)
- **Charting**: Short-term only (last 1 hour, stored in circular buffers)
- **Logging**: CAN messages logged to CSV files with 7-day retention

---

## Architecture Decisions

### Pattern Selection (Mainstream .NET)

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Architecture** | Clean Architecture + Service Layer | Clear separation of concerns |
| **Commands/Queries** | MediatR | Standard request/response pattern |
| **Validation** | FluentValidation | Industry-standard validation |
| **Mapping** | AutoMapper | DTO mapping |
| **Logging** | Serilog | Structured logging |
| **Real-time Web** | SignalR | Standard for real-time updates |
| **High-perf Async** | System.Threading.Channels | Fast message processing (3000 msg/s) |
| **Events** | Event Aggregator (custom) | Simple pub/sub for internal events |
| **API Style** | Controllers | Better organization than Minimal APIs |
| **Configuration** | Options Pattern | Strong typing for settings |

### What We're NOT Using

- ❌ **CQRS** - Overkill for simple read/write operations
- ❌ **Rx.NET** - Too niche; using event aggregator instead
- ❌ **Database** - Not needed for MVP; using JSON + CSV + in-memory
- ❌ **Device Catalogs** - MVP uses concrete device classes; catalogs later if needed
- ❌ **Minimal APIs** - Controllers better for multiple device types

### Data Storage Strategy

```
├── Device Configurations → JSON files (configs/active/)
├── Real-time State → In-memory (device properties)
├── Short-term Charts → Circular buffers in-memory (1 hour)
└── Message Logs → CSV files (logs/) with daily rotation
```

**No Database Required for MVP**

---

## Folder Structure

```
dingoConfig/
├── api/                                     # REST API, SignalR, Web UI
│   ├── Controllers/
│   │   ├── DingoPDMController.cs
│   │   ├── DingoPDMMaxController.cs
│   │   ├── CANBoardController.cs
│   │   ├── ChartsController.cs
│   │   ├── ConfigurationController.cs
│   │   └── AdapterController.cs           # Runtime adapter selection
│   ├── Realtime/
│   │   ├── Hubs/
│   │   │   └── CanDataHub.cs
│   │   └── Services/
│   │       └── SignalRBroadcastService.cs
│   ├── HealthChecks/
│   │   ├── CanAdapterHealthCheck.cs
│   │   └── DeviceHealthCheck.cs
│   ├── Middleware/
│   │   ├── ErrorHandlingMiddleware.cs
│   │   └── RequestLoggingMiddleware.cs
│   ├── wwwroot/
│   │   ├── index.html
│   │   ├── css/
│   │   ├── js/
│   │   └── pages/
│   │       ├── pdm-dashboard.html
│   │       ├── pdm-max-dashboard.html
│   │       └── canboard-dashboard.html
│   ├── Program.cs
│   └── appsettings.json
│
├── application/                             # Business Logic
│   ├── Commands/
│   │   ├── ReadDeviceConfigCommand.cs
│   │   ├── WriteDeviceConfigCommand.cs
│   │   └── InitializeDeviceCommand.cs
│   ├── Queries/
│   │   ├── GetDeviceStateQuery.cs
│   │   ├── GetAllDevicesQuery.cs
│   │   └── GetChartDataQuery.cs
│   ├── Handlers/
│   │   ├── Commands/
│   │   └── Queries/
│   ├── Services/
│   │   ├── DeviceManager.cs              # Manages all device instances
│   │   └── ConfigurationService.cs
│   ├── Mappings/
│   │   └── DeviceMappingProfile.cs
│   └── Validators/
│       ├── DingoPDMConfigValidator.cs
│       └── DingoPDMMaxConfigValidator.cs
│
├── contracts/                               # DTOs & API Contracts
│   ├── DingoPDM/
│   │   ├── DingoPDMStateDto.cs
│   │   └── DingoPDMConfigDto.cs
│   ├── DingoPDMMax/
│   │   ├── DingoPDMMaxStateDto.cs
│   │   └── DingoPDMMaxConfigDto.cs
│   ├── CANBoard/
│   │   ├── CANBoardStateDto.cs
│   │   └── CANBoardConfigDto.cs
│   ├── Common/
│   │   ├── ChartDataDto.cs
│   │   └── DeviceInfoDto.cs
│   └── Responses/
│       ├── ApiResponse.cs
│       └── ErrorResponse.cs
│
├── domain/                                  # Core Domain Models & Interfaces
│   ├── Interfaces/
│   │   ├── ICommsAdapter.cs              # Adapter interface
│   │   ├── IDevice.cs                    # Device interface
│   │   ├── ICommsAdapterManager.cs       # Runtime adapter selection
│   │   └── IEventAggregator.cs
│   ├── Models/
│   │   ├── CanData.cs
│   │   ├── DeviceState.cs
│   │   ├── DataPoint.cs
│   │   └── CircularBuffer.cs             # For charting
│   ├── Enums/
│   │   ├── DeviceStatus.cs
│   │   └── TransmitPriority.cs
│   ├── Events/
│   │   ├── CanDataEventArgs.cs
│   │   └── CyclicDataReceivedEventArgs.cs
│   ├── Exceptions/
│   │   ├── CanInterfaceException.cs
│   │   ├── DeviceNotFoundException.cs
│   │   └── ConfigurationException.cs
│   └── Devices/                          # Concrete Device Classes
│       ├── DingoPDMDevice.cs
│       ├── DingoPDMMaxDevice.cs
│       └── CANBoardDevice.cs
│
├── infrastructure/                          # Implementation Details
│   ├── Comms/
│   │   ├── Adapters/                     # Physical CAN adapters
│   │   │   ├── UsbAdapter.cs
│   │   │   ├── SlcanAdapter.cs
│   │   │   ├── PcanAdapter.cs
│   │   │   └── SimAdapter.cs
│   │   ├── CommsAdapterManager.cs        # Runtime adapter selection
│   │   └── Common/
│   │       ├── CanBitRates.cs
│   │       └── CanFrameParser.cs
│   ├── Persistence/
│   │   ├── JsonConfigurationRepository.cs
│   │   └── ConfigurationOptions.cs
│   ├── Logging/
│   │   ├── CsvMessageLogger.cs           # Logs to CSV files
│   │   └── LogCleanupService.cs          # Deletes old logs
│   ├── BackgroundServices/
│   │   └── CommsDataPipeline.cs          # Bidirectional TX/RX pipeline
│   └── Events/
│       └── EventAggregator.cs
│
├── tests/
│   ├── api.tests/
│   ├── application.tests/
│   ├── domain.tests/
│   └── infrastructure.tests/
│
├── configs/                                 # Device configurations (JSON)
│   ├── active/
│   ├── backup/
│   └── templates/
│
└── logs/                                    # CSV message logs
    ├── canlog_2025-10-24.csv
    └── ...
```

---

## Layer Responsibilities

### api/ - Presentation Layer
- ASP.NET Core controllers (device-specific endpoints)
- SignalR hubs for real-time updates
- Web UI (HTML, CSS, JS)
- Health checks
- Middleware

**Dependencies**: `application`, `contracts`, `infrastructure`

### application/ - Business Logic Layer
- MediatR commands/queries/handlers
- Business services (DeviceManager, ConfigurationService)
- FluentValidation validators
- AutoMapper profiles

**Dependencies**: `domain`, `contracts`

### contracts/ - Data Transfer Objects
- DTOs for API requests/responses
- No logic, pure data structures
- No dependencies

### domain/ - Core Domain Layer
- Domain interfaces (ICommsAdapter, IDevice, ICommsAdapterManager)
- Core models (CanData, CircularBuffer, DataPoint)
- Concrete device implementations (DingoPDMDevice, etc.)
- Domain events and exceptions
- **No dependencies** (core domain)

### infrastructure/ - Implementation Details
- CAN adapter implementations (USB, SLCAN, PCAN, Simulated)
- CommsAdapterManager (runtime adapter selection)
- CommsDataPipeline (bidirectional TX/RX with request/response tracking)
- JSON configuration persistence
- CSV logging
- Background services

**Dependencies**: `domain`

---

## Core Components

### 1. CommsAdapterManager (Runtime Adapter Selection)

**Purpose**: Allows user to select and connect to CAN adapter at runtime (not compile-time).

**Location**: `infrastructure/Comms/CommsAdapterManager.cs`

**Key Features**:
- Manages current active adapter
- Forwards DataReceived events from adapter
- Handles connect/disconnect lifecycle
- Enables hot-swapping adapters

```csharp
public interface ICommsAdapterManager
{
    ICommsAdapter? ActiveAdapter { get; }
    bool IsConnected { get; }

    Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate, CancellationToken ct = default);
    Task DisconnectAsync();

    event EventHandler<CanDataEventArgs>? DataReceived;
}
```

**Usage Flow**:
1. User selects adapter type in UI (USB, SLCAN, PCAN, Simulated)
2. Frontend calls `POST /api/adapter/connect` with adapter type
3. Controller resolves adapter from DI and calls `ConnectAsync()`
4. CommsDataPipeline receives data via manager's DataReceived event

### 2. CommsDataPipeline (Bidirectional TX/RX)

**Purpose**: Processes all CAN bus communication with request/response tracking and automatic retry.

**Location**: `infrastructure/BackgroundServices/CommsDataPipeline.cs`

**Architecture**:
```
RX Pipeline:
Adapter → Manager → RX Channel → Match Response → Complete Request
                              → Forward to Devices (cyclic data)

TX Pipeline:
Request → TX Channel (priority queue) → Send via Adapter
                                      → Track in ConcurrentDictionary
                                      → Monitor Timeouts
                                      → Auto-retry on timeout
```

**Key Features**:
- **Bidirectional channels**: Separate RX and TX channels
- **Priority handling**: High priority for config/commands, normal for cyclic
- **Request/response tracking**: `ConcurrentDictionary<RequestKey, PendingRequest>`
- **Automatic retry**: Configurable retry count and timeout
- **Timeout monitoring**: Background task watches for timeouts and retries

**RequestKey Structure**:
```csharp
public struct RequestKey
{
    public int BaseId { get; }   // Device base CAN ID
    public int Prefix { get; }   // Command prefix
    public int Index { get; }    // Parameter index
}
```

**Public API**:
```csharp
// Send request and wait for response with automatic retry
Task<CanData> SendRequestAsync(
    CanData requestFrame,
    RequestKey requestKey,
    TimeSpan? timeout = null,
    int maxRetries = 3,
    TransmitPriority priority = TransmitPriority.High,
    CancellationToken ct = default);

// Fire-and-forget (no response expected)
void QueueTransmit(CanData data);
```

### 3. Device Classes (Concrete Implementations)

**Location**: `domain/Devices/`

Each device type has a concrete class with:
- **Cyclic properties**: Real-time values updated from CAN frames
- **Configuration properties**: Settings read/written via CAN
- **Circular buffers**: For charting (1 hour of history per parameter)
- **Event raising**: `CyclicDataReceived` event for SignalR

**Example Structure**:
```csharp
public class DingoPDMDevice : IDevice
{
    // Current values (updated in real-time from CAN)
    public double BatteryVoltage { get; private set; }
    public double[] OutputCurrents { get; private set; } // 8 channels
    public int Temperature { get; private set; }

    // Configuration (read/written on-demand via CAN)
    public int[] CurrentLimits { get; set; }
    public bool[] ChannelEnabled { get; set; }

    // History buffers for charting (last 1 hour at 10 Hz)
    public CircularBuffer<DataPoint> VoltageHistory { get; }
    public CircularBuffer<DataPoint>[] CurrentHistory { get; }

    // Events
    public event EventHandler<CyclicDataReceivedEventArgs>? CyclicDataReceived;

    // Methods
    public Task InitializeAsync(CancellationToken ct);
    public Task<DingoPDMConfig> ReadConfigurationAsync(CancellationToken ct);
    public Task WriteConfigurationAsync(DingoPDMConfig config, CancellationToken ct);
    public DataPoint[] GetVoltageHistory(TimeSpan? duration = null);
}
```

**Data Parsing**:
- Subscribe to adapter's `DataReceived` event
- Check if data belongs to this device
- Parse data based on device protocol
- Update properties
- Add to circular buffers (throttled to 10 Hz)
- Raise `CyclicDataReceived` event

### 4. CircularBuffer<T> (For Charting)

**Location**: `domain/Models/CircularBuffer.cs`

**Purpose**: Store fixed-size history (last 1 hour) without memory growth

**Capacity**:
- 1 hour at 10 Hz = 36,000 samples
- Memory per parameter: ~576 KB

**Features**:
- Thread-safe (lock-based)
- Fixed capacity
- Automatic overwrite of oldest data
- `ToArray()` method for retrieving history

### 5. DeviceManager

**Location**: `application/Services/DeviceManager.cs`

**Purpose**: Central registry of all device instances

```csharp
public class DeviceManager
{
    private readonly Dictionary<string, DingoPDMDevice> _pdmDevices;
    private readonly Dictionary<string, DingoPDMMaxDevice> _pdmMaxDevices;
    private readonly Dictionary<string, CANBoardDevice> _canBoards;
    
    public async Task InitializeAsync();
    public DingoPDMDevice GetPDM(string deviceId);
    public IEnumerable<DingoPDMDevice> GetAllPDMs();
    // ... etc for other device types
}
```

**Initialization**: Creates known device instances at startup

### 6. SignalR Broadcasting

**Location**: `api/Realtime/Services/SignalRBroadcastService.cs`

**Purpose**: Broadcast device state updates to web clients at 20 Hz

**Flow**:
```
Device.CyclicDataReceived event
    ↓
SignalRBroadcastService subscribes
    ↓
Throttle to 20 Hz (50ms intervals)
    ↓
Broadcast via SignalR to all clients
```

**Messages**:
- `InitialPDMStates` - Sent on client connect
- `PDMStateUpdate` - Sent every 50ms with all device states

### 7. CSV Message Logger

**Location**: `infrastructure/Logging/CsvMessageLogger.cs`

**Purpose**: Log all CAN messages to CSV files for debugging/analysis

**Features**:
- Batch writing (1000 messages at a time)
- Daily file rotation (`canlog_YYYY-MM-DD.csv`)
- 7-day retention (automatic cleanup)
- Non-blocking (uses Channel buffer)

**Format**:
```csv
Timestamp,CanId,IsExtended,DataLength,Data
2025-10-24T10:30:15.123Z,0x100,false,8,1234567890ABCDEF
```

---

## Data Flows

### 1. Cyclic Data Flow (Real-time Monitoring)

```
CAN Adapter (3000 msg/s)
    ↓
CommsAdapterManager.DataReceived event
    ↓
CommsDataPipeline.OnDataReceived()
    ↓
Writes to RX Channel
    ↓
ProcessRxPipelineAsync() reads from channel
    ↓
NOT a response → Forward to devices
    ↓
Device.Read() (subscribed to adapter)
    ↓
Parse data → Update properties → Add to CircularBuffer
    ↓
Raise CyclicDataReceived event
    ↓
SignalRBroadcastService (throttled to 20 Hz)
    ↓
SignalR Hub broadcasts to web clients
```

**Key**: No MediatR in this path (too slow for 3000 msg/s)

### 2. Configuration Read/Write Flow

```
User clicks "Read Config" in UI
    ↓
Frontend: GET /api/pdm/{id}/config
    ↓
DingoPDMController.GetConfig()
    ↓
MediatR: ReadPDMConfigCommand
    ↓
ReadPDMConfigHandler.Handle()
    ↓
DingoPDMDevice.ReadConfigurationAsync()
    ↓
For each parameter:
    ↓
    Build CAN request data
    ↓
    CommsDataPipeline.SendRequestAsync()
        - Creates RequestKey
        - Tracks in ConcurrentDictionary
        - Sends data via adapter
        - Waits for response (with timeout/retry)
    ↓
    Response matched in ProcessRxPipelineAsync()
    ↓
    TaskCompletionSource signaled
    ↓
    Parse response value
    ↓
Return full config to handler
    ↓
Map to DTO via AutoMapper
    ↓
Return to controller → JSON response to browser
```

**Similar flow for WriteConfig but with validation via FluentValidation**

### 3. Chart Data Flow

```
User requests chart for last 5 minutes
    ↓
Frontend: GET /api/charts/pdm/{id}/voltage?minutes=5
    ↓
ChartsController.GetVoltageChart()
    ↓
MediatR: GetChartDataQuery
    ↓
Handler: DeviceManager.GetPDM(id)
    ↓
DingoPDMDevice.GetVoltageHistory(TimeSpan.FromMinutes(5))
    ↓
CircularBuffer.ToArray() → Filter by time range
    ↓
Map to ChartDataDto
    ↓
Return to controller → JSON response
    ↓
Browser renders with Chart.js
```

### 4. Runtime Adapter Selection Flow

```
User selects "PCAN" in UI and clicks Connect
    ↓
Frontend: POST /api/adapter/connect { adapterType: "PCAN" }
    ↓
AdapterController.Connect()
    ↓
Resolve PcanAdapter from DI
    ↓
CommsAdapterManager.ConnectAsync(pcanAdapter, port, bitRate)
    ↓
Manager subscribes to adapter.DataReceived
    ↓
Manager calls adapter.InitAsync() then adapter.StartAsync()
    ↓
Adapter connects to hardware
    ↓
CommsDataPipeline starts receiving data via manager
    ↓
All devices start updating
```

---

## Request/Response Tracking Details

### Problem
CAN is a broadcast bus. When you send a configuration read request, you must match the response that comes back (potentially milliseconds later) to the original request.

### Solution
Use `ConcurrentDictionary<RequestKey, PendingRequest>` to track pending requests.

**RequestKey**: Uniquely identifies a request
```csharp
public struct RequestKey
{
    public int BaseId { get; }   // Device CAN ID
    public int Prefix { get; }   // Command type (e.g., 0x40 = read, 0x23 = write)
    public int Index { get; }    // Parameter index
}
```

**PendingRequest**: Tracks a request awaiting response
```csharp
public class PendingRequest
{
    public RequestKey Key { get; }
    public CanData Data { get; }                    // Original request data
    public TaskCompletionSource<CanData> CompletionSource { get; }
    public DateTime TransmitTime { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; } = 3;
    public TimeSpan Timeout { get; } = 500ms;
    public CanData? ResponseData { get; set; }
}
```

### Flow

**Transmit**:
1. Create RequestKey from request parameters
2. Create PendingRequest with TaskCompletionSource
3. Add to ConcurrentDictionary
4. Send data via adapter
5. Return Task from TaskCompletionSource (caller awaits)

**Receive**:
1. Data arrives via RX pipeline
2. Try to extract RequestKey from data (protocol-specific)
3. Look up in ConcurrentDictionary
4. If found: Remove from dictionary, set ResponseData, signal TaskCompletionSource
5. If not found: Must be cyclic data, forward to devices

**Timeout Monitor** (background task):
1. Every 100ms, check all pending requests
2. If elapsed > timeout:
   - If retryCount < maxRetries: Increment retry, resend data
   - Else: Remove from dictionary, signal TaskCompletionSource with TimeoutException

---

## Key Interfaces

### ICommsAdapter (Adapter Interface)

```csharp
public delegate void DataReceivedHandler(object sender, CanDataEventArgs e);

public interface ICommsAdapter
{
    Task<(bool success, string? error)> InitAsync(string port, CanBitRate bitRate, CancellationToken ct);
    Task<(bool success, string? error)> StartAsync(CancellationToken ct);
    Task<(bool success, string? error)> StopAsync();
    Task<(bool success, string? error)> WriteAsync(CanData data, CancellationToken ct);

    DataReceivedHandler DataReceived { get; set; }

    TimeSpan RxTimeDelta();
    bool IsConnected { get; }
}
```

Implemented by: `UsbAdapter`, `SlcanAdapter`, `PcanAdapter`, `SimAdapter`

### IDevice (Device Interface)

```csharp
public interface IDevice
{
    Guid Id { get; }
    string Name { get; set; }
    int BaseId { get; set; }
    bool Connected { get; set; }
    TimeSpan LastRxTime { get; set; }

    void UpdateConnected();
    void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceResponse> queue);
    void Clear();
    bool InIdRange(int id);
    List<DeviceResponse> GetUploadMsgs();
    List<DeviceResponse> GetDownloadMsgs();
    List<DeviceResponse> GetUpdateMsgs(int newId);
    DeviceResponse GetBurnMsg();
    DeviceResponse GetSleepMsg();
    DeviceResponse GetVersionMsg();
}
```

Implemented by: `DingoPDMDevice`, `DingoPDMMaxDevice`, `CANBoardDevice`

### ICommsAdapterManager (Runtime Adapter Selection)

```csharp
public interface ICommsAdapterManager
{
    ICommsAdapter? ActiveAdapter { get; }
    bool IsConnected { get; }

    Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate, CancellationToken ct = default);
    Task DisconnectAsync();

    event EventHandler<CanDataEventArgs>? DataReceived;
}
```

Implemented by: `CommsAdapterManager`

---

## NuGet Packages

### api/
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="8.0.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="8.0.0" />
```

### application/
```xml
<PackageReference Include="MediatR" Version="12.2.0" />
<PackageReference Include="AutoMapper" Version="12.0.1" />
<PackageReference Include="FluentValidation" Version="11.9.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

### infrastructure/
```xml
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
<PackageReference Include="System.Threading.Channels" Version="8.0.0" />
<PackageReference Include="CsvHelper" Version="30.0.0" /> <!-- Optional -->
```

### domain/
No external packages (keep clean)

### contracts/
No external packages (pure data structures)

---

## Configuration (appsettings.json)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { 
        "Name": "File", 
        "Args": { 
          "path": "logs/dingoconfig-.log",
          "rollingInterval": "Day"
        } 
      }
    ]
  },
  "DeviceConfigurations": {
    "Directory": "./configs/active",
    "BackupDirectory": "./configs/backup",
    "AutoBackup": true,
    "BackupIntervalMinutes": 60
  },
  "MessageLogging": {
    "Directory": "./logs",
    "RetentionDays": 7,
    "BatchSize": 1000
  },
  "SignalR": {
    "BroadcastIntervalMs": 50,
    "KeepAliveIntervalSeconds": 15
  },
  "CanPipeline": {
    "RxChannelCapacity": 50000,
    "TxChannelCapacity": 10000,
    "TxPriorityChannelCapacity": 1000,
    "DefaultTimeoutMs": 500,
    "DefaultMaxRetries": 3
  }
}
```

---

## Development Phases

### Phase 1: Foundation (Day 1)
- Create folder structure (api, application, contracts, domain, infrastructure)
- Add NuGet packages to each project
- Create core interfaces in domain/
- Set up dependency injection in api/Program.cs

### Phase 2: Core Domain (Day 2)
- Implement CanData, DeviceStatus, CircularBuffer in domain/Models/
- Implement ICommsAdapter, IDevice, ICommsAdapterManager in domain/Interfaces/
- Create SimAdapter in infrastructure/
- Test data generation and events

### Phase 3: CommsAdapterManager + Pipeline (Day 3-4)
- Implement CommsAdapterManager in infrastructure/
- Implement CommsDataPipeline with bidirectional channels
- Add request/response tracking with ConcurrentDictionary
- Add timeout monitoring and retry logic
- Test with SimAdapter

### Phase 4: First Device - dingoPDM (Day 5-6)
- Implement DingoPDMDevice in domain/Devices/
- Add circular buffers for charting
- Implement ReadConfigurationAsync / WriteConfigurationAsync
- Test data parsing and configuration
- Create DTOs in contracts/DingoPDM/

### Phase 5: Application Layer (Day 7)
- Implement DeviceManager in application/Services/
- Add MediatR commands/queries in application/
- Implement handlers in application/Handlers/
- Add FluentValidation validators
- Add AutoMapper profiles

### Phase 6: API Controllers (Day 8)
- Implement DingoPDMController in api/
- Implement AdapterController for runtime selection
- Implement ChartsController for historical data
- Test with Postman/Swagger

### Phase 7: SignalR (Day 9)
- Implement CanDataHub in api/Realtime/
- Implement SignalRBroadcastService
- Test real-time updates with browser

### Phase 8: CSV Logging (Day 10)
- Implement CsvMessageLogger in infrastructure/
- Implement LogCleanupService
- Test file rotation and cleanup

### Phase 9: Frontend (Day 11-13)
- Create HTML dashboards in api/wwwroot/pages/
- Implement SignalR client connection
- Add Chart.js for historical charts
- Add adapter selection UI

### Phase 10: Additional Devices (Day 14-15)
- Implement DingoPDMMaxDevice
- Implement CANBoardDevice
- Add corresponding controllers and DTOs
- Update UI

### Phase 11: Real Adapters (Day 16+)
- Implement PcanAdapter with PCAN SDK
- Implement UsbAdapter
- Implement SlcanAdapter
- Test with real hardware

### Phase 12: Polish & Testing (Day 17-20)
- Add health checks
- Add error handling middleware
- Comprehensive testing
- Documentation
- Performance tuning

---

## Success Criteria

✅ Handle 3,000 CAN messages/second without dropping  
✅ Update web UI at 20 Hz smoothly  
✅ Read/write all ~100 device parameters per device  
✅ Chart last 1 hour of data per parameter  
✅ Log all CAN messages to CSV with rotation  
✅ Runtime adapter selection (USB, SLCAN, PCAN, Simulated)  
✅ Request/response tracking with automatic retry  
✅ Clean architecture with proper separation of concerns  
✅ Comprehensive test coverage (>80%)  
✅ Production-ready error handling and logging  

---

## Important Implementation Notes

### 1. CommsDataPipeline Must Subscribe to Manager, Not Adapter
The pipeline subscribes to `ICommsAdapterManager.DataReceived`, not directly to adapter, because the adapter is selected at runtime.

### 2. Request/Response Matching is Protocol-Specific
The `ExtractRequestKeyFromResponse()` method must be implemented based on your specific CAN protocol. The example code assumes SDO-style responses but should be adapted.

### 3. Circular Buffer Throttling
Devices should throttle circular buffer updates to ~10 Hz (not every CAN message) to save memory while still providing smooth charts.

### 4. SignalR Throttling
SignalR broadcasts are throttled to 20 Hz to avoid overwhelming browsers. Internal processing runs at full speed.

### 5. Channel Capacity Tuning
Channel capacities (50K RX, 10K TX) are starting points. Monitor `FullMode.DropOldest` behavior and adjust if needed.

### 6. CSV Batch Size
CsvMessageLogger writes in batches of 1000 messages. Adjust based on disk I/O performance.

### 7. No Database in MVP
Resist the temptation to add a database. JSON + CSV + in-memory is sufficient for MVP. Add database later only if needed for long-term analytics.

---

## Testing Strategy

### Unit Tests
- CircularBuffer operations
- Request/response key matching
- Device data parsing
- Validators
- AutoMapper profiles

### Integration Tests
- CommsDataPipeline with SimAdapter
- End-to-end device config read/write
- SignalR message broadcasting
- CSV logging and rotation

### Performance Tests
- 3000 msg/s sustained load
- Channel throughput
- Memory usage under load
- SignalR broadcast latency

---

**Status**: Ready for implementation with Claude Code

**Next Step**: Start with Phase 1 - Foundation