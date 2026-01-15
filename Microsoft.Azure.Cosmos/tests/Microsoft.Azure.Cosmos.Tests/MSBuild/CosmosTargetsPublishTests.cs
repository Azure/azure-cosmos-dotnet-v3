//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.MSBuild
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests that verify Windows native DLLs are only copied when publishing
    /// for Windows RuntimeIdentifiers, and not for Linux/macOS targets.
    /// </summary>
    [TestClass]
    public class CosmosTargetsPublishTests
    {
        private static string testProjectsRoot;
        private static readonly string[] WindowsNativeDlls = new[]
        {
            "Microsoft.Azure.Cosmos.ServiceInterop.dll",
            "Cosmos.CRTCompat.dll",
            "msvcp140.dll",
            "vcruntime140.dll",
            "vcruntime140_1.dll"
        };

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            testProjectsRoot = Path.Combine(Path.GetTempPath(), "CosmosTargetsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testProjectsRoot);
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
        public void Publish_WithNonWindowsRuntimeIdentifier_DoesNotCopyWindowsDlls(string runtimeIdentifier)
        {
            string projectPath = this.CreateTestProject($"NonWinTest_{runtimeIdentifier}");
            string publishPath = this.PublishProject(projectPath, runtimeIdentifier);

            this.AssertWindowsDllsNotPresent(publishPath, runtimeIdentifier);
        }

        /// <summary>
        /// Tests that Windows native DLLs are copied when publishing for Windows platforms.
        /// </summary>
        /// <param name="runtimeIdentifier">The runtime identifier to test (e.g., win-x64, win-x86).</param>
        [TestMethod]
        [DataRow("win-x64")]
        [DataRow("win-x86")]
        [DataRow("win-arm64")]
        public void Publish_WithWindowsRuntimeIdentifier_CopiesWindowsDlls(string runtimeIdentifier)
        {
            string projectPath = this.CreateTestProject($"WinTest_{runtimeIdentifier}");
            string publishPath = this.PublishProject(projectPath, runtimeIdentifier);

            this.AssertWindowsDllsPresent(publishPath, runtimeIdentifier);
        }

        private string CreateTestProject(string projectName)
        {
            string projectDir = Path.Combine(testProjectsRoot, projectName);
            Directory.CreateDirectory(projectDir);

            string projectFile = Path.Combine(projectDir, $"{projectName}.csproj");
            string programFile = Path.Combine(projectDir, "Program.cs");

            // Get path to the Cosmos SDK project for reference
            string repoRoot = GetRepositoryRoot();
            string cosmosProjectPath = Path.Combine(repoRoot, "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj");

            // Create a simple console app project that references Microsoft.Azure.Cosmos
            File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""{cosmosProjectPath}"" />
  </ItemGroup>
</Project>");

            // Create a minimal Program.cs that uses CosmosClient to ensure the SDK is properly referenced
            File.WriteAllText(programFile, @"using System;
using Microsoft.Azure.Cosmos;

class Program
{
    static void Main()
    {
        // Create a dummy CosmosClient to ensure the SDK is used and targets file is applied
        var client = new CosmosClient(""AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="");
        Console.WriteLine($""CosmosClient created: {client != null}"");
    }
}");

            return projectFile;
        }

        private string PublishProject(string projectFile, string runtimeIdentifier)
        {
            string projectDir = Path.GetDirectoryName(projectFile);
            string publishDir = Path.Combine(projectDir, "bin", "publish", runtimeIdentifier);

            // Run dotnet publish
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{projectFile}\" -r {runtimeIdentifier} -c Release -o \"{publishDir}\" --self-contained false",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Trace the command being executed for debugging
            string commandLine = $"{processInfo.FileName} {processInfo.Arguments}";
            Console.WriteLine($"Executing: {commandLine}");
            Console.WriteLine($"Working directory: {projectDir}");

            using (Process process = Process.Start(processInfo))
            {
                if (process == null)
                {
                    Assert.Fail($"Failed to start dotnet publish process for {runtimeIdentifier}");
                }

                // .NET 6 compatibility: WaitForExit doesn't support TimeSpan parameter
                process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
                
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (process.ExitCode != 0)
                {
                    Assert.Fail($"dotnet publish failed for {runtimeIdentifier}.\nCommand: {commandLine}\nOutput: {output}\nError: {error}");
                }
                else
                {
                    Console.WriteLine($"Publish succeeded for {runtimeIdentifier}. Exit code: {process.ExitCode}");
                }
            }

            return publishDir;
        }

        private void AssertWindowsDllsNotPresent(string publishPath, string runtimeIdentifier)
        {
            Assert.IsTrue(Directory.Exists(publishPath), $"Publish directory does not exist: {publishPath}");

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

            foreach (string dll in WindowsNativeDlls)
            {
                string dllPath = Path.Combine(publishPath, dll);
                Assert.IsTrue(File.Exists(dllPath),
                    $"Windows native DLL '{dll}' SHOULD be present when publishing for {runtimeIdentifier}, but was NOT found at: {dllPath}");
            }
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
    }
}
