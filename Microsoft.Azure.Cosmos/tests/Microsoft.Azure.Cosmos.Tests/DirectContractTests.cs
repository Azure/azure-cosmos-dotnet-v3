//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}
