# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

dingoConfig is a desktop application for reading CAN data and configuring Dingo Electronics devices (dingoPDM, dingoPDM-Max, CANBoard). It uses an ASP.NET Core backend with React frontend, runs locally with no internet connectivity required, and is developed/tested in JetBrains Rider.

## Development Approach

This project follows a **strict phased development approach with comprehensive testing**. The backend must be fully complete and tested before any frontend development begins.

### Phase Requirements
1. **Backend First**: Complete all backend phases (1-5) before starting frontend
2. **Test-Driven Development**: Write tests before implementation
3. **Quality Gates**: Each phase must pass all tests before proceeding
4. **Coverage Requirements**: 90% for Core/Hardware, 85% for API controllers

## Build and Test Commands

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test src/Backend/Tests/dingoConfig.Core.Tests/

# Build for release
dotnet publish -c Release

# Restore packages (if needed)
dotnet restore

# Check solution info
dotnet sln list
```

## Project Architecture

### Multi-Project Structure
```
src/Backend/
├── dingoConfig.API/          # Web API controllers, SignalR hubs
├── dingoConfig.Core/         # Domain models, business logic
├── dingoConfig.Hardware/     # CAN/USB communication layer
└── Tests/                    # Comprehensive test projects
```

### Key Architectural Patterns
- **Repository Pattern**: Abstract data access through interfaces
- **Service Layer**: Business logic separated from controllers
- **SignalR**: Real-time communication for device data streaming
- **Dependency Injection**: Services registered in Program.cs
- **Mock Implementations**: Hardware mocking for testing without physical devices

### Communication Layers
- **CAN Communication**: Peak PCAN USB and USB CDC (SLCAN) support
- **SignalR Groups**: Device connections grouped for efficient broadcasting
- **JSON Catalogs**: Device definitions loaded from catalog files

### File Storage Strategy
- Catalogs stored in `/catalogs` subdirectory
- Configurations stored in `/configs` subdirectory
- Atomic file operations for data integrity
- User-configurable base directories

## Testing Strategy

### Required Test Types
1. **Unit Tests**: All services, models, and business logic
2. **Integration Tests**: API endpoints with in-memory setup
3. **Hardware Mock Tests**: Communication layer without physical devices
4. **End-to-End Tests**: Complete workflows with SignalR

### Quality Gates (Must Pass)
- Phase 2: Core models and services 100% unit tested
- Phase 3: Hardware communication layer fully tested
- Phase 4: All API endpoints integration tested
- Phase 5: Backend system passes end-to-end and performance tests

### Performance Benchmarks
- Handle 10+ concurrent device connections
- Real-time updates with <50ms latency
- Memory stability over 24-hour operation
- Graceful recovery from all error scenarios

## Key Implementation Notes

- **No Frontend Development** until backend phases 1-5 are complete and tested
- Use comprehensive error handling with structured logging
- Implement connection retry logic for hardware communication
- Follow atomic file operations for configuration management
- Create typed SignalR hubs for compile-time safety
- Support multiple concurrent device connections through abstract interfaces