//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Contracts
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using System.Text;

    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void ApiVersionTest()
        {
            try
            {
                new CosmosClient((string)null);
                Assert.Fail();
            }
            catch(ArgumentNullException)
            { }
#if PREVIEW
            Assert.AreEqual(HttpConstants.Versions.v2020_07_15, HttpConstants.Versions.CurrentVersion);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(HttpConstants.Versions.v2020_07_15), HttpConstants.Versions.CurrentVersionUTF8);
#else
            Assert.AreEqual(HttpConstants.Versions.v2018_12_31, HttpConstants.Versions.CurrentVersion);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(HttpConstants.Versions.v2018_12_31), HttpConstants.Versions.CurrentVersionUTF8);
#endif
        }

        [TestMethod]
        public void ClientDllNamespaceTest()
        {

#if INTERNAL
            int expected = 7;
#else
            int expected = 5;
#endif
            ContractTests.NamespaceCountTest(typeof(CosmosClient), expected);
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