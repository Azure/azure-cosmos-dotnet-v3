# Azure Cosmos DB .NET AOT Sample

This is a sample console application demonstrating how to use the Microsoft.Azure.Cosmos SDK with Native AOT compilation in .NET 9.

## Features

- ? **Native AOT Compilation** - Compiles to a self-contained native executable
- ? **Full Trimming** - Uses aggressive IL trimming for smaller size
- ? **AOT & Trim Analysis** - All AOT and trim analyzers enabled
- ? **Azure Cosmos DB Integration** - Uses Microsoft.Azure.Cosmos NuGet package
- ?? **Dependency Warnings Suppressed** - Known AOT warnings from dependencies are suppressed

## Project Configuration

Key settings for AOT compatibility:

```xml
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>full</TrimMode>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<IsAotCompatible>true</IsAotCompatible>
```

## Build & Run

### Debug Build
```bash
dotnet build
dotnet run
```

### AOT Release Build
```bash
cd .\AOT-Sample\
rm .\obj\ -Recurse -Force
rm .\bin\ -Recurse -Force
dotnet publish AOT-Sample.csproj -c Release --verbosity normal

# Run the native executable
./bin/Release/net9.0/win-arm64/native/AOT-Sample.exe
```

## Known Limitations

The current Microsoft.Azure.Cosmos SDK (3.44.0) has some AOT/trim warnings that are suppressed:

- **IL2104**: Trim warnings from dependencies
- **IL3000**: Single-file app compatibility issues  
- **IL3053**: AOT analysis warnings

These warnings are from dependencies (Newtonsoft.Json, System.Configuration.ConfigurationManager, etc.) and don't affect the basic functionality.

## File Size

The compiled native executable is approximately **26.76 MB** and includes the entire .NET runtime and Cosmos SDK.

## Next Steps

To build a production-ready AOT application with Cosmos DB:

1. Use real connection strings and endpoints
2. Implement proper error handling
3. Consider using System.Text.Json instead of Newtonsoft.Json for better AOT compatibility
4. Test thoroughly with your specific Cosmos DB operations