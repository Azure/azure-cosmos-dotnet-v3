//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.MSBuild
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests that verify native assets match the published RuntimeIdentifier,
    /// including Linux native loading and fallback when the library is unavailable.
    /// These tests pack the SDK into a local NuGet package to properly test the .targets file behavior.
    /// </summary>
    [TestClass]
    [TestCategory("LongRunning")]
    public class CosmosTargetsInteropPublishTests
    {
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
            string[] linuxLibraries = Directory.GetFiles(publishPath, "libQueryPlanInterop.*.so", SearchOption.AllDirectories);
            if (runtimeIdentifier == "linux-x64" || runtimeIdentifier == "linux-arm64")
            {
                string libraryName = runtimeIdentifier == "linux-x64" ? "libQueryPlanInterop.amd64.so" : "libQueryPlanInterop.arm64.so";
                Assert.AreEqual(1, linuxLibraries.Length, "Only the matching Linux architecture should be published.");
                Assert.AreEqual(libraryName, Path.GetFileName(linuxLibraries[0]));
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
            Assert.AreEqual(0, Directory.GetFiles(publishPath, "libQueryPlanInterop.*.so", SearchOption.AllDirectories).Length);
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
            Assert.IsTrue(File.Exists(Path.Combine(publishPath, "runtimes", "linux-x64", "native", "libQueryPlanInterop.amd64.so")));
            Assert.IsTrue(File.Exists(Path.Combine(publishPath, "runtimes", "linux-arm64", "native", "libQueryPlanInterop.arm64.so")));
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

        private async Task<string> CreateAndPublishTestProjectAsync(string projectName, string runtimeIdentifier)
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

            // Create a simple console app project that references the local NuGet package
            File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.Azure.Cosmos"" Version=""{packageVersion}"" />
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>");

            // Create a minimal Program.cs
            File.WriteAllText(programFile, @"
bool expectGateway = System.Array.IndexOf(args, ""--verify-linux-query-planning-fallback"") >= 0;
if (expectGateway || System.Array.IndexOf(args, ""--verify-linux-query-planning"") >= 0)
{
    System.Environment.SetEnvironmentVariable(""AZURE_COSMOS_USE_QUERY_INTEROP"", ""true"");
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
                string libraryName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "libQueryPlanInterop.arm64.so"
                    : "libQueryPlanInterop.amd64.so";
                string libraryPath = Directory.GetFiles(publishDir, libraryName, SearchOption.AllDirectories).Single();
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
                    $"Windows native DLL '{dll}' SHOULD be present when publishing for {runtimeIdentifier}, but was NOT found at: {dllPath}");
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

        private static async Task RunDotnetCommandAsync(string arguments, int timeoutMinutes = 5)
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
            }
        }
    }
}
