# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

dingoConfig is a .NET 9.0 web application for managing dingo CAN devices (dingoPDM, dingoPDM-Max, CANBoard) through various communication adapters (USB, SLCAN, PCAN, Simulated). The system handles real-time CAN data at 1,000-3,000 messages/second, provides device configuration management (~100 parameters per device), and offers web-based monitoring with SignalR real-time updates.

## Build and Development Commands

### Build the solution
```bash
dotnet build dingoConfig.sln
```

### Run the API (web application)
```bash
dotnet run --project api/api.csproj
```

### Build specific projects
```bash
dotnet build api/api.csproj
dotnet build domain/domain.csproj
dotnet build infrastructure/infrastructure.csproj
dotnet build application/application.csproj
dotnet build contracts/contracts.csproj
```

### Run tests (when test projects are added)
```bash
dotnet test
```

### Clean build artifacts
```bash
dotnet clean
```

## Architecture

This project follows **Clean Architecture** with clear separation of concerns across five layers:

### Layer Structure and Dependencies

```
api (Presentation)
├── depends on: application, contracts, infrastructure
├── Controllers for device-specific endpoints
├── Realtime/Hubs/ for SignalR real-time updates
├── Middleware and health checks
└── Web UI (wwwroot/)

application (Business Logic)
├── depends on: domain, contracts
├── MediatR commands/queries (when implemented)
├── Business services (DeviceManager, ConfigurationService)
├── FluentValidation validators (when implemented)
└── AutoMapper profiles (when implemented)

contracts (DTOs)
├── no dependencies
└── Pure data structures for API requests/responses

domain (Core Domain)
├── no dependencies
├── Core interfaces (ICommsAdapter, IDevice, ICommsAdapterManager)
├── Domain models (CanData, DeviceResponse)
├── Device implementations (when added)
├── Domain events and exceptions
└── Enums (CanBitRate, etc.)

infrastructure (Implementation)
├── depends on: domain, application
├── Communication adapter implementations (USB, SLCAN, PCAN, Simulated)
├── CommsAdapterManager (runtime adapter selection)
├── CommsDataPipeline (bidirectional TX/RX with Channels)
├── JSON configuration persistence (when implemented)
├── CSV logging (when implemented)
└── Background services
```

**Key Rule**: Domain layer has NO dependencies. It defines interfaces that infrastructure implements.

## Core Components

### 1. CommsAdapterManager (Runtime Adapter Selection)

**Location**: `infrastructure/Comms/CommsAdapterManager.cs`

The CommsAdapterManager allows users to select and connect to communication adapters at runtime (not compile-time). It manages the active adapter lifecycle, forwards DataReceived events, and enables hot-swapping between different adapter types.

**Key Methods**:
- `ConnectAsync(ICommsAdapter, string port, CanBitRate, CancellationToken)`: Initialize and start an adapter
- `DisconnectAsync()`: Stop and clean up the current adapter
- `DataReceived` event: Fired when data arrives from the active adapter

**Usage Pattern**:
1. User selects adapter type in UI
2. Frontend calls API endpoint with adapter type
3. Controller resolves specific adapter from DI (UsbAdapter, SlcanAdapter, etc.)
4. Controller calls `CommsAdapterManager.ConnectAsync()` with the adapter instance
5. CommsDataPipeline receives data via manager's DataReceived event

### 2. CommsDataPipeline (Bidirectional TX/RX)

**Location**: `infrastructure/BackgroundServices/CommsDataPipeline.cs`

Processes all CAN bus communication using System.Threading.Channels for high-performance async message processing at 3000 msg/s.

**Architecture**:
- **RX Channel**: 50,000 capacity, drops oldest on full, routes incoming CAN data to devices
- **TX Channel**: 10,000 capacity, queues outgoing CAN data for transmission
- Background service that runs both pipelines concurrently

**Key Features**:
- Non-blocking channel-based architecture
- Subscribes to CommsAdapterManager.DataReceived (not directly to adapter)
- Separate RX and TX processing pipelines
- `QueueTransmit(CanData)` method for fire-and-forget transmission

**Important**: The pipeline subscribes to `ICommsAdapterManager.DataReceived`, NOT directly to individual adapters, because adapters are selected at runtime.

### 3. Communication Adapters

**Location**: `infrastructure/Comms/Adapters/`

All adapters implement `ICommsAdapter`:
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

Available adapters: `UsbAdapter`, `SlcanAdapter`, `PcanAdapter`, `SimAdapter`

### 4. Dependency Injection Setup

**Location**: `api/Program.cs`

Adapters are registered as Transient (new instance per request):
```csharp
builder.Services.AddTransient<UsbAdapter>();
builder.Services.AddTransient<SlcanAdapter>();
builder.Services.AddTransient<PcanAdapter>();
builder.Services.AddTransient<SimAdapter>();
```

CommsAdapterManager is Singleton (one instance for the lifetime of the app):
```csharp
builder.Services.AddSingleton<ICommsAdapterManager, CommsAdapterManager>();
```

CommsDataPipeline runs as a hosted background service:
```csharp
builder.Services.AddHostedService<CommsDataPipeline>();
```

## Data Flow Patterns

### Cyclic Data Flow (Real-time Monitoring)
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
Route to devices (to be implemented)
    ↓
Device updates properties and circular buffers
    ↓
SignalR broadcasts to web clients at 20 Hz
```

### Runtime Adapter Selection Flow
```
User selects "PCAN" in UI
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
CommsDataPipeline starts receiving data via manager
```

## Implementation Approach

### Current State (Phase 2-3)
- ✅ Foundation and folder structure complete
- ✅ Core interfaces defined in domain/ (ICommsAdapter, IDevice, ICommsAdapterManager)
- ✅ CommsAdapterManager fully implemented in infrastructure/Comms/
- ✅ CommsDataPipeline with bidirectional channels implemented in infrastructure/BackgroundServices/
- ✅ Adapter stubs created (UsbAdapter, SlcanAdapter, PcanAdapter, SimAdapter)
- ✅ Basic DI setup in api/Program.cs
- ⚠️ Device implementations not yet created
- ⚠️ DeviceManager not yet implemented
- ⚠️ Request/response tracking not yet added to pipeline

### Next Steps
1. **Complete Device Implementations**: Create concrete device classes in `domain/Devices/` (DingoPDMDevice, etc.)
2. **Add Request/Response Tracking**: Enhance CommsDataPipeline with ConcurrentDictionary for configuration read/write
3. **Implement DeviceManager**: Central registry in `application/Services/`
4. **Add MediatR**: Commands/queries/handlers for business operations
5. **Build Controllers**: Device-specific API endpoints
6. **Add SignalR**: Real-time web updates
7. **Implement CSV Logging**: Message logging with rotation

### When Adding New Features

**Device Classes**:
- Place concrete implementations in `domain/Devices/`
- Implement `IDevice` interface
- Include cyclic properties (real-time values), configuration properties, and circular buffers for charting
- Parse incoming CAN data and raise CyclicDataReceived events

**Controllers**:
- Create device-specific controllers in `api/Controllers/`
- Use MediatR for command/query handling
- Return DTOs from `contracts/` layer

**DTOs**:
- Define all API contracts in `contracts/` with appropriate subfolders
- Keep them pure data structures with no logic

**Background Services**:
- Implement as IHostedService
- Register in `api/Program.cs` with `AddHostedService<T>()`

## Key Design Decisions

### Why Channel-based Pipeline?
System.Threading.Channels provides high-performance async message processing needed for 3000 msg/s without blocking. The bounded channels with DropOldest policy ensure the system never blocks the CAN adapter.

### Why Runtime Adapter Selection?
Users need to choose their CAN adapter type at runtime based on available hardware. The CommsAdapterManager abstraction allows hot-swapping adapters without recompiling or restarting the application.

### Why No Database for MVP?
The MVP uses JSON for configuration persistence, in-memory circular buffers for charting (last 1 hour), and CSV files for message logging. This is sufficient for real-time monitoring and avoids database complexity.

### Why Clean Architecture?
Clear separation of concerns makes the codebase maintainable and testable. The domain layer defines interfaces without dependencies, while infrastructure provides implementations. This allows swapping implementations (e.g., different adapters) without changing business logic.

## Configuration

Key settings that will be needed in `appsettings.json` (to be added during implementation):

```json
{
  "CanPipeline": {
    "RxChannelCapacity": 50000,
    "TxChannelCapacity": 10000,
    "DefaultTimeoutMs": 500,
    "DefaultMaxRetries": 3
  },
  "SignalR": {
    "BroadcastIntervalMs": 50,
    "KeepAliveIntervalSeconds": 15
  },
  "MessageLogging": {
    "Directory": "./logs",
    "RetentionDays": 7,
    "BatchSize": 1000
  }
}
```

## Important Notes

1. **CommsDataPipeline subscribes to ICommsAdapterManager, not individual adapters**, because adapters are selected at runtime.

2. **CanData Model**: The codebase uses `CanData` as the core model (with Id, Len, Payload properties) for all CAN bus communication.

3. **Circular Buffer Throttling**: When implemented, devices should throttle circular buffer updates to ~10 Hz (not every CAN message) to save memory.

4. **SignalR Throttling**: SignalR broadcasts should be throttled to 20 Hz to avoid overwhelming browsers while internal processing runs at full speed.

5. **No MediatR in RX Path**: Don't use MediatR for cyclic data processing (too slow for 3000 msg/s). Use events and direct method calls instead.

6. **Channel Capacity Tuning**: The 50K RX and 10K TX capacities are starting points. Monitor BoundedChannelFullMode.DropOldest behavior and adjust if messages are being dropped.

7. **Realtime Folder**: The actual codebase uses `api/Realtime/` for SignalR components, not `api/SignalR/` as might be expected.

8. **DeviceManager Dependency**: The CommsDataPipeline constructor includes a DeviceManager parameter, but device routing is not yet fully implemented. This is part of the next implementation phase where devices will receive and parse messages from the RX channel.

## Reference Documentation

For detailed technical specification including:
- Complete CAN protocol details
- Request/response tracking implementation
- Device-specific parameter mappings
- Phase-by-phase implementation guide
- Success criteria and testing strategy

See: `dingoconfig-spec.md` in the repository root.
