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
    using Microsoft.Azure.Documents;

    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void ClientDllNamespaceTest()
        {
            ContractTests.NamespaceCountTest(typeof(CosmosClient), 3);
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