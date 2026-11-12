//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.MSBuild
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Integration tests that verify native assets match the published RuntimeIdentifier,
    /// including Linux native loading and fallback when the library is unavailable.
    /// These tests pack the SDK into a local NuGet package to properly test the .targets file behavior.
    /// </summary>
    [TestClass]
    [TestCategory("LongRunning")]
    public class CosmosTargetsInteropPublishTests
    {
        private const string LinuxLibraryName = "libQueryPlanInterop.so";
        private static string testProjectsRoot;
        private static string localNugetPackagePath;
        private static string packageVersion;
        private static readonly string[] WindowsNativeDlls = new[]
        {
            "Microsoft.Azure.Cosmos.ServiceInterop.dll",
            "Cosmos.CRTCompat.dll",
            "msvcp140.dll",
            "vcruntime140.dll",
            "vcruntime140_1.dll"
        };

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext _)
        {
            testProjectsRoot = Path.Combine(Path.GetTempPath(), "CosmosTargetsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testProjectsRoot);

            // Create local NuGet package from the SDK
            await CreateLocalNuGetPackageAsync();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Directory.Exists(testProjectsRoot))
            {
                try
                {
                    Directory.Delete(testProjectsRoot, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Tests that Windows native DLLs are not copied when publishing for non-Windows platforms.
        /// </summary>
        /// <param name="runtimeIdentifier">The runtime identifier to test (e.g., linux-x64, osx-x64).</param>
        [TestMethod]
        [DataRow("linux-x64")]
        [DataRow("linux-arm64")]
        [DataRow("osx-x64")]
        [DataRow("osx-arm64")]
        public async Task Publish_WithNonWindowsRuntimeIdentifier_DoesNotCopyWindowsDlls(string runtimeIdentifier)
        {
            string publishPath = await this.CreateAndPublishTestProjectAsync($"NonWinTest_{runtimeIdentifier}", runtimeIdentifier);

            this.AssertWindowsDllsNotPresent(publishPath, runtimeIdentifier);
            string[] linuxLibraries = Directory.GetFiles(publishPath, LinuxLibraryName, SearchOption.AllDirectories);
            if (runtimeIdentifier == "linux-x64" || runtimeIdentifier == "linux-arm64")
            {
                Assert.AreEqual(1, linuxLibraries.Length, "Only the matching Linux architecture should be published.");
                Assert.AreEqual(Path.Combine(publishPath, LinuxLibraryName), linuxLibraries[0]);
                using BinaryReader reader = new BinaryReader(File.OpenRead(linuxLibraries[0]));
                reader.BaseStream.Position = 18; // ELF e_machine.
                Assert.AreEqual(runtimeIdentifier == "linux-x64" ? (ushort)62 : (ushort)183, reader.ReadUInt16());
            }
            else
            {
                Assert.AreEqual(0, linuxLibraries.Length, "Linux libraries must not be published for macOS.");
            }
        }

        /// <summary>
        /// Tests that Windows native DLLs are copied when publishing for Windows platforms.
        /// </summary>
        /// <param name="runtimeIdentifier">The runtime identifier to test (e.g., win-x64, win-x86).</param>
        [TestMethod]
        [DataRow("win-x64")]
        [DataRow("win-x86")]
        [DataRow("win-arm64")]
        public async Task Publish_WithWindowsRuntimeIdentifier_CopiesWindowsDlls(string runtimeIdentifier)
        {
            string publishPath = await this.CreateAndPublishTestProjectAsync($"WinTest_{runtimeIdentifier}", runtimeIdentifier);

            this.AssertWindowsDllsPresent(publishPath, runtimeIdentifier);
            Assert.AreEqual(0, Directory.GetFiles(publishPath, LinuxLibraryName, SearchOption.AllDirectories).Length);
        }

        /// <summary>
        /// Tests that Windows native DLLs are copied when publishing without a RuntimeIdentifier,
        /// which is the most common developer scenario (regular 'dotnet publish' without -r).
        /// </summary>
        [TestMethod]
        public async Task Publish_WithoutRuntimeIdentifier_CopiesWindowsDlls()
        {
            string publishPath = await this.CreateAndPublishTestProjectAsync("NoRidTest", runtimeIdentifier: null);

            this.AssertWindowsDllsPresent(publishPath, "no RuntimeIdentifier");
            Assert.IsTrue(File.Exists(Path.Combine(publishPath, "runtimes", "linux-x64", "native", LinuxLibraryName)));
            Assert.IsTrue(File.Exists(Path.Combine(publishPath, "runtimes", "linux-arm64", "native", LinuxLibraryName)));
            Assert.IsFalse(File.Exists(Path.Combine(publishPath, LinuxLibraryName)), "Portable publish must resolve the Linux library through runtimes/<rid>/native.");
        }

        [TestMethod]
        [DataRow("linux-x64")]
        [DataRow("win-x64")]
        public async Task Publish_WithSourceProjectReference_SelectsNativePayload(string runtimeIdentifier)
        {
            string publishPath = await this.CreateAndPublishTestProjectAsync(
                $"SourceTest_{runtimeIdentifier}", runtimeIdentifier, useProjectReference: true);

            if (runtimeIdentifier.StartsWith("win", StringComparison.Ordinal))
            {
                this.AssertWindowsDllsPresent(publishPath, runtimeIdentifier);
                Assert.AreEqual(0, Directory.GetFiles(publishPath, LinuxLibraryName, SearchOption.AllDirectories).Length);
            }
            else
            {
                this.AssertWindowsDllsNotPresent(publishPath, runtimeIdentifier);
                string[] linuxLibraries = Directory.GetFiles(publishPath, LinuxLibraryName, SearchOption.AllDirectories);
                Assert.AreEqual(1, linuxLibraries.Length);
                Assert.AreEqual(Path.Combine(publishPath, LinuxLibraryName), linuxLibraries[0]);
                using BinaryReader reader = new BinaryReader(File.OpenRead(linuxLibraries[0]));
                reader.BaseStream.Position = 18;
                Assert.AreEqual((ushort)62, reader.ReadUInt16(), "Source-reference publish must include the x64 ELF library.");
            }
        }

        [TestMethod]
        [DataRow("Debug")]
        [DataRow("Release")]
        public async Task Direct_WithGlobalSdkConstants_PreservesProjectConstants(string configuration)
        {
            const string internalConstants = "DOCDBCLIENT COSMOSCLIENT NETSTANDARD20 INTERNAL";
            string directProjectPath = Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "Direct", "src", "Microsoft.Azure.Cosmos.Direct.csproj");
            string arguments = $"msbuild \"{directProjectPath}\" -nologo -p:Configuration={configuration} -getProperty:DefineConstants";
            string normalConstants = await RunDotnetCommandAsync(arguments);
            string overriddenConstants = await RunDotnetCommandAsync(
                $"{arguments} \"-p:DefineConstants={internalConstants}\"");

            Assert.AreEqual(
                normalConstants.Trim(),
                overriddenConstants.Trim(),
                "SDK-wide compiler constants must not replace Direct's compilation settings.");

            string sdkProjectPath = Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj");
            string sdkConstants = await RunDotnetCommandAsync(
                $"msbuild \"{sdkProjectPath}\" -nologo -p:Configuration={configuration} \"-p:DefineConstants={internalConstants}\" -getProperty:DefineConstants");
            Assert.AreEqual(internalConstants, sdkConstants.Trim(), "The SDK must still receive its global compiler constants.");
        }

        [TestMethod]
        public async Task Package_NativePayloadsMatchRestoredPackages()
        {
            string projectPath = Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj");
            string evaluation = await RunDotnetCommandAsync(
                $"msbuild \"{projectPath}\" -nologo -p:Configuration=Release -getProperty:PkgMicrosoft_Azure_Cosmos_ServiceInterop,PkgMicrosoft_Azure_Cosmos_QueryPlanInterop_Linux");
            JObject properties = (JObject)JObject.Parse(evaluation)["Properties"];
            string windowsPackage = (string)properties["PkgMicrosoft_Azure_Cosmos_ServiceInterop"];
            string linuxPackage = (string)properties["PkgMicrosoft_Azure_Cosmos_QueryPlanInterop_Linux"];
            Assert.IsFalse(string.IsNullOrEmpty(windowsPackage), "The ServiceInterop package must be restored.");
            Assert.IsFalse(string.IsNullOrEmpty(linuxPackage), "The Linux query-plan package must be restored.");

            using ZipArchive package = ZipFile.OpenRead(
                Path.Combine(localNugetPackagePath, $"Microsoft.Azure.Cosmos.{packageVersion}.nupkg"));
            Assert.AreEqual(
                7,
                package.Entries.Count(entry => entry.FullName.StartsWith("runtimes/", StringComparison.Ordinal)
                    && entry.FullName.Contains("/native/", StringComparison.Ordinal)),
                "The SDK must bundle exactly five Windows DLLs and two Linux query-plan libraries.");

            foreach (string dll in WindowsNativeDlls)
            {
                AssertPackageEntryMatchesFile(
                    package,
                    $"runtimes/win-x64/native/{dll}",
                    Path.Combine(windowsPackage, "runtimes", "win-x64", "native", dll));
            }

            foreach (string rid in new[] { "linux-x64", "linux-arm64" })
            {
                AssertPackageEntryMatchesFile(
                    package,
                    $"runtimes/{rid}/native/{LinuxLibraryName}",
                    Path.Combine(linuxPackage, "runtimes", rid, "native", LinuxLibraryName));
            }
        }

        [TestMethod]
        [TestCategory("Windows")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Publish_WindowsX64_ExecutesNativeQueryPlanning(bool useProjectReference)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                Assert.Inconclusive("Executing ServiceInterop requires a Windows x64 process.");
            }

            string projectName = useProjectReference ? "WindowsNativeSourceTest" : "WindowsNativePackageTest";
            string publishPath = await this.CreateAndPublishTestProjectAsync(
                projectName, "win-x64", useProjectReference);
            await RunDotnetCommandAsync(
                $"\"{Path.Combine(publishPath, projectName + ".dll")}\" --verify-windows-query-planning");
        }

        private static void AssertPackageEntryMatchesFile(ZipArchive package, string entryName, string filePath)
        {
            ZipArchiveEntry entry = package.GetEntry(entryName);
            Assert.IsNotNull(entry, $"Missing SDK package entry: {entryName}");
            using Stream actual = entry.Open();
            using Stream expected = File.OpenRead(filePath);
            using SHA256 sha = SHA256.Create();
            CollectionAssert.AreEqual(
                sha.ComputeHash(expected),
                sha.ComputeHash(actual),
                $"Packaged native payload must match its restored NuGet source: {entryName}");
        }

        private static async Task CreateLocalNuGetPackageAsync()
        {
            string repoRoot = GetRepositoryRoot();
            string cosmosProjectPath = Path.Combine(repoRoot, "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj");
            string packOutputDir = Path.Combine(testProjectsRoot, "nuget-packages");
            Directory.CreateDirectory(packOutputDir);

            // Use a unique version to avoid cache conflicts
            packageVersion = $"99.0.0-test.{DateTime.UtcNow:yyyyMMddHHmmss}";

            await RunDotnetCommandAsync(
                $"pack \"{cosmosProjectPath}\" -c Release -o \"{packOutputDir}\" /p:Version={packageVersion} /p:PackageVersion={packageVersion}",
                timeoutMinutes: 10);

            localNugetPackagePath = packOutputDir;
        }

        private async Task<string> CreateAndPublishTestProjectAsync(string projectName, string runtimeIdentifier, bool useProjectReference = false)
        {
            string projectDir = Path.Combine(testProjectsRoot, projectName);
            Directory.CreateDirectory(projectDir);

            string projectFile = Path.Combine(projectDir, $"{projectName}.csproj");
            string programFile = Path.Combine(projectDir, "Program.cs");
            string nugetConfigFile = Path.Combine(projectDir, "nuget.config");

            // Create nuget.config to use local package source
            File.WriteAllText(nugetConfigFile, $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{localNugetPackagePath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>");

            string sdkReference = useProjectReference
                ? $@"<ProjectReference Include=""{Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj")}"" />"
                : $@"<PackageReference Include=""Microsoft.Azure.Cosmos"" Version=""{packageVersion}"" />";
            string sourceDependencies = useProjectReference
                ? $@"<ProjectReference Include=""{Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "Direct", "src", "Microsoft.Azure.Cosmos.Direct.csproj")}"" />
    <PackageReference Include=""Microsoft.HybridRow"" Version=""[$(HybridRowVersion)]"" />
    <PackageReference Include=""Microsoft.Azure.Cosmos.QueryPlanInterop.Linux"" Version=""[$(QueryPlanInteropLinuxVersion)]"" />"
                : string.Empty;
            string sourceVersionImport = useProjectReference
                ? $@"<Import Project=""{Path.Combine(GetRepositoryRoot(), "Directory.Build.props")}"" />"
                : string.Empty;

            // Create a simple console app project that references the SDK.
            File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  {sourceVersionImport}
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    {sdkReference}
    {sourceDependencies}
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>");

            // Reflection exercises the packaged SDK's internal native planner without friend-assembly access.
            File.WriteAllText(programFile, @"
if (System.Array.IndexOf(args, ""--verify-windows-query-planning"") >= 0)
{
    System.Environment.SetEnvironmentVariable(""AZURE_COSMOS_BYPASS_QUERY_PARSING"", ""false"");
    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Microsoft.Azure.Cosmos.CosmosClient).TypeHandle);
    var providerType = typeof(Microsoft.Azure.Cosmos.CosmosClient).Assembly.GetType(
        ""Microsoft.Azure.Cosmos.Query.Core.QueryPlan.QueryPartitionProvider"", throwOnError: true)!;
    using var provider = (System.IDisposable)System.Activator.CreateInstance(
        providerType,
        new object[] { new System.Collections.Generic.Dictionary<string, object> { { ""maxSqlQueryInputLength"", 524288 } } })!;
    var getPlan = providerType.GetMethod(""TryGetPartitionedQueryExecutionInfo"")!;
    var partitionKey = Newtonsoft.Json.JsonConvert.DeserializeObject(
        ""{\""paths\"":[\""/pk\""],\""kind\"":\""Hash\"",\""version\"":2}"",
        getPlan.GetParameters()[1].ParameterType)!;
    string query = Newtonsoft.Json.JsonConvert.SerializeObject(
        new { query = ""SELECT TOP 1 * FROM c WHERE c.pk = 'partition'"" });
    var outcome = getPlan.Invoke(provider, new object?[]
    {
        query, partitionKey, null,
        true, false, true, false, true, false, false,
        Microsoft.Azure.Cosmos.GeospatialType.Geography
    })!;
    var outcomeType = outcome.GetType();
    if (!(bool)outcomeType.GetProperty(""Succeeded"")!.GetValue(outcome)!)
    {
        throw new System.InvalidOperationException(
            ""Published SDK native query planning failed."",
            (System.Exception)outcomeType.GetProperty(""Exception"")!.GetValue(outcome)!);
    }

    var plan = Newtonsoft.Json.Linq.JObject.Parse(
        Newtonsoft.Json.JsonConvert.SerializeObject(outcomeType.GetProperty(""Result"")!.GetValue(outcome)));
    if ((int?)plan[""queryInfo""]?[""top""] != 1
        || plan[""queryRanges""] is not Newtonsoft.Json.Linq.JArray ranges
        || ranges.Count != 1
        || string.IsNullOrEmpty((string?)ranges[0][""min""])
        || (string?)ranges[0][""min""] != (string?)ranges[0][""max""])
    {
        throw new System.InvalidOperationException(""Unexpected native TOP or partition-routing plan: "" + plan);
    }
}

bool expectGateway = System.Array.IndexOf(args, ""--verify-linux-query-planning-fallback"") >= 0;
if (expectGateway || System.Array.IndexOf(args, ""--verify-linux-query-planning"") >= 0)
{
    System.Environment.SetEnvironmentVariable(""AZURE_COSMOS_USE_QUERY_PLAN_INTEROP_LINUX"", ""true"");
    System.Environment.SetEnvironmentVariable(""AZURE_COSMOS_BYPASS_QUERY_PARSING"", ""false"");
    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Microsoft.Azure.Cosmos.CosmosClient).TypeHandle);
    var retriever = typeof(Microsoft.Azure.Cosmos.CosmosClient).Assembly.GetType(
        ""Microsoft.Azure.Cosmos.Query.Core.QueryPlan.QueryPlanRetriever"", throwOnError: true)!;
    var bypass = retriever.GetMethod(""BypassQueryParsing"",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
    if ((bool)bypass.Invoke(null, null)! != expectGateway)
    {
        throw new System.InvalidOperationException(""Unexpected published Linux query-plan routing."");
    }
}
System.Console.WriteLine(""Test app for verifying Cosmos SDK package behavior"");");

            // Publish the project
            string publishDir = Path.Combine(projectDir, "bin", "publish", runtimeIdentifier ?? "no-rid");
            string ridArgument = runtimeIdentifier != null ? $"-r {runtimeIdentifier} --self-contained false" : string.Empty;
            await RunDotnetCommandAsync(
                $"publish \"{projectFile}\" -c Release -o \"{publishDir}\" {ridArgument}".Trim(),
                timeoutMinutes: 5);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && (runtimeIdentifier == null
                    || (runtimeIdentifier == "linux-x64" && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    || (runtimeIdentifier == "linux-arm64" && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)))
            {
                await RunDotnetCommandAsync($"\"{Path.Combine(publishDir, projectName + ".dll")}\" --verify-linux-query-planning");
                string libraryPath = runtimeIdentifier != null
                    ? Path.Combine(publishDir, LinuxLibraryName)
                    : Path.Combine(
                        publishDir,
                        "runtimes",
                        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64",
                        "native",
                        LinuxLibraryName);
                string unavailablePath = libraryPath + ".disabled";
                File.Move(libraryPath, unavailablePath);
                try
                {
                    await RunDotnetCommandAsync($"\"{Path.Combine(publishDir, projectName + ".dll")}\" --verify-linux-query-planning-fallback");
                }
                finally
                {
                    File.Move(unavailablePath, libraryPath);
                }
            }

            return publishDir;
        }

        private void AssertWindowsDllsNotPresent(string publishPath, string runtimeIdentifier)
        {
            Assert.IsTrue(Directory.Exists(publishPath), $"Publish directory does not exist: {publishPath}");
            this.AssertPublishOutputIsValid(publishPath, runtimeIdentifier);

            foreach (string dll in WindowsNativeDlls)
            {
                string dllPath = Path.Combine(publishPath, dll);
                Assert.IsFalse(File.Exists(dllPath),
                    $"Windows native DLL '{dll}' should NOT be present when publishing for {runtimeIdentifier}, but was found at: {dllPath}");
            }
        }

        private void AssertWindowsDllsPresent(string publishPath, string runtimeIdentifier)
        {
            Assert.IsTrue(Directory.Exists(publishPath), $"Publish directory does not exist: {publishPath}");
            this.AssertPublishOutputIsValid(publishPath, runtimeIdentifier);

            foreach (string dll in WindowsNativeDlls)
            {
                string dllPath = Path.Combine(publishPath, dll);
                Assert.IsTrue(File.Exists(dllPath),
                    $"Unexpected native payload '{dll}' for {runtimeIdentifier}: {dllPath}");
            }
        }

        private void AssertPublishOutputIsValid(string publishPath, string runtimeIdentifier)
        {
            string[] publishedFiles = Directory.GetFiles(publishPath);
            Assert.IsTrue(publishedFiles.Length > 0,
                $"Publish directory is empty for {runtimeIdentifier}. Publish may have silently failed: {publishPath}");

            string sdkDllPath = Path.Combine(publishPath, "Microsoft.Azure.Cosmos.Client.dll");
            Assert.IsTrue(File.Exists(sdkDllPath),
                $"Microsoft.Azure.Cosmos.Client.dll not found in publish output for {runtimeIdentifier}. " +
                $"Publish may not have included the SDK package correctly: {publishPath}");
        }

        private static string GetRepositoryRoot()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;

            while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Microsoft.Azure.Cosmos.sln")))
            {
                DirectoryInfo parent = Directory.GetParent(currentDir);
                if (parent == null)
                {
                    break;
                }
                currentDir = parent.FullName;
            }

            Assert.IsNotNull(currentDir, "Could not find repository root");
            return currentDir;
        }

        private static async Task<string> RunDotnetCommandAsync(string arguments, int timeoutMinutes = 5)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Reused build nodes can keep redirected output pipes open after dotnet exits.
            processInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

            string commandLine = $"dotnet {arguments}";
            Console.WriteLine($"Executing: {commandLine}");

            Process process = Process.Start(processInfo);
            if (process == null)
            {
                Assert.Fail($"Failed to start process: {commandLine}");
            }

            using (process)
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                bool exited = process.WaitForExit((int)TimeSpan.FromMinutes(timeoutMinutes).TotalMilliseconds);
                if (!exited)
                {
                    process.Kill();
                    Assert.Fail($"Command timed out after {timeoutMinutes} minutes.\nCommand: {commandLine}");
                }

                string output = await outputTask;
                string error = await errorTask;

                if (process.ExitCode != 0)
                {
                    Assert.Fail($"Command failed with exit code {process.ExitCode}.\nCommand: {commandLine}\nOutput: {output}\nError: {error}");
                }

                Assert.IsTrue(string.IsNullOrEmpty(error), $"Command had unexpected error output.\nCommand: {commandLine}\nError: {error}");
                Console.WriteLine($"Command succeeded: {commandLine}");
                return output;
            }
        }
    }
}
