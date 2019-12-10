**Code analysis tools**

Roslyn compiler for C#.

For more information, see: https://github.com/dotnet/roslyn

**Overview**

The DLL in this package are not automatically referenced to your project's assemblies, so you will need to reference all of them in each `.asmdef` that will require them.

This also circumvent certain compilation issues in case the user already has these DLLs elsewhere, which is frequent as Roslyn has a wide range of application.
