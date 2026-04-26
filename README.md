# mdb-reflection-inspector
Reflection Inspector for mdb (unity games) — invoke methods/properties and build objects at runtime.

# ImGui-based runtime inspector for Unity mods. Inspect classes, invoke methods/properties/fields, construct objects with the built-in Object Builder, register instances and save favorite calls.

## Features
•	Set Namespace, Class and Member to invoke
•	Choose call kind: method, prop-get, prop-set, field-get, field-set
•	Add arguments: primitives, registered instances or constructed objects
•	Object Builder to create complex objects and register them
•	Save/load favorite calls
•	Execution log with levels (ok/warn/error)

## Build
•	Requirements: .NET Framework 4.8.1, C# 9, Visual Studio
•	Open solution in Visual Studio, build the project and produce the mod DLL
•	Install the DLL according to the host mod loader instructions

## Usage
•	Load the mod in the game
•	Open the "Reflection Inspector" ImGui window
•	Enter namespace/class/member, select kind and fill arguments
•	Click "Invoke" to run; results appear in the log and result field
•	Use Object Builder to create objects and inject them into argument slots
