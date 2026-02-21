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

3. **Add SDK Reference:**
   - Copy the SDK from the Managed folder to the mod directory

## Build

```bash
dotnet build -c Release
```

## Prerequisites

- [MDB Framework](https://github.com/Zaclin-GIT/MDB) injected into the game at least once (to generate the SDK)
- .NET Framework 4.8.1 targeting pack (installed with Visual Studio)
- .NET SDK 8.0+
