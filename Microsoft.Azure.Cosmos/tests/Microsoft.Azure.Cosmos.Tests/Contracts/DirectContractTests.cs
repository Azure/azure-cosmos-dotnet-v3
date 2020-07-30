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
            catch(ArgumentNullException)
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

            if(locationNames.Length > cosmosRegions.Length)
            {
                HashSet<string> missingLocationNames = new HashSet<string>(locationNames);
                foreach(string region in cosmosRegions)
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
            Dictionary<string, string> projDependencies = DirectContractTests.GetPackageReferencies(csprojFile);
            Dictionary<string, string> baselineDependencies = JsonConvert.DeserializeObject< Dictionary<string, string>>(
                "{\"System.Numerics.Vectors\":\"4.5.0\",\"Newtonsoft.Json\":\"10.0.2\",\"System.Configuration.ConfigurationManager\":\"4.5.0\",\"System.Memory\":\"4.5.1\",\"System.Buffers\":\"4.5.1\",\"System.Runtime.CompilerServices.Unsafe\":\"4.5.1\",\"System.Threading.Tasks.Extensions\":\"4.5.1\",\"System.ValueTuple\":\"4.5.0\"}");

            Assert.AreEqual(projDependencies.Count, baselineDependencies.Count);
            foreach(KeyValuePair<string, string> projectDependency in projDependencies)
            {
                string baselineVersion = baselineDependencies[projectDependency.Key];
                Assert.AreEqual(baselineVersion, projectDependency.Value);
            }

        }

        [TestMethod]
        public void PackageDependenciesTest()
        {
            string csprojFile = "Microsoft.Azure.Cosmos.csproj";
            Dictionary<string, string> projDependencies = DirectContractTests.GetPackageReferencies(csprojFile);

            string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.nuspec");
            Dictionary<string, string> allDependencies = new Dictionary<string, string>();
            foreach (string nuspecFile in files)
            {
                Dictionary<string, string> nuspecDependencies = DirectContractTests.GetNuspecDependencies(nuspecFile);
                foreach(KeyValuePair<string, string> e in nuspecDependencies)
                {
                    if (!allDependencies.ContainsKey(e.Key))
                    {
                        allDependencies[e.Key] = e.Value;
                    }
                    else
                    {
                        string existingValue = allDependencies[e.Key];
                        if (existingValue.CompareTo(e.Value) > 0)
                        {
                            allDependencies[e.Key] = e.Value;
                        }
                    }
                }
            }

            // Dependency version should match
            foreach(KeyValuePair<string, string> e in allDependencies)
            {
                Assert.AreEqual(e.Value, projDependencies[e.Key]);
            }

            CollectionAssert.IsSubsetOf(allDependencies.Keys, projDependencies.Keys);
        }

        private static Dictionary<string, string> GetPackageReferencies(string csprojName)
        {
            string fullCsprojName = Path.Combine(Directory.GetCurrentDirectory(), csprojName);
            Trace.TraceInformation($"Testing dependencies for csporj file {fullCsprojName}");
            string projContent = File.ReadAllText(fullCsprojName);

            Regex projRefMatcher = new Regex("<PackageReference\\s+Include=\"(?<Include>[^\"]*)\"\\s+Version=\"(?<Version>[^\"]*)\"\\s+(PrivateAssets=\"(?<PrivateAssets>[^\"]*)\")?");
            MatchCollection matches = projRefMatcher.Matches(projContent);

            int prjRefCount = new Regex("<PackageReference").Matches(projContent).Count;
            Assert.AreEqual(prjRefCount, matches.Count, "CSPROJ PackageReference regex is broken");

            Dictionary<string, string> projReferences = new Dictionary<string, string>();
            foreach (Match m in matches)
            {
                if (m.Groups["PrivateAssets"].Captures.Count != 0)
                {
                    Assert.AreEqual("All", m.Groups["PrivateAssets"].Value, $"{m.Groups["Include"].Value}");
                }
                else
                {
                    projReferences[m.Groups["Include"].Value] = m.Groups["Version"].Value;
                }
            }

            return projReferences;
        }

        private static Dictionary<string, string> GetNuspecDependencies(string nuspecFile)
        {
            Trace.TraceInformation($"Testing dependencies for nuspec file {nuspecFile}");
            string nuspecContent = File.ReadAllText(nuspecFile);

            Regex regexDepMatcher = new Regex("<dependency\\s+id=\"(?<id>[^\"]*)\"\\s+version=\"(?<version>[^\"]*)\"");
            MatchCollection matches = regexDepMatcher.Matches(nuspecContent);

            int dependencyCount = new Regex("<dependency").Matches(nuspecContent).Count;
            Assert.AreEqual(dependencyCount, matches.Count, "Nuspec dependency regex is broken");

            Dictionary<string, string> dependencies = new Dictionary<string, string>();
            foreach (Match m in matches)
            {
                dependencies[m.Groups["id"].Value] = m.Groups["version"].Value;
            }

            return dependencies;
        }
    }
}
