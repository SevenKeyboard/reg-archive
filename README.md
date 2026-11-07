# reg-archive

Synchronous registry import/export on Windows using C# (.NET 10) and Windows reg.exe.

```csharp
using SevenKeyboard;

bool saved = RegArchive.Export(@"HKCU\Software\MyApp", @"C:\Backup\MyApp.reg");
bool restored = RegArchive.Import(@"C:\Backup\MyApp.reg");
```

Both methods return false for validation, registry, process or expected file/permission
failures. Export requires a directory in the file path and creates it if missing.
Overwrite defaults to true; false preserves an existing destination without prompting,
including when another caller creates it during export.

The optional Microsoft.Win32.RegistryView defaults to Default, which omits the /reg
switch and uses the OS-native reg.exe view, even from an x86 process on 64-bit Windows.
Registry32 and Registry64 explicitly select a view.

Version 1.0.1 makes no-overwrite failure quiet. No elevation or timeout is added.
Supports x64, x86 and NativeAOT.

Run the isolated HKCU smoke checks with:
```text
dotnet run --project tests/RegArchive.Smoke -c Release -r win-x64
dotnet run --project tests/RegArchive.Smoke -c Release -r win-x86
dotnet publish tests/RegArchive.Smoke -c Release -r win-x64 -p:PublishAot=true
```
