# dingoConfig - Claude Code Implementation Specification

## Project Overview

Build a desktop application using ASP.NET Core backend with React frontend for reading CAN data from multiple devices and configuring Dingo Electronics devices (dingoPDM, dingoPDM-Max, CANBoard). The application runs locally with no internet connectivity required.

## Project Structure

```
dingoConfig/
├── src/
│   ├── Backend/
│   │   ├── dingoConfig.API/
│   │   │   ├── Controllers/
│   │   │   ├── Hubs/
│   │   │   ├── Models/
│   │   │   ├── Services/
│   │   │   └── Program.cs
│   │   ├── dingoConfig.Core/
│   │   │   ├── Models/
│   │   │   ├── Interfaces/
│   │   │   └── Services/
│   │   ├── dingoConfig.Hardware/
│   │   │   ├── CAN/
│   │   │   ├── USB/
│   │   │   └── Communication/
│   │   └── dingoConfig.Tests/
│   │       ├── Unit/
│   │       └── Integration/
│   ├── Frontend/ (START AFTER BACKEND IS WORKING)
│   │   ├── src/
│   │   │   ├── components/
│   │   │   ├── pages/
│   │   │   ├── services/
│   │   │   └── types/
│   │   └── package.json
│   └── Shared/
│       └── Models/
├── catalogs/
├── configs/
└── test-data/
```

## Implementation Plan

### Phase 1: Backend Foundation

**Task 1.1: Project Setup**
- Create ASP.NET Core Web API solution with Core, API, Hardware, and Tests projects
- Add essential NuGet packages:
   - Microsoft.AspNetCore.SignalR
   - Microsoft.Extensions.Hosting
   - System.IO.Ports
   - Newtonsoft.Json
   - xUnit and Moq (for testing)

**Task 1.2: Core Models**
Create essential models in dingoConfig.Core:
```csharp
public class Device
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public List<Parameter> Parameters { get; set; }
    public CommunicationSettings Communication { get; set; }
}

public class DeviceConfiguration
{
    public string DeviceId { get; set; }
    public Dictionary<string, object> Settings { get; set; }
    public DateTime LastModified { get; set; }
}

public class CANMessage
{
    public uint Id { get; set; }
    public byte[] Data { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Task 1.3: File Storage Services**
Implement JSON-based storage:
- `ICatalogService` - Load device catalog files
- `IConfigurationService` - Save/load configurations
- Basic error handling for file operations

### Phase 2: Hardware Communication

**Task 2.1: Communication Interfaces**
Create hardware communication abstractions:
```csharp
public interface ICANCommunicator
{
    Task<bool> ConnectAsync(string connectionString);
    Task DisconnectAsync();
    Task SendMessageAsync(CANMessage message);
    event EventHandler<CANMessage> MessageReceived;
}
```

**Task 2.2: USB Communication Implementation**
- Implement Peak PCAN USB communication
- Implement USB CDC (SLCAN) communication
- Create mock communicator for testing without hardware
- Basic device discovery and connection management

**Task 2.3: Background Service**
Create hosted service for:
- Managing device connections
- Processing CAN messages
- Broadcasting data via SignalR

### Phase 3: Web API and SignalR

**Task 3.1: SignalR Hub**
```csharp
public class DeviceDataHub : Hub
{
    public async Task JoinDeviceGroup(string deviceId);
    public async Task LeaveDeviceGroup(string deviceId);
}
```

**Task 3.2: API Controllers**
Create controllers for:
- Device management (connect/disconnect/status)
- Configuration management (save/load/list)
- Catalog management (load catalogs)

**Task 3.3: Basic Testing**
Write tests for:
- Core services functionality
- API endpoints with mock data
- SignalR hub operations
- Hardware communication with mocks

### Phase 4: Backend Integration and Validation

**Task 4.1: End-to-End Backend Testing**
- Test complete data flow: hardware → service → SignalR
- Verify API endpoints work with real data
- Test error handling and recovery
- Performance check with multiple mock devices

**Task 4.2: Sample Data and Documentation**
- Create sample catalog files
- Create sample configurations
- Document API endpoints
- Create basic setup instructions

**GATE: Backend must be working and tested before frontend development**

### Phase 5: React Frontend

**Task 5.1: React Setup**
- Initialize React TypeScript project
- Install Material-UI, SignalR client, charting library
- Set up API service layer
- Configure TypeScript interfaces matching backend

**Task 5.2: Core Components**
- Device connection interface
- Configuration editor (form-based)
- Real-time data display (charts/tables)
- File save/load operations

**Task 5.3: Integration**
- Connect frontend to backend APIs
- Implement SignalR real-time updates
- Add error handling and loading states
- Basic responsive design

### Phase 6: Final Integration and Packaging

**Task 6.1: Application Packaging**
- Configure single executable deployment
- Embed React build in ASP.NET Core
- Set up automatic browser launch
- Test deployment on clean system

**Task 6.2: Final Testing**
- End-to-end testing with frontend and backend
- Test with mock hardware scenarios
- Verify file operations work correctly
- Performance and stability testing

## Key Implementation Notes

### Testing Approach
- **Pragmatic testing**: Focus on critical paths and error scenarios
- **Mock hardware**: Create mock implementations for development/testing
- **Integration tests**: Test complete workflows, not just individual units
- **Backend validation**: Ensure backend works before starting frontend

### Architecture Principles
- **Clean separation**: Core business logic separate from API and hardware concerns
- **Dependency injection**: Use ASP.NET Core DI for service management
- **Error handling**: Graceful degradation and user-friendly error messages
- **File operations**: Atomic operations and proper error recovery

### Hardware Communication
- **Abstract interfaces**: Support multiple hardware types through common interfaces
- **Mock support**: Enable development and testing without physical hardware
- **Connection management**: Handle device discovery, connection, and disconnection
- **Error resilience**: Recover from hardware communication failures

### SignalR Implementation
- **Grouped connections**: Organize clients by device for efficient broadcasting
- **Connection lifecycle**: Handle client connect/disconnect properly
- **Message filtering**: Only send relevant data to interested clients

## Success Criteria

### Backend Completion (Required before frontend)
- All API endpoints functional and tested
- Hardware communication working with mocks
- SignalR real-time updates working
- File operations reliable
- Basic error handling in place
- Performance acceptable for target use cases

### Final Application
- Connect to multiple devices (mocked initially)
- Load device catalogs from JSON files
- Configure devices through web interface
- Display real-time data
- Save/load configurations
- Single executable deployment
- No internet connectivity required

This specification provides a clear, practical roadmap for Claude Code to build the dingoConfig application with appropriate testing and a backend-first approach, without excessive complexity or rigid TDD requirements.