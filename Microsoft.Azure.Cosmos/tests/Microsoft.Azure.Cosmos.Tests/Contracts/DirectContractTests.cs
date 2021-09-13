//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestCategory("Windows")]
    [TestClass]
    public class DirectContractTests
    {
        [TestMethod]
        public void TestInteropTest()
        {
            try
            {
                CosmosClient client = new CosmosClient(connectionString: null);
                Assert.Fail();
            }
            catch (ArgumentNullException)
            {
            }

            Assert.IsTrue(ServiceInteropWrapper.AssembliesExist.Value);

            string configJson = "{}";
            IntPtr provider;
            uint result = ServiceInteropWrapper.CreateServiceProvider(configJson, out provider);
        }

        [TestMethod]
        public void PublicDirectTypes()
        {
            Assembly directAssembly = typeof(IStoreClient).Assembly;

            Assert.IsTrue(directAssembly.FullName.StartsWith("Microsoft.Azure.Cosmos.Direct", System.StringComparison.Ordinal), directAssembly.FullName);

            Type[] exportedTypes = directAssembly.GetExportedTypes();
            Assert.AreEqual(0, exportedTypes.Length, string.Join(",", exportedTypes.Select(e => e.Name).ToArray()));
        }

        [TestMethod]
        public void MappedRegionsTest()
        {
            string[] cosmosRegions = typeof(Regions)
                            .GetMembers(BindingFlags.Static | BindingFlags.Public)
                            .Select(e => e.Name)
                            .ToArray();

            string[] locationNames = typeof(LocationNames)
                            .GetMembers(BindingFlags.Static | BindingFlags.Public)
                            .Select(e => e.Name)
                            .ToArray();

            if (locationNames.Length > cosmosRegions.Length)
            {
                HashSet<string> missingLocationNames = new HashSet<string>(locationNames);
                foreach (string region in cosmosRegions)
                {
                    missingLocationNames.Remove(region);
                }

                Assert.Fail($"Missing regions from Cosmos.Regions: {string.Join(";", missingLocationNames)}");
            }

            CollectionAssert.AreEquivalent(locationNames, cosmosRegions);
        }

        [TestMethod]
        public void RMContractTest()
        {
            Trace.TraceInformation($"{Documents.RMResources.PartitionKeyAndEffectivePartitionKeyBothSpecified} " +
                $"{Documents.RMResources.UnexpectedPartitionKeyRangeId}");
        }

        [TestMethod]
        public void CustomJsonReaderTest()
        {
            // Contract validation that JsonReaderFactory is present 
            DocumentServiceResponse.JsonReaderFactory = (stream) => null;
        }

        [TestMethod]
        public void ProjectPackageDependenciesTest()
        {
            string csprojFile = "Microsoft.Azure.Cosmos.csproj";
            Dictionary<string, Version> projectDependencies = DirectContractTests.GetPackageReferencies(csprojFile);
            Dictionary<string, Version> baselineDependencies = new Dictionary<string, Version>()
            {
                { "System.Collections.Immutable", new Version(1, 7, 0) },
                { "System.Numerics.Vectors", new Version(4, 5, 0) },
                { "Newtonsoft.Json", new Version(10, 0, 2) },
                { "Microsoft.Bcl.AsyncInterfaces", new Version(1, 0, 0) },
                { "System.Configuration.ConfigurationManager", new Version(4, 5, 0) },
                { "System.Memory", new Version(4, 5, 4) },
                { "System.Buffers", new Version(4, 5, 1) },
                { "System.Runtime.CompilerServices.Unsafe", new Version(4, 5, 3) },
                { "System.Threading.Tasks.Extensions", new Version(4, 5, 4) },
                { "System.ValueTuple", new Version(4, 5, 0) },
                { "Microsoft.Bcl.HashCode", new Version(1, 1, 0) },
                { "Azure.Core", new Version(1, 3, 0) },
            };

            Assert.AreEqual(projectDependencies.Count, baselineDependencies.Count);
            foreach (KeyValuePair<string, Version> projectDependency in projectDependencies)
            {
                Version baselineVersion = baselineDependencies[projectDependency.Key];
                Assert.AreEqual(baselineVersion, projectDependency.Value);
            }
        }

        [TestMethod]
        public void PackageDependenciesTest()
        {
            string csprojFile = "Microsoft.Azure.Cosmos.csproj";
            Dictionary<string, Version> projDependencies = DirectContractTests.GetPackageReferencies(csprojFile);

            string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.nuspec");
            Dictionary<string, Version> allDependencies = new Dictionary<string, Version>();
            foreach (string nuspecFile in files)
            {
                Dictionary<string, Version> nuspecDependencies = DirectContractTests.GetNuspecDependencies(nuspecFile);
                foreach (KeyValuePair<string, Version> e in nuspecDependencies)
                {
                    if (!allDependencies.ContainsKey(e.Key))
                    {
                        allDependencies[e.Key] = e.Value;
                    }
                    else
                    {
                        Version existingValue = allDependencies[e.Key];
                        if (existingValue.CompareTo(e.Value) > 0)
                        {
                            allDependencies[e.Key] = e.Value;
                        }
                    }
                }
            }

            // Dependency version should match
            foreach (KeyValuePair<string, Version> e in allDependencies)
            {
                Assert.AreEqual(e.Value, projDependencies[e.Key], e.Key);
            }

            CollectionAssert.IsSubsetOf(allDependencies.Keys, projDependencies.Keys);
        }

        private static Dictionary<string, Version> GetPackageReferencies(string csprojName)
        {
            string fullCsprojName = Path.Combine(Directory.GetCurrentDirectory(), csprojName);
            Trace.TraceInformation($"Testing dependencies for csporj file {fullCsprojName}");
            string projContent = File.ReadAllText(fullCsprojName);

            Regex projRefMatcher = new Regex("<PackageReference\\s+Include=\"(?<Include>[^\"]*)\"\\s+Version=\"(?<Version>[^\"]*)\"\\s+(PrivateAssets=\"(?<PrivateAssets>[^\"]*)\")?");
            MatchCollection matches = projRefMatcher.Matches(projContent);

            int prjRefCount = new Regex("<PackageReference").Matches(projContent).Count;
            Assert.AreEqual(prjRefCount, matches.Count, "CSPROJ PackageReference regex is broken");

            Dictionary<string, Version> projReferences = new Dictionary<string, Version>();
            foreach (Match m in matches)
            {
                if (m.Groups["PrivateAssets"].Captures.Count != 0)
                {
                    Assert.AreEqual("All", m.Groups["PrivateAssets"].Value, $"{m.Groups["Include"].Value}");
                }
                else
                {
                    projReferences[m.Groups["Include"].Value] = Version.Parse(m.Groups["Version"].Value);
                }
            }

            return projReferences;
        }

        private static Dictionary<string, Version> GetNuspecDependencies(string nuspecFile)
        {
            Trace.TraceInformation($"Testing dependencies for nuspec file {nuspecFile}");
            string nuspecContent = File.ReadAllText(nuspecFile);

            Regex regexDepMatcher = new Regex("<dependency\\s+id=\"(?<id>[^\"]*)\"\\s+version=\"(?<version>[^\"]*)\"");
            MatchCollection matches = regexDepMatcher.Matches(nuspecContent);

            int dependencyCount = new Regex("<dependency").Matches(nuspecContent).Count;
            Assert.AreEqual(dependencyCount, matches.Count, "Nuspec dependency regex is broken");

            Dictionary<string, Version> dependencies = new Dictionary<string, Version>();
            foreach (Match m in matches)
            {
                dependencies[m.Groups["id"].Value] = Version.Parse(m.Groups["version"].Value);
            }

            return dependencies;
        }
    }
}
