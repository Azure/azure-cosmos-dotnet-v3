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
        public static void ClassInitialize(TestContext context)
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

        [TestMethod]
        [TestCategory("MSBuild")]
        public void Publish_WithLinuxX64RuntimeIdentifier_DoesNotCopyWindowsDlls()
        {
            string projectPath = CreateTestProject("LinuxX64Test");
            string publishPath = PublishProject(projectPath, "linux-x64");

            AssertWindowsDllsNotPresent(publishPath, "linux-x64");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void Publish_WithLinuxArm64RuntimeIdentifier_DoesNotCopyWindowsDlls()
        {
            string projectPath = CreateTestProject("LinuxArm64Test");
            string publishPath = PublishProject(projectPath, "linux-arm64");

            AssertWindowsDllsNotPresent(publishPath, "linux-arm64");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void Publish_WithOsxX64RuntimeIdentifier_DoesNotCopyWindowsDlls()
        {
            string projectPath = CreateTestProject("OsxX64Test");
            string publishPath = PublishProject(projectPath, "osx-x64");

            AssertWindowsDllsNotPresent(publishPath, "osx-x64");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void Publish_WithOsxArm64RuntimeIdentifier_DoesNotCopyWindowsDlls()
        {
            string projectPath = CreateTestProject("OsxArm64Test");
            string publishPath = PublishProject(projectPath, "osx-arm64");

            AssertWindowsDllsNotPresent(publishPath, "osx-arm64");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void Publish_WithWinX64RuntimeIdentifier_CopiesWindowsDlls()
        {
            string projectPath = CreateTestProject("WinX64Test");
            string publishPath = PublishProject(projectPath, "win-x64");

            AssertWindowsDllsPresent(publishPath, "win-x64");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void Publish_WithWinX86RuntimeIdentifier_CopiesWindowsDlls()
        {
            string projectPath = CreateTestProject("WinX86Test");
            string publishPath = PublishProject(projectPath, "win-x86");

            AssertWindowsDllsPresent(publishPath, "win-x86");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void Publish_WithWinArm64RuntimeIdentifier_CopiesWindowsDlls()
        {
            string projectPath = CreateTestProject("WinArm64Test");
            string publishPath = PublishProject(projectPath, "win-arm64");

            AssertWindowsDllsPresent(publishPath, "win-arm64");
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
            var processInfo = new ProcessStartInfo
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

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit(TimeSpan.FromMinutes(5).Milliseconds);
                
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
