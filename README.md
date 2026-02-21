# MDB Mod Template

A starter project for creating mods with [MDB Framework](https://github.com/Zaclin-GIT/MDB).

## Setup

1. **Clone this template:**
   ```bash
   git clone -b mdbmod https://github.com/Zaclin-GIT/MDB.Templates.git MyModName
   cd MyModName
   ```

2. **Rename the project:**
   - Rename `MyMod.csproj` to `YourModName.csproj`
   - Find & replace `MyMod` → `YourModName` across all files
   - Find & replace `MyAuthor` → your name

3. **Set your game path:**
   - Open the `.csproj` and replace `GAME_FOLDER` with the path to your game (where the `.exe` is)

4. **Remove the template git history** (start fresh):
   ```bash
   Remove-Item .git -Recurse -Force
   git init
   ```

## Build

```bash
dotnet build -c Release
```

## Deploy

Copy `bin\Release\YourModName.dll` into `<GameFolder>\MDB\Mods\`

> **Tip:** Uncomment the `CopyToMods` target in the `.csproj` to auto-deploy on build.

## What's Included

| File | Description |
|------|-------------|
| `Mod.cs` | Main mod entry point — `OnLoad`, `OnUpdate`, `OnUnload` |
| `ImGuiWindow.cs` | ImGui debug window (delete if you don't need UI) |
| `MyMod.csproj` | Project file with SDK reference |

## Prerequisites

- [MDB Framework](https://github.com/Zaclin-GIT/MDB) injected into the game at least once (to generate the SDK)
- .NET Framework 4.8.1 targeting pack (installed with Visual Studio)
- .NET SDK 8.0+
