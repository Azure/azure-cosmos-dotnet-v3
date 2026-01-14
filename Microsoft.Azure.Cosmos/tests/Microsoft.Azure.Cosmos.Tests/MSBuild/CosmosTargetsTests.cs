//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.MSBuild
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for Microsoft.Azure.Cosmos.targets MSBuild file to verify conditional
    /// inclusion of Windows native DLLs based on RuntimeIdentifier
    /// </summary>
    [TestClass]
    public class CosmosTargetsTests
    {
        private static readonly string TargetsFilePath = Path.Combine(
            GetRepositoryRoot(),
            "Microsoft.Azure.Cosmos",
            "src",
            "Microsoft.Azure.Cosmos.targets");

        [TestMethod]
        [TestCategory("MSBuild")]
        public void TargetsFile_Exists()
        {
            Assert.IsTrue(File.Exists(TargetsFilePath), 
                $"Microsoft.Azure.Cosmos.targets file not found at {TargetsFilePath}");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void TargetsFile_ContainsConditionalItemGroup()
        {
            XDocument doc = XDocument.Load(TargetsFilePath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            // Find ItemGroup with condition for Windows RuntimeIdentifier
            var itemGroups = doc.Descendants(ns + "ItemGroup")
                .Where(ig => ig.Attribute("Condition") != null)
                .ToList();

            Assert.IsTrue(itemGroups.Any(), 
                "No conditional ItemGroup found in targets file");

            // Verify the condition checks for RuntimeIdentifier starting with 'win'
            var windowsItemGroup = itemGroups.FirstOrDefault(ig => 
                ig.Attribute("Condition")?.Value.Contains("RuntimeIdentifier") == true &&
                ig.Attribute("Condition")?.Value.Contains("StartsWith('win')") == true);

            Assert.IsNotNull(windowsItemGroup, 
                "ItemGroup with condition for Windows RuntimeIdentifier not found");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void TargetsFile_ContainsAllNativeDlls()
        {
            XDocument doc = XDocument.Load(TargetsFilePath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            // Get the ItemGroup with Windows condition
            var windowsItemGroup = doc.Descendants(ns + "ItemGroup")
                .FirstOrDefault(ig => 
                    ig.Attribute("Condition")?.Value.Contains("RuntimeIdentifier") == true &&
                    ig.Attribute("Condition")?.Value.Contains("StartsWith('win')") == true);

            Assert.IsNotNull(windowsItemGroup, "Windows ItemGroup not found");

            var contentItems = windowsItemGroup.Descendants(ns + "ContentWithTargetPath").ToList();

            // Verify all expected Windows native DLLs are included
            var expectedDlls = new[]
            {
                "Microsoft.Azure.Cosmos.ServiceInterop.dll",
                "Cosmos.CRTCompat.dll",
                "msvcp140.dll",
                "vcruntime140.dll",
                "vcruntime140_1.dll"
            };

            foreach (var expectedDll in expectedDlls)
            {
                var dllItem = contentItems.FirstOrDefault(ci => 
                    ci.Attribute("Include")?.Value.Contains(expectedDll) == true);

                Assert.IsNotNull(dllItem, 
                    $"Expected DLL '{expectedDll}' not found in conditional ItemGroup");

                // Verify it has the correct target path
                var targetPath = dllItem.Descendants(ns + "TargetPath").FirstOrDefault();
                Assert.IsNotNull(targetPath, 
                    $"TargetPath not specified for {expectedDll}");
                Assert.AreEqual(expectedDll, targetPath.Value, 
                    $"TargetPath mismatch for {expectedDll}");
            }

            Assert.AreEqual(expectedDlls.Length, contentItems.Count, 
                "Unexpected number of DLLs in conditional ItemGroup");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void TargetsFile_ConditionChecksRuntimeIdentifierNotEmpty()
        {
            XDocument doc = XDocument.Load(TargetsFilePath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            var windowsItemGroup = doc.Descendants(ns + "ItemGroup")
                .FirstOrDefault(ig => 
                    ig.Attribute("Condition")?.Value.Contains("RuntimeIdentifier") == true &&
                    ig.Attribute("Condition")?.Value.Contains("StartsWith('win')") == true);

            Assert.IsNotNull(windowsItemGroup, "Windows ItemGroup not found");

            string condition = windowsItemGroup.Attribute("Condition")?.Value;
            Assert.IsNotNull(condition, "Condition attribute not found");

            // Verify condition checks RuntimeIdentifier is not empty
            Assert.IsTrue(condition.Contains("'$(RuntimeIdentifier)' != ''"), 
                "Condition should check that RuntimeIdentifier is not empty");

            // Verify condition uses AND operator
            Assert.IsTrue(condition.Contains(" AND "), 
                "Condition should use AND to combine checks");
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void TargetsFile_DllsAreFromWinX64NativeFolder()
        {
            XDocument doc = XDocument.Load(TargetsFilePath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            var contentItems = doc.Descendants(ns + "ContentWithTargetPath")
                .Where(ci => ci.Attribute("Include")?.Value.Contains(".dll") == true)
                .ToList();

            foreach (var item in contentItems)
            {
                string includePath = item.Attribute("Include")?.Value;
                Assert.IsNotNull(includePath, "Include attribute not found");

                // Verify all DLLs come from runtimes\win-x64\native folder
                Assert.IsTrue(includePath.Contains(@"runtimes\win-x64\native") || 
                             includePath.Contains("runtimes/win-x64/native"), 
                    $"DLL should be from runtimes\\win-x64\\native folder: {includePath}");
            }
        }

        [TestMethod]
        [TestCategory("MSBuild")]
        public void TargetsFile_NoUnconditionalDllInclusions()
        {
            XDocument doc = XDocument.Load(TargetsFilePath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            // Find any ItemGroup without a condition that contains DLLs from win-x64
            var unconditionalItemGroups = doc.Descendants(ns + "ItemGroup")
                .Where(ig => ig.Attribute("Condition") == null)
                .ToList();

            foreach (var itemGroup in unconditionalItemGroups)
            {
                var winDlls = itemGroup.Descendants(ns + "ContentWithTargetPath")
                    .Where(ci => 
                        ci.Attribute("Include")?.Value.Contains(@"runtimes\win-x64\native") == true ||
                        ci.Attribute("Include")?.Value.Contains("runtimes/win-x64/native") == true)
                    .ToList();

                Assert.AreEqual(0, winDlls.Count, 
                    "Found Windows native DLLs in unconditional ItemGroup - they should be conditional");
            }
        }

        private static string GetRepositoryRoot()
        {
            // Navigate up from the test assembly location to find the repository root
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
