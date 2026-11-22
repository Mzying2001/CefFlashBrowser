# Visual C++ runtime

The x86 and x64 archives contain Microsoft-signed runtime DLLs, version `14.44.35211.0`, from Visual Studio 2022's `VC\Redist\MSVC\14.44.35112\<platform>\Microsoft.VC143.CRT` directory. Microsoft distributes these DLLs under the [Visual Studio 2022 redistribution terms](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution).

To refresh the archives for a newer build toolset, take both architectures from the corresponding redistributable directory. Place `msvcp140.dll` and `vcruntime140.dll` at the archive root for both architectures, and include `vcruntime140_1.dll` for x64. Update the version here and run the release package smoke checks for both architectures.
