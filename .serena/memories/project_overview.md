# VSA Project Overview

## Project Purpose
VSA (VrcSnapArchive) is a Windows desktop application launcher for VRChat that automates screenshot processing and archiving. It integrates with VRChat via OSC (Open Sound Control) to capture and process in-game screenshots, apply metadata, and organize them into structured folders. The application includes features for real-time monitoring, image processing, and VRChat parameter synchronization.

## Tech Stack
- **Language**: C#
- **Framework**: .NET 8.0 (Windows-specific)
- **UI**: Windows Forms
- **Key Libraries**:
  - Newtonsoft.Json for JSON handling
  - Rug.Osc for OSC communication
  - SixLabors.ImageSharp for image processing
  - vrc-oscquery-lib for OSCQuery protocol
  - MeaMod.DNS for DNS operations
- **Platform**: Windows only (net8.0-windows target)

## Code Style and Conventions
- Standard C# naming conventions (PascalCase for classes/methods, camelCase for variables)
- Uses partial classes (e.g., Form1.Designer.cs for UI components)
- XML documentation comments for public methods
- Async/await pattern for asynchronous operations
- Dependency injection pattern for services
- Event-driven architecture for UI interactions
- Mutex-based single instance enforcement

## Development Commands
- **Build**: `dotnet build`
- **Run**: `dotnet run`
- **Clean**: `dotnet clean`
- **Restore packages**: `dotnet restore`
- **Publish**: `dotnet publish -c Release -r win-x64 --self-contained`

## Codebase Structure
- **Root**: Main application files (Program.cs, Form1.cs, etc.)
- **FileSystems/**: File management and watching services
- **ImageControllers/**: Image processing and metadata handling
- **OSCServer/**: OSC communication and VRChat integration
- **Settings/**: Configuration management
- **VRC-Game/**: VRChat-specific functionality
- **Properties/**: Resources and assembly info
- **test/**: JavaScript test files (not C# unit tests)

## Testing
No C# unit tests found. JavaScript files in test/ folder appear to be for image processing testing.

## Entry Points
- Main entry: `Program.cs::Main()` - Initializes Windows Forms application with single-instance mutex
- UI: `VSA_launcher` form class in Form1.cs

## System Commands (Windows)
- File operations: Use .NET System.IO classes
- Process management: System.Diagnostics.Process
- Windows API: P/Invoke for AllocConsole/FreeConsole in debug mode

## Guidelines and Patterns
- Single responsibility principle for service classes
- Observer pattern for file watching
- Factory pattern for OSC servers
- Configuration-based settings management
- Error handling with try-catch and user notifications
- Thread-safe operations for UI updates