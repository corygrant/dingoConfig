# dingoConfig - Claude Code Implementation Specification

## Project Overview

Build a desktop application using ASP.NET Core backend with React frontend for reading CAN data from multiple devices and configuring Dingo Electronics devices (dingoPDM, dingoPDM-Max, CANBoard). The application runs locally with no internet connectivity required.

## Note

This application will be developed/tested in Jetbrains Rider

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
│   │   │   ├── Configuration/
│   │   │   └── Program.cs
│   │   ├── dingoConfig.Core/
│   │   │   ├── Models/
│   │   │   ├── Interfaces/
│   │   │   └── Services/
│   │   ├── dingoConfig.Hardware/
│   │   │   ├── CAN/
│   │   │   ├── USB/
│   │   │   └── Communication/
│   │   └── Tests/
│   │       ├── dingoConfig.Core.Tests/
│   │       ├── dingoConfig.Hardware.Tests/
│   │       ├── dingoConfig.API.Tests/
│   │       └── dingoConfig.Integration.Tests/
│   ├── Frontend/ (ONLY AFTER BACKEND PHASE COMPLETE)
│   │   ├── public/
│   │   ├── src/
│   │   │   ├── components/
│   │   │   ├── pages/
│   │   │   ├── services/
│   │   │   ├── types/
│   │   │   └── utils/
│   │   ├── package.json
│   │   └── tsconfig.json
│   └── Shared/
│       └── Models/
├── catalogs/
│   └── (JSON catalog files)
├── configs/
│   └── (JSON configuration files)
├── test-data/
│   └── (Sample catalogs and configs for testing)
└── README.md
```

## Implementation Phases

### Phase 1: Project Setup and Core Infrastructure

**Task 1.1: Initialize ASP.NET Core Solution with Testing Framework**
- Create new ASP.NET Core Web API solution
- Set up multi-project structure (API, Core, Hardware projects)
- **Add comprehensive test projects for each layer**
- Configure project dependencies and NuGet packages:
  - Microsoft.AspNetCore.SignalR
  - Microsoft.Extensions.Hosting
  - System.IO.Ports (for USB communication)
  - Newtonsoft.Json or System.Text.Json
  - Microsoft.Extensions.Logging
  - **Testing packages:**
    - xUnit
    - Microsoft.AspNetCore.Mvc.Testing
    - Moq
    - FluentAssertions
    - Microsoft.Extensions.DependencyInjection.Abstractions

**Task 1.2: Test Data and Mock Setup**
- Create sample catalog JSON files for testing
- Create sample configuration JSON files for testing
- Set up test data constants and builders
- Create mock hardware interfaces for testing without physical devices

**Task 1.3: Configure Build and CI Pipeline**
- Set up solution-wide test execution
- Configure test coverage reporting
- Set up automated testing on build
- Create test result reporting

### Phase 2: Core Domain Models and Services (TDD Approach)

**Task 2.1: Data Models with Unit Tests**
Create models in dingoConfig.Core with comprehensive testing:

```csharp
// First write tests, then implement models
[Test]
public class DeviceTests
{
    [Fact]
    public void Device_ShouldValidateRequiredProperties()
    [Fact]
    public void Device_ShouldSerializeToJsonCorrectly()
    [Fact]
    public void Device_ShouldDeserializeFromJsonCorrectly()
}
```

Implement models:
```csharp
public class Device
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public List<Parameter> Parameters { get; set; }
    public CommunicationSettings Communication { get; set; }
    
    // Add validation methods
    public ValidationResult Validate()
}

public class DeviceConfiguration
{
    public string DeviceId { get; set; }
    public Dictionary<string, object> Settings { get; set; }
    public DateTime LastModified { get; set; }
    
    // Add validation and serialization methods
}

public class CANMessage
{
    public uint Id { get; set; }
    public byte[] Data { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Add validation and parsing methods
}
```

**Task 2.2: File Storage Services with Comprehensive Testing**
Implement and test JSON-based storage services:

```csharp
// Test first approach
[TestClass]
public class CatalogServiceTests
{
    [Test]
    public async Task LoadCatalogAsync_ShouldReturnValidCatalog_WhenFileExists()
    [Test]
    public async Task LoadCatalogAsync_ShouldThrowException_WhenFileNotFound()
    [Test]
    public async Task LoadCatalogAsync_ShouldThrowException_WhenJsonInvalid()
    [Test]
    public async Task ReloadCatalogsAsync_ShouldUpdateCacheCorrectly()
}

[TestClass]
public class ConfigurationServiceTests
{
    [Test]
    public async Task SaveConfigurationAsync_ShouldCreateFile_WhenValidConfiguration()
    [Test]
    public async Task LoadConfigurationAsync_ShouldReturnConfiguration_WhenFileExists()
    [Test]
    public async Task DeleteConfigurationAsync_ShouldRemoveFile_WhenConfigurationExists()
}
```

Then implement services:
- `ICatalogService` with full error handling and validation
- `IConfigurationService` with atomic file operations
- `IFileStorageService` with comprehensive path validation

**Task 2.3: Service Integration Tests**
Create integration tests that verify services work together:
- Test catalog loading with various file formats
- Test configuration save/load roundtrip
- Test error handling and recovery scenarios
- Test concurrent access scenarios

### Phase 3: Hardware Communication Layer (Fully Tested)

**Task 3.1: CAN Communication Interfaces and Tests**
Create abstract interfaces with comprehensive test coverage:

```csharp
[TestClass]
public class CANCommunicatorTests
{
    [Test]
    public async Task ConnectAsync_ShouldReturnTrue_WhenValidConnectionString()
    [Test]
    public async Task SendMessageAsync_ShouldTransmitCorrectly_WhenConnected()
    [Test]
    public async Task MessageReceived_ShouldTriggerEvent_WhenDataReceived()
    [Test]
    public async Task DisconnectAsync_ShouldCleanupResources_WhenConnected()
}

[TestClass]
public class MockCANCommunicatorTests
{
    // Test the mock implementation thoroughly
    [Test]
    public async Task MockCommunicator_ShouldSimulateRealBehavior()
}
```

**Task 3.2: USB Communication Implementation with Mock Testing**
- Implement Peak PCAN USB communication with full test coverage
- Implement USB CDC (SLCAN) communication with mock testing
- Create comprehensive device discovery tests
- Test connection management and error scenarios
- **Create mock implementations for testing without hardware**

**Task 3.3: Background Service Testing**
Create and test hosted service:
```csharp
[TestClass]
public class HardwareCommunicationServiceTests
{
    [Test]
    public async Task StartAsync_ShouldInitializeAllCommunicators()
    [Test]
    public async Task ProcessMessage_ShouldBroadcastViaSignalR()
    [Test]
    public async Task HandleDeviceDisconnection_ShouldUpdateStatus()
    [Test]
    public async Task StopAsync_ShouldCleanupAllResources()
}
```

### Phase 4: Web API with Complete Test Coverage

**Task 4.1: API Controllers with Unit and Integration Tests**

```csharp
[TestClass]
public class DevicesControllerTests
{
    [Test]
    public async Task GetDevices_ShouldReturnAllAvailableDevices()
    [Test]
    public async Task GetDevice_ShouldReturnNotFound_WhenDeviceDoesntExist()
    [Test]
    public async Task ConnectDevice_ShouldReturnOk_WhenConnectionSuccessful()
    [Test]
    public async Task ConnectDevice_ShouldReturnBadRequest_WhenAlreadyConnected()
}

[TestClass]
public class ConfigurationControllerTests
{
    [Test]
    public async Task SaveConfiguration_ShouldReturnCreated_WhenValidData()
    [Test]
    public async Task LoadConfiguration_ShouldReturnConfiguration_WhenExists()
    [Test]
    public async Task DeleteConfiguration_ShouldReturnNoContent_WhenDeleted()
}
```

**Task 4.2: SignalR Hub Testing**
```csharp
[TestClass]
public class DeviceDataHubTests
{
    [Test]
    public async Task JoinDeviceGroup_ShouldAddToGroup()
    [Test]
    public async Task BroadcastDeviceData_ShouldOnlySendToGroupMembers()
    [Test]
    public async Task HandleDisconnection_ShouldRemoveFromAllGroups()
}
```

**Task 4.3: API Integration Tests**
Create full end-to-end API tests:
- Test complete workflows (connect device → configure → receive data)
- Test error scenarios and recovery
- Test concurrent API usage
- Performance testing for real-time data streaming

### Phase 5: Backend System Integration and Performance Testing

**Task 5.1: End-to-End Backend Testing**
- Test complete system with mock hardware
- Verify data flow from hardware → service → SignalR → clients
- Test system under load (multiple devices, high data rates)
- Memory leak and resource usage testing

**Task 5.2: Error Handling and Resilience Testing**
- Test hardware disconnection scenarios
- Test file system errors (permissions, disk full, etc.)
- Test invalid catalog/configuration file handling
- Test system recovery after errors

**Task 5.3: Performance and Load Testing**
- Test with multiple simultaneous device connections
- Test high-frequency data streaming performance
- Test large configuration file handling
- Memory usage profiling and optimization

**Task 5.4: Backend Acceptance Testing**
- Verify all API endpoints work correctly
- Test SignalR real-time communication
- Test file operations and persistence
- **Backend must pass all tests before frontend development begins**

### Phase 6: Frontend Development (ONLY AFTER BACKEND COMPLETE)

**PREREQUISITE: All backend tests must pass and system must be proven stable**

**Task 6.1: React Application Setup**
- Initialize React TypeScript project
- Set up testing framework (Jest, React Testing Library)
- Configure Material-UI and other dependencies
- Create TypeScript interfaces matching backend models

**Task 6.2: Frontend Components with Tests**
- Implement components with test-driven development
- Create comprehensive component tests
- Test SignalR integration
- Test error handling and loading states

### Phase 7: System Integration and Deployment

**Task 7.1: Full System Testing**
- End-to-end testing with frontend and backend
- User acceptance testing scenarios
- Cross-browser compatibility testing

**Task 7.2: Application Packaging**
- Configure single executable deployment
- Test deployment on clean systems
- Verify all dependencies are included

## Key Implementation Notes

### File Storage Strategy
- Use user-configurable base directories
- Store catalogs in `/catalogs` subdirectory
- Store configurations in `/configs` subdirectory
- Implement atomic file operations for data integrity

### SignalR Implementation
- Use typed hubs for compile-time safety
- Implement connection retry logic
- Group connections by device for efficient broadcasting
- Handle connection lifecycle properly

### CAN Communication
- Abstract hardware-specific implementations
- Support multiple concurrent connections
- Implement message filtering and routing
- Handle device-specific protocols via catalog definitions

### Frontend Architecture
- Use React Context for global state (device connections, configurations)
- Implement proper TypeScript interfaces matching backend models
- Create reusable components for parameter editing
- Use Material-UI consistently throughout the application

### Error Handling Strategy
- Implement global error boundaries in React
- Use structured logging in backend
- Provide meaningful error messages to users
- Include diagnostic information for troubleshooting

## Testing Strategy and Quality Gates

### Unit Testing Requirements
- **Minimum 90% code coverage** for Core and Hardware projects
- **Minimum 85% code coverage** for API controllers
- All public methods must have corresponding unit tests
- All error paths must be tested
- All edge cases must be covered

### Integration Testing Requirements
- Full API endpoint testing with in-memory database
- SignalR hub testing with test clients
- Hardware communication testing with mock devices
- File system operations testing with temporary directories

### Quality Gates
- **Phase 2 Gate**: All core models and services must pass 100% of unit tests
- **Phase 3 Gate**: Hardware communication layer must pass all unit and integration tests
- **Phase 4 Gate**: All API endpoints must pass integration tests
- **Phase 5 Gate**: Backend system must pass end-to-end testing with performance benchmarks
- **Frontend Gate**: Frontend development cannot begin until Phase 5 gate is passed

### Performance Benchmarks (Must Pass Before Frontend)
- System must handle 10+ concurrent device connections
- Real-time data updates must maintain < 50ms latency
- Memory usage must remain stable over 24-hour operation
- File operations must complete within acceptable timeframes
- System must recover gracefully from all tested error scenarios

## Success Criteria

1. **Backend Completion Criteria (Required before Frontend)**
   - All unit tests passing (90%+ coverage)
   - All integration tests passing
   - Performance benchmarks met
   - Error handling verified
   - Hardware communication stable with mocks
   - SignalR real-time communication verified
   - File operations reliable and atomic

2. **Functional Requirements**
   - Connect to multiple CAN devices simultaneously
   - Load device definitions from JSON catalog files
   - Configure device parameters through web interface
   - Display real-time data with charts and tables
   - Save/load configuration files
   - Log data to files with export capability

3. **Technical Requirements**
   - Single executable deployment
   - No internet connectivity required
   - Responsive web interface
   - Real-time data updates (< 100ms latency)
   - Support for USB and CAN bus communication
   - Generic catalog-driven device support

4. **Quality Requirements**
   - Comprehensive test coverage at all levels
   - Robust error handling and recovery
   - Performance meets specifications
   - Memory usage remains stable
   - System resilience verified

This specification provides Claude Code with a comprehensive roadmap to build the dingoConfig application systematically, with clear phases, tasks, and technical requirements.