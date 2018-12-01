//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos;
    using Moq;
    using System.Reflection;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Internal;

    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void ClientDllNamespaceTest()
        {
            ContractTests.NamespaceCountTest(typeof(CosmosClient), 2);
        }

        [TestMethod]
        public void DirectDllTwoNamespaceTest()
        {
            ContractTests.NamespaceCountTest(typeof(DocumentServiceRequest), 1);
        }

        [TestMethod]
        public void DirectDllValidPublicTypesTest()
        {
            string[] allowsTypes = new string[]
            {
                "Microsoft.Azure.Cosmos.ConsistencyLevel",
                "Microsoft.Azure.Cosmos.CosmosAccountLocation",
                "Microsoft.Azure.Cosmos.CosmosAccountSettings",
                "Microsoft.Azure.Cosmos.CosmosConsistencySettings",
                "Microsoft.Azure.Cosmos.CosmosContainerSettings",
                "Microsoft.Azure.Cosmos.CosmosDatabaseSettings",
                "Microsoft.Azure.Cosmos.CosmosResource",
                "Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings",
                "Microsoft.Azure.Cosmos.CosmosTriggerSettings",
                "Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings",
                "Microsoft.Azure.Cosmos.DataType",
                "Microsoft.Azure.Cosmos.ExcludedPath",
                "Microsoft.Azure.Cosmos.HashIndex",
                "Microsoft.Azure.Cosmos.IncludedPath",
                "Microsoft.Azure.Cosmos.Index",
                "Microsoft.Azure.Cosmos.IndexingDirective",
                "Microsoft.Azure.Cosmos.IndexingMode",
                "Microsoft.Azure.Cosmos.IndexingPolicy",
                "Microsoft.Azure.Cosmos.IndexKind",
                "Microsoft.Azure.Cosmos.JsonSerializable",
                "Microsoft.Azure.Cosmos.PartitionKey",
                "Microsoft.Azure.Cosmos.PartitionKeyDefinition",
                "Microsoft.Azure.Cosmos.PartitionKeyRangeStatistics",
                "Microsoft.Azure.Cosmos.PartitionKeyStatistics",
                "Microsoft.Azure.Cosmos.RangeIndex",
                "Microsoft.Azure.Cosmos.SpatialIndex",
                "Microsoft.Azure.Cosmos.TriggerOperation",
                "Microsoft.Azure.Cosmos.TriggerType",
                "Microsoft.Azure.Cosmos.UniqueKey",
                "Microsoft.Azure.Cosmos.UniqueKeyPolicy",
            };

            Assembly clientAssembly = typeof(DocumentServiceRequest).GetAssembly();
            string[] presentTypes = clientAssembly.GetExportedTypes()
                .Select(e => e.FullName)
                .Distinct()
                .ToArray();

            Trace.TraceInformation($"Expected list {Environment.NewLine} {string.Join(Environment.NewLine, allowsTypes.OrderBy(e => e))}");
            Trace.TraceInformation(Environment.NewLine);
            Trace.TraceInformation($"Current list {Environment.NewLine} {string.Join(Environment.NewLine, presentTypes.OrderBy(e => e))}");

            CollectionAssert.AreEqual(allowsTypes.OrderBy(e => e).ToArray(), presentTypes.OrderBy(e => e).ToArray());
        }

        private static void NamespaceCountTest(Type input, int expected)
        {
            Assembly clientAssembly = input.GetAssembly();
            string[] distinctNamespaces = clientAssembly.GetExportedTypes()
                .Select(e => e.Namespace)
                .Distinct()
                .ToArray();

            Assert.AreEqual(expected, distinctNamespaces.Length, string.Join(", ", distinctNamespaces));
        }
    }
}