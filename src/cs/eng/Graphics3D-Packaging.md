# Graphics3D package workflow

The Graphics3D packages are built from `RedFox.sln` and packed into the local feed at `artifacts/nuget`.

## Build the projects

Build the full solution during normal development:

```powershell
dotnet build RedFox.sln
```

Build only the Formats package and its dependencies:

```powershell
dotnet build RedFox.Graphics3D.Formats/RedFox.Graphics3D.Formats.csproj
```

Normal builds do not alter package versions or create release packages.

## Create package versions and pack

Run the package script to create every Graphics3D package:

```powershell
./eng/Pack-Graphics3D.ps1
```

Run it with `-PackageId` to release one package whose Graphics3D dependencies have already been released:

```powershell
./eng/Pack-Graphics3D.ps1 -PackageId RedFox.Graphics3D.Formats
```

The script reads and updates `eng/Graphics3D.PackageVersions.json`.
It assigns versions using `YEAR.MONTH.DAY.BUILD`, increments the build number only while packing, stages package output before publishing it to the local feed, and rejects an existing package filename.

Do not edit a package version after its `.nupkg` has been created.
Run the script again to allocate the next build number.

`AssemblyVersion` and `FileVersion` remain independent from package versions.

## Validate a fresh package consumer

Run the clean consumer check after packing:

```powershell
./eng/Test-Graphics3D-Packages.ps1
```

The test restores `RedFox.Graphics3D.Formats` from `artifacts/nuget`, builds a temporary console consumer, creates the default translator manager, and runs it.

For local validation, the test creates a disposable dependency feed for the existing RedFox core dependencies and `CallOfFile`.
Those dependency packages are not copied into `artifacts/nuget` and are deleted after the check.

## Package contents

`RedFox.Graphics3D.Formats` contains every built-in scene translator, including XAsset.
It has a `CallOfFile` dependency because XAsset requires it.

`RedFox.Graphics3D.Rendering`, `RedFox.Graphics3D.D3D11`, `RedFox.Graphics3D.OpenGL`, `RedFox.Graphics3D.Silk`, and `RedFox.Graphics3D.Avalonia` are packaged separately according to their project dependencies.
