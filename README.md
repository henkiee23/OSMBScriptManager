# OSMB Script Manager

A modern desktop application for managing and auto-updating OSMB script from multiple developer repositories.

## Features

### Script Management
- **Multi-Repository Support**: Browse and track scripts from multiple developer repositories (GitHub, GitLab)
- **Smart Script Discovery**: Automatically scans git repositories for `.jar` plugin files
- **Version Tracking**: Tracks commit IDs and dates for each script to detect updates
- **Selective Installation**: Choose which scripts to install from each developer's repository
- **Update Detection**: Automatically detects when newer versions of installed scripts are available
- **Batch Operations**: Update all installed scripts at once or individually

### Installation & Updates
- **One-Click Installation**: Download and install selected scripts to your OSMB scripts folder
- **Installed Script Monitoring**: Scans your target folder to show currently installed scripts
- **Update Management**: Compare installed versions against repository versions and update with one click
- **Efficient Repository Cloning**: Uses sparse checkout to fetch only `.jar` files, minimizing download size

### Application Management
- **Auto-Start**: Optional Windows auto-start on system login
- **Self-Update**: Automatic update checks on startup with configurable skip options
- **Installer Support**: Includes Inno Setup installer for easy installation

### User Interface
- **Theme Support**: Light/Dark theme with system theme following option
- **Developer Organization**: Browse scripts organized by developer/repository

### Configuration
- **Persistent Settings**: Settings and state stored in `%APPDATA%\OSMBScriptManager`
- **Target Directory**: Configure where scripts are installed
- **Update Preferences**: Control auto-update behavior and skip specific versions

## Technology Stack

- **.NET 9**: Modern .NET runtime
- **Avalonia UI 11**: Cross-platform UI framework
- **Git Integration**: Direct git operations for repository scanning
- **Sparse Checkout**: Optimized repository cloning

## Installing

Download the latest release from the [GitHub Releases page](https://github.com/henkiee23/OSMBScriptManager/releases/latest).

1. Download the `OSMBScriptManager-Setup-*-win-x64.exe` installer
2. Run the installer and follow the setup wizard
3. Launch OSMB Script Manager from the Start Menu or desktop shortcut

The installer will automatically place the application in your Program Files and create shortcuts for easy access.

## Building

```bash
# Build the application
dotnet build

# Run tests
dotnet test

# Publish (single-file executable)
dotnet publish -c Release -r win-x64 --self-contained

# Build installer (requires Inno Setup)
cd Installer
.\build-installer.ps1
```

## Configuration

Settings are automatically managed through the UI, but advanced users can edit:
- `%APPDATA%\OSMBScriptManager\settings.json` - Application settings
- `%APPDATA%\OSMBScriptManager\plugin_state.json` - Tracked script state

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
