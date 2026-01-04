---
allowed-tools: Bash(dotnet build:*)
description: Build the SafeRoom3D C# project
---

Build the SafeRoom3D project and report results:

```powershell
dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"
```

After running:
- If build succeeds, report "Build succeeded"
- If errors occur, list them and suggest fixes
