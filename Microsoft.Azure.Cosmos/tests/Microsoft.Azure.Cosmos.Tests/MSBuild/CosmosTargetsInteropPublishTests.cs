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
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
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
        private const string WindowsArm64LibraryName = "Cosmos.QueryPlanInterop.dll";
        private const string CommandOutputLogEnvironmentVariable = "COSMOS_TEST_DOTNET_OUTPUT_LOG";
        private static readonly object CommandOutputLock = new object();
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
        [DataRow("ARM64", null)]
        [DataRow("AnyCPU", null)]
        [DataRow(null, null)]
        [DataRow("ARM64", "win-arm64")]
        public async Task Publish_NetFramework_CopiesArm64PlannerBesideManagedAssemblies(string platformTarget, string runtimeIdentifier)
        {
            string projectName = $"Framework_{platformTarget ?? "ImplicitAnyCPU"}_{runtimeIdentifier ?? "NoRid"}";
            string publishPath = await this.CreateAndPublishTestProjectAsync(
                projectName, runtimeIdentifier, targetFramework: "net481", platformTarget: platformTarget);
            string buildAssembly = Directory.GetFiles(
                Path.Combine(testProjectsRoot, projectName, "bin", "Release"),
                "Microsoft.Azure.Cosmos.Client.dll",
                SearchOption.AllDirectories).Single();

            foreach (string outputPath in new[] { Path.GetDirectoryName(buildAssembly), publishPath })
            {
                string[] libraries = Directory.GetFiles(outputPath, WindowsArm64LibraryName, SearchOption.AllDirectories);
                CollectionAssert.AreEqual(
                    new[] { Path.Combine(outputPath, WindowsArm64LibraryName) },
                    libraries,
                    ".NET Framework must resolve the ARM64 planner without CoreCLR's RID-aware probing.");
            }
        }

        [TestMethod]
        [DataRow("linux-x64")]
        [DataRow("linux-arm64")]
        [DataRow("win-x64")]
        [DataRow("win-arm64")]
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
                Assert.AreEqual(
                    runtimeIdentifier == "linux-arm64" ? (ushort)183 : (ushort)62,
                    reader.ReadUInt16(),
                    "Source-reference publish must include the requested architecture's ELF library.");
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task Direct_MetadataMatchesAssemblyVersion(bool isPreview, bool isNightly)
        {
            string directProjectPath = Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "Direct", "src", "Microsoft.Azure.Cosmos.Direct.csproj");
            string arguments = $"-nologo -p:IsPreview={isPreview} -p:IsNightly={isNightly} "
                + "-getProperty:Version,FileVersion,InformationalVersion,AssemblyVersion";
            JObject directProperties = (JObject)JObject.Parse(await RunDotnetCommandAsync(
                $"msbuild \"{directProjectPath}\" {arguments}"))["Properties"];

            foreach (string property in new[] { "Version", "FileVersion", "InformationalVersion", "AssemblyVersion" })
            {
                Assert.AreEqual("3.44.4.0", (string)directProperties[property], $"Direct's {property} must remain independent of SDK release versions.");
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
        [DoNotParallelize]
        public async Task CommandTimeoutPreservesCapturedOutput()
        {
            const string projectName = "CommandTimeoutProbe";
            string publishPath = await this.CreateAndPublishTestProjectAsync(projectName, runtimeIdentifier: null);
            string originalLog = Environment.GetEnvironmentVariable(CommandOutputLogEnvironmentVariable);
            string progressLog = string.IsNullOrEmpty(originalLog)
                ? Path.Combine(testProjectsRoot, "command-output.log")
                : originalLog;
            if (!File.Exists(progressLog))
            {
                File.WriteAllText(progressLog, string.Empty);
            }

            long startOffset = new FileInfo(progressLog).Length;
            Environment.SetEnvironmentVariable(CommandOutputLogEnvironmentVariable, progressLog);
            try
            {
                Task<AssertFailedException> command = Assert.ThrowsExceptionAsync<AssertFailedException>(() =>
                    RunDotnetCommandAsync(
                        $"\"{Path.Combine(publishPath, projectName + ".dll")}\" --simulate-stall",
                        timeoutMinutes: 0.25));
                AssertFailedException exception = null;
                try
                {
                    bool forwarded = false;
                    while (!command.IsCompleted)
                    {
                        using FileStream stream = new FileStream(progressLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        stream.Position = startOffset;
                        using StreamReader reader = new StreamReader(stream);
                        string progress = await reader.ReadToEndAsync();
                        if (progress.Contains("Timeout probe stdout") && progress.Contains("Timeout probe stderr"))
                        {
                            forwarded = true;
                            break;
                        }

                        await Task.Delay(25);
                    }

                    Assert.IsTrue(forwarded, "Both child streams must be forwarded before the command exits.");
                    Assert.IsFalse(command.IsCompleted, "Progress must be visible while the child is still running.");
                }
                finally
                {
                    exception = await command;
                }

                StringAssert.Contains(exception.Message, "Command timed out");
                StringAssert.Contains(exception.Message, "Timeout probe stdout");
                StringAssert.Contains(exception.Message, "Timeout probe stderr");
            }
            finally
            {
                Environment.SetEnvironmentVariable(CommandOutputLogEnvironmentVariable, originalLog);
            }
        }

        [TestMethod]
        public async Task Package_UniqueVersionPreservesAssemblyIdentity()
        {
            string projectPath = Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj");
            string evaluation = await RunDotnetCommandAsync(
                $"msbuild \"{projectPath}\" -nologo -target:GetAssemblyVersion -getProperty:AssemblyVersion");
            Version expectedAssemblyVersion = Version.Parse(evaluation.Trim());
            using ZipArchive package = ZipFile.OpenRead(
                Path.Combine(localNugetPackagePath, $"Microsoft.Azure.Cosmos.{packageVersion}.nupkg"));
            ZipArchiveEntry nuspec = package.GetEntry("Microsoft.Azure.Cosmos.nuspec");
            Assert.IsNotNull(nuspec);
            using (Stream stream = nuspec.Open())
            {
                XDocument metadata = XDocument.Load(stream);
                Assert.AreEqual(packageVersion, metadata.Descendants().Single(element => element.Name.LocalName == "version").Value);
            }

            ZipArchiveEntry assembly = package.GetEntry("lib/netstandard2.0/Microsoft.Azure.Cosmos.Client.dll");
            Assert.IsNotNull(assembly);
            using MemoryStream image = new MemoryStream();
            using (Stream stream = assembly.Open())
            {
                await stream.CopyToAsync(image);
            }

            image.Position = 0;
            using PEReader reader = new PEReader(image);
            Assert.AreEqual(
                expectedAssemblyVersion,
                reader.GetMetadataReader().GetAssemblyDefinition().Version,
                "A unique test package version must not change the SDK's managed assembly identity.");
        }

        [TestMethod]
        public async Task Package_NativePayloadsMatchRestoredPackages()
        {
            string projectPath = Path.Combine(GetRepositoryRoot(), "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj");
            string evaluation = await RunDotnetCommandAsync(
                $"msbuild \"{projectPath}\" -nologo -p:Configuration=Release -getProperty:PkgMicrosoft_Azure_Cosmos_ServiceInterop,PkgMicrosoft_Azure_Cosmos_QueryPlanInterop");
            JObject properties = (JObject)JObject.Parse(evaluation)["Properties"];
            string windowsPackage = (string)properties["PkgMicrosoft_Azure_Cosmos_ServiceInterop"];
            string linuxPackage = (string)properties["PkgMicrosoft_Azure_Cosmos_QueryPlanInterop"];
            Assert.IsFalse(string.IsNullOrEmpty(windowsPackage), "The ServiceInterop package must be restored.");
            Assert.IsFalse(string.IsNullOrEmpty(linuxPackage), "The Linux query-plan package must be restored.");

            using ZipArchive package = ZipFile.OpenRead(
                Path.Combine(localNugetPackagePath, $"Microsoft.Azure.Cosmos.{packageVersion}.nupkg"));
            Assert.AreEqual(
                8,
                package.Entries.Count(entry => entry.FullName.StartsWith("runtimes/", StringComparison.Ordinal)
                    && entry.FullName.Contains("/native/", StringComparison.Ordinal)),
                "The SDK must bundle exactly five Windows x64 DLLs, one Windows ARM64 DLL and two Linux query-plan libraries.");

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

            AssertPackageEntryMatchesFile(
                package,
                $"runtimes/win-arm64/native/{WindowsArm64LibraryName}",
                Path.Combine(linuxPackage, "runtimes", "win-arm64", "native", WindowsArm64LibraryName));
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

            // Avoid NuGet cache conflicts without changing assembly metadata and forcing a rebuild.
            packageVersion = $"99.0.0-test.{DateTime.UtcNow:yyyyMMddHHmmss}";

            await RunDotnetCommandAsync(
                $"pack \"{cosmosProjectPath}\" -c Release -o \"{packOutputDir}\" --verbosity normal /p:PackageVersion={packageVersion}",
                timeoutMinutes: 10);

            localNugetPackagePath = packOutputDir;
        }

        private async Task<string> CreateAndPublishTestProjectAsync(
            string projectName,
            string runtimeIdentifier,
            bool useProjectReference = false,
            string targetFramework = "net8.0",
            string platformTarget = null)
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
    <PackageReference Include=""Microsoft.Azure.Cosmos.QueryPlanInterop"" Version=""[$(QueryPlanInteropVersion)]"" ExcludeAssets=""All"" GeneratePathProperty=""true"" />
    <Content Include=""$(PkgMicrosoft_Azure_Cosmos_QueryPlanInterop)\runtimes\{runtimeIdentifier}\native\{(runtimeIdentifier.StartsWith("win", StringComparison.Ordinal) ? WindowsArm64LibraryName : LinuxLibraryName)}""
             Condition=""'{runtimeIdentifier}' == 'linux-x64' Or '{runtimeIdentifier}' == 'linux-arm64' Or '{runtimeIdentifier}' == 'win-arm64'""
             Link=""{(runtimeIdentifier.StartsWith("win", StringComparison.Ordinal) ? WindowsArm64LibraryName : LinuxLibraryName)}"" CopyToOutputDirectory=""PreserveNewest"" />"
                : string.Empty;
            string sourceVersionImport = useProjectReference
                ? $@"<Import Project=""{Path.Combine(GetRepositoryRoot(), "Directory.Build.props")}"" />"
                : string.Empty;

            // Create a simple console app project that references the SDK.
            File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  {sourceVersionImport}
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>{targetFramework}</TargetFramework>
    <PlatformTarget>{platformTarget}</PlatformTarget>
    <LangVersion>latest</LangVersion>
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
if (System.Array.IndexOf(args, ""--simulate-stall"") >= 0)
{
    using (var process = System.Diagnostics.Process.GetCurrentProcess())
    {
        System.Console.WriteLine(""Timeout probe PID: "" + process.Id);
    }
    System.Console.WriteLine(""Timeout probe stdout"");
    System.Console.Error.WriteLine(""Timeout probe stderr"");
    System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
}
if (args.Length > 1)
{
    throw new System.ArgumentException(""Expected a single verification mode."");
}

const string windowsArm64OptIn = ""AZURE_COSMOS_USE_QUERY_PLAN_INTEROP_WINDOWS_ARM64"";
string mode = args.Length == 0 ? string.Empty : args[0];
string expectedAdapter;
switch (mode)
{
    case """":
        System.Console.WriteLine(""Test app for verifying Cosmos SDK package behavior"");
        return;
    case ""--verify-windows-query-planning"":
        System.Environment.SetEnvironmentVariable(windowsArm64OptIn, ""true"");
        expectedAdapter = ""WindowsX64QueryPlanInterop"";
        break;
    case ""--verify-linux-query-planning"":
    case ""--verify-linux-query-planning-fallback"":
        System.Environment.SetEnvironmentVariable(""AZURE_COSMOS_USE_QUERY_PLAN_INTEROP_LINUX"", ""true"");
        expectedAdapter = ""LinuxQueryPlanInterop"";
        break;
    case ""--verify-windows-arm64-default"":
        System.Environment.SetEnvironmentVariable(windowsArm64OptIn, null);
        expectedAdapter = ""WindowsX64QueryPlanInterop"";
        break;
    case ""--verify-windows-arm64-disabled"":
        System.Environment.SetEnvironmentVariable(windowsArm64OptIn, ""false"");
        expectedAdapter = ""WindowsX64QueryPlanInterop"";
        break;
    case ""--verify-windows-arm64-invalid"":
        System.Environment.SetEnvironmentVariable(windowsArm64OptIn, ""1"");
        expectedAdapter = ""WindowsX64QueryPlanInterop"";
        break;
    case ""--verify-windows-arm64-query-planning"":
    case ""--verify-windows-arm64-fallback"":
    case ""--verify-windows-arm64-force-bypass"":
        System.Environment.SetEnvironmentVariable(windowsArm64OptIn, ""TrUe"");
        expectedAdapter = ""WindowsArm64QueryPlanInterop"";
        break;
    default:
        throw new System.ArgumentException(""Unknown verification mode: "" + mode);
}

bool expectGateway = mode is not (""--verify-windows-query-planning""
    or ""--verify-linux-query-planning""
    or ""--verify-windows-arm64-query-planning"");
System.Environment.SetEnvironmentVariable(""AZURE_COSMOS_BYPASS_QUERY_PARSING"",
    mode == ""--verify-windows-arm64-force-bypass"" ? ""true"" : ""false"");
System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Microsoft.Azure.Cosmos.CosmosClient).TypeHandle);
var sdkAssembly = typeof(Microsoft.Azure.Cosmos.CosmosClient).Assembly;
var factory = sdkAssembly.GetType(""Microsoft.Azure.Cosmos.Query.Core.QueryPlan.QueryPlanInterop"", throwOnError: true)!;
var instance = factory.GetProperty(""Instance"")!.GetValue(null)!;
if (instance.GetType().Name != expectedAdapter)
{
    throw new System.InvalidOperationException(""Unexpected native adapter: "" + instance.GetType().Name);
}

var retriever = sdkAssembly.GetType(""Microsoft.Azure.Cosmos.Query.Core.QueryPlan.QueryPlanRetriever"", throwOnError: true)!;
var bypass = retriever.GetMethod(""BypassQueryParsing"",
    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
if ((bool)bypass.Invoke(null, null)! != expectGateway)
{
    throw new System.InvalidOperationException(""Unexpected published native query-plan routing."");
}

if (!expectGateway)
{
    var providerType = sdkAssembly.GetType(
        ""Microsoft.Azure.Cosmos.Query.Core.QueryPlan.QueryPartitionProvider"", throwOnError: true)!;
    using var provider = (System.IDisposable)System.Activator.CreateInstance(
        providerType,
        new object[] { new System.Collections.Generic.Dictionary<string, object> { { ""maxSqlQueryInputLength"", 524288 } } })!;
    var getPlan = providerType.GetMethod(""TryGetPartitionedQueryExecutionInfo"")!;
    var partitionKey = Newtonsoft.Json.JsonConvert.DeserializeObject(
        ""{\""paths\"":[\""/pk\""],\""kind\"":\""Hash\"",\""version\"":2}"",
        getPlan.GetParameters()[1].ParameterType)!;
    string query = Newtonsoft.Json.JsonConvert.SerializeObject(
        new { query = ""SELECT TOP 1 * FROM c WHERE c.pk = 'partition-\u6771\u4eac\U0001f642'"" });
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

if (mode.StartsWith(""--verify-windows-arm64-"", System.StringComparison.Ordinal))
{
    using var process = System.Diagnostics.Process.GetCurrentProcess();
    System.Diagnostics.ProcessModule? native = null;
    foreach (System.Diagnostics.ProcessModule module in process.Modules)
    {
        if (System.String.Equals(module.ModuleName, ""Cosmos.QueryPlanInterop.dll"", System.StringComparison.OrdinalIgnoreCase))
        {
            native = module;
        }
    }
    if ((native == null) != expectGateway)
    {
        throw new System.InvalidOperationException(""Unexpected Windows ARM64 native library load."");
    }
    if (native != null)
    {
        string expected = System.IO.Path.Combine(System.AppContext.BaseDirectory, ""Cosmos.QueryPlanInterop.dll"");
        if (!System.IO.File.Exists(expected))
        {
            expected = System.IO.Path.Combine(System.AppContext.BaseDirectory, ""runtimes"", ""win-arm64"", ""native"", ""Cosmos.QueryPlanInterop.dll"");
        }
        if (!System.String.Equals(System.IO.Path.GetFullPath(expected), System.IO.Path.GetFullPath(native.FileName), System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.InvalidOperationException(""Unexpected native library path: "" + native.FileName);
        }
    }
    System.Environment.SetEnvironmentVariable(windowsArm64OptIn,
        expectedAdapter == ""WindowsArm64QueryPlanInterop"" ? ""false"" : ""true"");
    if (!System.Object.ReferenceEquals(instance, factory.GetProperty(""Instance"")!.GetValue(null))
        || (bool)bypass.Invoke(null, null)! != expectGateway)
    {
        throw new System.InvalidOperationException(""Native backend or routing changed after initialization."");
    }
}
System.Console.WriteLine(""Test app for verifying Cosmos SDK package behavior"");");

            // Publish the project
            string publishDir = Path.Combine(projectDir, "bin", "publish", runtimeIdentifier ?? "no-rid");
            string ridArgument = runtimeIdentifier != null ? $"-r {runtimeIdentifier} --self-contained false" : string.Empty;
            // Keep RID-based native-asset selection without rebuilding the AnyCPU libraries into new RID directories.
            string sourceArguments = useProjectReference
                ? "--verbosity normal /p:AppendRuntimeIdentifierToOutputPath=false"
                : string.Empty;
            await RunDotnetCommandAsync(
                $"publish \"{projectFile}\" -c Release -o \"{publishDir}\" {ridArgument} {sourceArguments}".Trim(),
                timeoutMinutes: 5);

            if (targetFramework == "net8.0"
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
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

            if (targetFramework == "net8.0"
                && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                && (runtimeIdentifier == null || runtimeIdentifier == "win-arm64"))
            {
                string consumer = $"\"{Path.Combine(publishDir, projectName + ".dll")}\"";
                foreach (string mode in new[] { "default", "disabled", "invalid", "force-bypass", "query-planning" })
                {
                    await RunDotnetCommandAsync($"{consumer} --verify-windows-arm64-{mode}");
                }

                string libraryPath = runtimeIdentifier == null
                    ? Path.Combine(publishDir, "runtimes", "win-arm64", "native", WindowsArm64LibraryName)
                    : Path.Combine(publishDir, WindowsArm64LibraryName);
                string unavailablePath = libraryPath + ".disabled";
                File.Move(libraryPath, unavailablePath);
                try
                {
                    await RunDotnetCommandAsync($"{consumer} --verify-windows-arm64-fallback");
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

            Assert.AreEqual(0, Directory.GetFiles(publishPath, WindowsArm64LibraryName, SearchOption.AllDirectories).Length,
                "Windows QueryPlanInterop must not be published for non-Windows runtimes.");

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

            string[] arm64Libraries = Directory.GetFiles(publishPath, WindowsArm64LibraryName, SearchOption.AllDirectories);
            bool includesArm64 = runtimeIdentifier == "win-arm64" || runtimeIdentifier == "no RuntimeIdentifier";
            Assert.AreEqual(includesArm64 ? 1 : 0, arm64Libraries.Length, "Do not publish QueryPlanInterop for Windows x64 or x86.");
            if (includesArm64)
            {
                string expected = runtimeIdentifier == "win-arm64"
                    ? Path.Combine(publishPath, WindowsArm64LibraryName)
                    : Path.Combine(publishPath, "runtimes", "win-arm64", "native", WindowsArm64LibraryName);
                Assert.AreEqual(expected, arm64Libraries[0]);
                using BinaryReader reader = new BinaryReader(File.OpenRead(expected));
                reader.BaseStream.Position = 0x3c;
                uint peOffset = reader.ReadUInt32();
                reader.BaseStream.Position = peOffset;
                Assert.AreEqual(0x00004550U, reader.ReadUInt32(), "Expected a PE native library.");
                Assert.AreEqual((ushort)0xaa64, reader.ReadUInt16(), "Expected native Windows ARM64, not x64 or ARM64EC.");
            }

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

        private static async Task<string> RunDotnetCommandAsync(string arguments, double timeoutMinutes = 5)
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

            TimeSpan timeout = TimeSpan.FromMinutes(timeoutMinutes);
            string commandLine = $"dotnet {arguments}";
            WriteCommandOutput($"Executing (timeout {timeout.TotalMinutes} minutes): {commandLine}{Environment.NewLine}");

            Process process = Process.Start(processInfo);
            if (process == null)
            {
                Assert.Fail($"Failed to start process: {commandLine}");
            }

            using (process)
            {
                Task<string> outputTask = CaptureCommandOutputAsync(process.StandardOutput, isError: false);
                Task<string> errorTask = CaptureCommandOutputAsync(process.StandardError, isError: true);

                bool timedOut = false;
                using CancellationTokenSource cancellation = new CancellationTokenSource(timeout);
                try
                {
                    await process.WaitForExitAsync(cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    timedOut = true;
                    WriteCommandOutput($"Command timed out; terminating process tree: {commandLine}{Environment.NewLine}", isError: true);
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                        // The process exited while the timeout was firing.
                    }

                    await process.WaitForExitAsync();
                }

                string output = await outputTask;
                string error = await errorTask;

                if (timedOut)
                {
                    Assert.Fail($"Command timed out after {timeout.TotalMinutes} minutes.\nCommand: {commandLine}\nOutput: {output}\nError: {error}");
                }

                if (process.ExitCode != 0)
                {
                    Assert.Fail($"Command failed with exit code {process.ExitCode}.\nCommand: {commandLine}\nOutput: {output}\nError: {error}");
                }

                Assert.IsTrue(string.IsNullOrEmpty(error), $"Command had unexpected error output.\nCommand: {commandLine}\nError: {error}");
                WriteCommandOutput($"Command succeeded: {commandLine}{Environment.NewLine}");
                return output;
            }
        }

        private static async Task<string> CaptureCommandOutputAsync(StreamReader reader, bool isError)
        {
            StringBuilder captured = new StringBuilder();
            char[] buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                string text = new string(buffer, 0, count);
                captured.Append(text);
                WriteCommandOutput(text, isError);
            }

            return captured.ToString();
        }

        private static void WriteCommandOutput(string text, bool isError = false)
        {
            lock (CommandOutputLock)
            {
                TextWriter console = isError ? Console.Error : Console.Out;
                console.Write(text);
                console.Flush();

                // MSTest buffers Console output; CI tails this optional log independently.
                string logPath = Environment.GetEnvironmentVariable(CommandOutputLogEnvironmentVariable);
                if (!string.IsNullOrEmpty(logPath))
                {
                    File.AppendAllText(logPath, text);
                }
            }
        }
    }
}