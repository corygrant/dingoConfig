# DingoConfigurator Web Application Conversion Specification

## Project Overview

Converting DingoConfigurator from WPF desktop application to modern web application using ASP.NET Core + SignalR + React frontend.

**Original Application**: WPF-based configuration tool for automotive Power Distribution Modules (PDMs)
**Target Application**: Local web-based PDM configurator with real-time monitoring (single-instance, offline)

## Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   React Frontend│    │  ASP.NET Core    │    │   PDM Hardware  │
│   (localhost)   │◄──►│  Web API +       │◄──►│   (CAN Bus/USB) │
│                 │    │  SignalR Hub     │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌──────────────────┐
                       │   Local File     │
                       │   Storage        │
                       │   (.json files)  │
                       └──────────────────┘
```

**Key Characteristics:**
- Single-instance application running locally
- No network/internet connectivity required
- File-based configuration storage (JSON)
- Automatic browser launch to localhost
- Embedded file picker for save/load operations

## Technology Stack

### Backend
- **Framework**: ASP.NET Core 8 Web API (local hosting only)
- **Real-time**: SignalR for live PDM data streaming
- **Storage**: File-based JSON configuration storage
- **Hardware Communication**: Background hosted service for CAN bus/USB communication
- **Hosting**: Kestrel server on localhost with automatic browser launch

### Frontend
- **Framework**: React 18 with TypeScript
- **UI Library**: Material-UI (MUI) or Ant Design
- **State Management**: React Context API or Zustand (simpler than Redux for single-user)
- **Real-time**: SignalR JavaScript client
- **Charts**: Chart.js or Recharts for real-time data visualization
- **File Operations**: Browser file picker APIs for save/load
- **Build**: Static files served by ASP.NET Core

### Infrastructure
- **Deployment**: Single executable with embedded web server
- **API Documentation**: Swagger/OpenAPI (for development)
- **Logging**: File-based logging with rolling files
- **Configuration**: appsettings.json with local file paths
- **File Storage**: User-selectable directories for configuration files

## File-Based Configuration Storage

### Configuration File Structure
```json
{
  "metadata": {
    "name": "My PDM Configuration",
    "description": "Racing setup for car #42",
    "createdDate": "2025-06-05T10:30:00Z",
    "modifiedDate": "2025-06-05T14:22:00Z",
    "version": "1.0",
    "configuratorVersion": "2.0.0"
  },
  "channels": [
    {
      "channelNumber": 1,
      "name": "Fuel Pump",
      "currentLimit": 15.0,
      "startupDelay": 100,
      "shutdownDelay": 50,
      "pwmEnabled": false,
      "pwmFrequency": 1000.0,
      "diagnosticsEnabled": true,
      "type": "HighSide"
    }
  ],
  "canSettings": {
    "baseId": 0x600,
    "baudRate": 500000,
    "terminationEnabled": true
  },
  "globalSettings": {
    "maxInputVoltage": 16.0,
    "minInputVoltage": 9.0,
    "temperatureAlarmThreshold": 85.0
  }
}
```

## Core Domain Models

### PDM Configuration
```csharp
public class PdmConfiguration
{
    public ConfigurationMetadata Metadata { get; set; }
    public List<ChannelConfiguration> Channels { get; set; }
    public CanBusSettings CanSettings { get; set; }
    public GlobalSettings GlobalSettings { get; set; }
}

public class ConfigurationMetadata
{
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Version { get; set; } = "1.0";
    public string ConfiguratorVersion { get; set; }
}

public class ChannelConfiguration
{
    public int ChannelNumber { get; set; }
    public string Name { get; set; }
    public double CurrentLimit { get; set; } // Amps
    public int StartupDelay { get; set; } // Milliseconds
    public int ShutdownDelay { get; set; } // Milliseconds
    public bool PwmEnabled { get; set; }
    public double PwmFrequency { get; set; }
    public bool DiagnosticsEnabled { get; set; }
    public ChannelType Type { get; set; }
}

public class CanBusSettings
{
    public int BaseId { get; set; }
    public int BaudRate { get; set; }
    public bool TerminationEnabled { get; set; }
}
```

### Real-time Data Models
```csharp
public class PdmLiveData
{
    public DateTime Timestamp { get; set; }
    public List<ChannelData> Channels { get; set; }
    public double InputVoltage { get; set; }
    public double Temperature { get; set; }
    public ConnectionStatus Status { get; set; }
}

public class ChannelData
{
    public int ChannelNumber { get; set; }
    public double CurrentDraw { get; set; }
    public bool IsActive { get; set; }
    public bool HasFault { get; set; }
    public string FaultDescription { get; set; }
}
```

## API Endpoints

### Configuration Management
```
GET    /api/configuration/current        - Get currently loaded configuration
POST   /api/configuration/load          - Load configuration from file path
POST   /api/configuration/save          - Save current configuration to file path
POST   /api/configuration/new           - Create new blank configuration
GET    /api/configuration/recent        - Get list of recently opened files
POST   /api/configuration/upload-to-pdm - Upload current config to connected PDM
GET    /api/files/save-dialog           - Trigger save file dialog (returns file path)
GET    /api/files/open-dialog           - Trigger open file dialog (returns file path)
```

### Device Management
```
GET    /api/devices                     - List connected PDM devices  
GET    /api/devices/current/status      - Get current device status
POST   /api/devices/connect             - Connect to first available device
POST   /api/devices/disconnect          - Disconnect from current device
GET    /api/devices/current/live-data   - Get current live data (also via SignalR)
POST   /api/devices/scan                - Scan for available devices
```

### System Operations
```
GET    /api/system/status               - Get overall application status
POST   /api/system/emergency-stop       - Emergency stop all outputs
GET    /api/system/logs                 - Get recent application logs
POST   /api/system/shutdown             - Gracefully shutdown application
```

## SignalR Hub Events

### Client → Server
```typescript
// Connection management
connection.invoke("ConnectToDevice");
connection.invoke("DisconnectFromDevice");

// Configuration management
connection.invoke("LoadConfiguration", filePath);
connection.invoke("SaveConfiguration", filePath);

// Device control
connection.invoke("SetChannelState", channelNumber, enabled);
connection.invoke("EmergencyStop");
```

### Server → Client
```typescript
// Real-time data updates
connection.on("LiveDataUpdate", (data: PdmLiveData) => {});
connection.on("DeviceConnected", (deviceInfo: DeviceInfo) => {});
connection.on("DeviceDisconnected", () => {});
connection.on("FaultDetected", (fault: FaultInfo) => {});
connection.on("ConfigurationLoaded", (config: PdmConfiguration) => {});
connection.on("ConfigurationSaved", (filePath: string) => {});
connection.on("ConfigurationUploaded", (success: boolean) => {});

// File operations
connection.on("SaveDialogResult", (filePath: string | null) => {});
connection.on("OpenDialogResult", (filePath: string | null) => {});
```

## Frontend Component Structure

```
src/
├── components/
│   ├── common/
│   │   ├── Layout.tsx
│   │   ├── Navigation.tsx
│   │   ├── FileMenu.tsx           // File operations (New, Open, Save, Recent)
│   │   └── ErrorBoundary.tsx
│   ├── configuration/
│   │   ├── ConfigurationEditor.tsx
│   │   ├── ChannelEditor.tsx
│   │   ├── CanSettingsEditor.tsx
│   │   └── MetadataEditor.tsx     // Name, description, etc.
│   ├── monitoring/
│   │   ├── LiveDashboard.tsx
│   │   ├── ChannelMonitor.tsx
│   │   ├── FaultDisplay.tsx
│   │   └── DataChart.tsx
│   └── devices/
│       ├── DeviceConnection.tsx   // Connect/disconnect single device
│       └── DeviceStatus.tsx
├── services/
│   ├── api.ts                     // REST API client
│   ├── signalr.ts                 // SignalR connection management
│   ├── fileOperations.ts          // File save/load operations
│   └── validation.ts              // Form validation rules
├── types/
│   └── pdm.ts                     // TypeScript type definitions
└── hooks/
    ├── useSignalR.ts              // SignalR hook
    ├── usePdmData.ts              // PDM data management
    ├── useConfiguration.ts        // Configuration state management
    └── useApi.ts                  // API calls hook
```

## Backend Service Structure

```
DingoConfigurator.Web/
├── Controllers/
│   ├── ConfigurationController.cs  // Single config management
│   ├── DevicesController.cs
│   ├── FilesController.cs          // File dialog operations
│   └── SystemController.cs
├── Hubs/
│   └── PdmDataHub.cs
├── Services/
│   ├── IPdmCommunicationService.cs
│   ├── PdmCommunicationService.cs
│   ├── IConfigurationFileService.cs
│   ├── ConfigurationFileService.cs  // File-based operations
│   └── IFileDialogService.cs       // Native file dialogs
└── Models/
    ├── PdmConfiguration.cs         // Core domain models
    ├── DTOs/                       // API response models
    └── Requests/                   // API request models
```

## Key Features to Implement

### Phase 1 - Core Functionality
1. **Application Startup & Local Hosting**
   - ASP.NET Core app that auto-launches browser to localhost
   - Single-instance enforcement (prevent multiple copies)
   - Graceful shutdown handling

2. **File-Based Configuration Management**
   - New, Open, Save, Save As operations
   - Recent files list
   - JSON serialization/deserialization
   - File validation and error handling

3. **Device Discovery & Connection**
   - Scan for available PDM devices via CAN bus/USB
   - Connect/disconnect from single device
   - Real-time connection status

4. **Real-time Monitoring**
   - Live current draw per channel
   - Voltage and temperature monitoring
   - Fault detection and alerts
   - Real-time charts and graphs

### Phase 2 - Enhanced Features
1. **Advanced Configuration Editor**
   - Comprehensive channel configuration UI
   - CAN bus settings management
   - Configuration validation and warnings
   - Undo/redo functionality

2. **Advanced Monitoring**
   - Historical data logging to files
   - Performance analytics
   - Automated fault reporting
   - Export capabilities (CSV, etc.)

3. **User Experience Improvements**
   - Keyboard shortcuts (Ctrl+S, Ctrl+O, etc.)
   - Drag-and-drop file loading
   - Auto-save and recovery
   - Customizable dashboard layouts

## Migration Strategy from WPF

### Data Models Migration
1. Extract existing data models from WPF application
2. Convert to Entity Framework entities
3. Create DTOs for API responses
4. Generate TypeScript interfaces

### Business Logic Migration
1. Identify pure C# business logic (validation, calculations)
2. Extract into service classes
3. Add unit tests
4. Integrate with dependency injection

### File Operations Migration
1. Replace WPF file dialogs with web-based equivalents
2. Maintain JSON file format compatibility
3. Add file validation and error handling
4. Implement recent files functionality

### UI Migration Priority
1. Start with device connection and status pages
2. Migrate file operations (New, Open, Save)
3. Convert configuration forms
4. Implement real-time monitoring dashboard
5. Add advanced features

### Hardware Communication Migration
1. Extract existing CAN bus/USB communication code
2. Wrap in background hosted service
3. Implement device abstraction layer
4. Add connection pooling and retry logic

## Development Phases

### Phase 1: Foundation (Weeks 1-2)
- [ ] Create ASP.NET Core project with auto-launch and single-instance enforcement
- [ ] Set up file-based configuration storage (JSON)
- [ ] Implement basic API controllers for configuration management
- [ ] Create React frontend with file menu (New, Open, Save, Recent)
- [ ] Set up SignalR hub for real-time communication

### Phase 2: Core Features (Weeks 3-4)
- [ ] Implement device communication service
- [ ] Build configuration management API
- [ ] Create configuration editor UI
- [ ] Add real-time data streaming
- [ ] Implement basic monitoring dashboard

### Phase 3: Advanced Features (Weeks 5-6)
- [ ] Add comprehensive error handling
- [ ] Implement data validation
- [ ] Create advanced monitoring charts
- [ ] Add device management features
- [ ] Optimize performance and add caching

### Phase 4: Polish & Deployment (Week 7)
- [ ] UI/UX improvements
- [ ] Mobile responsiveness
- [ ] Documentation
- [ ] Deployment setup
- [ ] Testing and bug fixes

## Technical Considerations

### Performance
- Efficient SignalR messaging for real-time data
- Local file caching for recently accessed configurations
- Background tasks for heavy hardware operations
- Optimized JSON serialization

### Local Application Architecture
- Single-instance application using mutex
- Embedded web server (Kestrel) for local hosting
- File-based storage with atomic write operations
- Proper resource disposal for hardware connections

### Hardware Communication
- Implement connection pooling for PDM devices
- Add automatic reconnection logic
- Handle device disconnections gracefully
- Implement proper disposal patterns for hardware resources

## Testing Strategy

### Backend Testing
- Unit tests for services and business logic
- Integration tests for API endpoints
- SignalR hub testing
- Hardware communication mocking

### Frontend Testing
- Component unit tests with React Testing Library
- Integration tests for SignalR communication
- End-to-end tests with Playwright or Cypress

## Deployment

### Docker Setup
```dockerfile
# Backend
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY . /app
WORKDIR /app
EXPOSE 80
ENTRYPOINT ["dotnet", "DingoConfigurator.Web.dll"]

# Frontend (if serving separately)
FROM nginx:alpine
COPY build/ /usr/share/nginx/html
```

### Environment Configuration
- Development: Local SQL Server, local CAN hardware
- Staging: Azure SQL, mocked hardware
- Production: Azure SQL, actual hardware integration

## Success Metrics

1. **Functionality Parity**: All existing WPF features work in web version
2. **Performance**: Real-time data updates with <100ms latency
3. **Reliability**: Stable device communication and file operations
4. **Usability**: Intuitive web interface that feels like desktop app
5. **File Compatibility**: Perfect compatibility with existing configuration files

## Future Enhancements

1. **Enhanced File Operations**: Import/export from other PDM formats
2. **Advanced Logging**: Comprehensive data logging and analysis
3. **Configuration Templates**: Pre-built templates for common setups
4. **Hardware Simulation**: Mock PDM mode for testing without hardware
5. **Plugin Architecture**: Support for different PDM manufacturers