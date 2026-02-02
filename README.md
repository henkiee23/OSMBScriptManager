# OSMB Plugin Updater

Small desktop tool to scan git repositories for .jar plugin files, track commit IDs per plugin, and download selected plugins to a target folder.

Quick notes:
- This is a .NET 9 / Avalonia UI application. Build with `dotnet build`.
- Settings and runtime state are stored under `%APPDATA%\OSMBScriptManager` on Windows.
- Fonts are embedded under `Resources/Fonts`.

Before publishing:
- Add a license file (e.g. `LICENSE`)
- Add CI (GitHub Actions) for builds and tests if desired
