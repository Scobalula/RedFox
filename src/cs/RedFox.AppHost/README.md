# RedFox.AppHost

Windows x64 .NET launcher with a splash screen, optionally packing the publish output into a single exe.

## Usage

```xml
<RedFoxAppHost>true</RedFoxAppHost>
<RedFoxAppHostSplash>Splash.png</RedFoxAppHostSplash>
```
```xml
<PackageReference Include="RedFox.AppHost" />
```

Run `dotnet publish -r win-x64` as normal.

## Properties

| Property | Default | |
|---|---|---|
| `RedFoxAppHostMode` | `packed` | `packed`: single exe in `publish\..\packed\`. `splash`: no packing, replaces `publish\<App>.exe` |
| `RedFoxAppHostName` | `$(Product)` | |
| `RedFoxAppHostSplash` | | PNG |
| `RedFoxAppHostFont` | `Segoe UI` | |
| `RedFoxAppHostBackground` | `#1E1E22` | `#RRGGBB` or `#AARRGGBB` |
| `RedFoxAppHostForeground` | `#F0F0F0` | |
| `RedFoxAppHostSecondaryText` | `#A0A0A8` | |
| `RedFoxAppHostAccent` | `#E8682A` | |
| `RedFoxAppHostBorder` | `#3C3C42` | `#00000000` for none |
| `RedFoxAppHostCornerRadius` | `0` | |
| `RedFoxAppHostQuality` | `5` | Brotli 0-11 |
| `RedFoxAppHostOutput` | | Override output path |

Packed exes extract to `%LOCALAPPDATA%\<Name>\App\<exe path hash>\<build hash>\`; a new build at the same path replaces the old one. Framework-dependent apps prompt to download .NET if the required runtime is missing. Not supported: `PublishAot`, `PublishSingleFile`.

## Code

```csharp
AppHostSplash.SetStatus("Loading...");
AppHostSplash.Close();
```

Calling these is optional. Without the launcher they do nothing.

## Building

`scripts\Build-AppHost.ps1` rebuilds `RedFoxAppHost.exe` into `RedFox.AppHost.Packer`. It's packed with the other RedFox packages.
