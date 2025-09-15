# Root Directory CS Files
- Program.cs: Main entry point with single-instance mutex and Windows Forms initialization
- StartupManager.cs: Application startup management
- VRChatLogParser.cs: (Appears empty or minimal)
- Form1.cs: Main UI form (VSA_launcher) and SystemTrayIcon classes

# FileSystems Folder
- FileHelper.cs: File operation utilities
- FileNameGenerator.cs: Generates filenames for processed images
- FileWatcherService.cs: Monitors file system changes
- FolderStructureManager.cs: Manages folder organization

# ImageControllers Folder
- Crc32.cs: CRC32 checksum calculation
- ImageProcessor.cs: Main image processing logic
- MetadataAnalyzer.cs: Analyzes image metadata
- MetadataProcessor.cs: Processes and modifies metadata
- PngMetadataManager.cs: PNG-specific metadata management
- SimplePngMetadataManager.cs: Simplified PNG metadata handling

# OSCServer Folder
- DelayedOscServerManager.cs: Manages delayed OSC server operations
- IntegralOscServer.cs: Integral OSC server implementation
- OscDataStore.cs: Stores OSC data
- OscManager.cs: Main OSC communication manager
- OSCParameterSender.cs: Sends OSC parameters
- VirtualLens2OscServer.cs: Virtual lens OSC server
- VRChatListener.cs: Listens for VRChat OSC messages

# Settings Folder
- AppSettings.cs: Application settings model
- RenameFormatSettings.cs: Filename format settings
- SettingsManager.cs: Settings persistence and management

# VRC-Game Folder
- VRChatInitializationManager.cs: VRChat initialization logic
- VRChatLogParser.cs: Parses VRChat log files
- VRChatUserDetector.cs: Detects VRChat users