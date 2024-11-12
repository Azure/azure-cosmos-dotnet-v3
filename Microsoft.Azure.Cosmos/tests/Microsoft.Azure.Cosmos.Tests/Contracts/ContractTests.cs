//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Contracts
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            catch (ArgumentNullException)
            { }

            Assert.AreEqual(HttpConstants.Versions.v2020_07_15, HttpConstants.Versions.CurrentVersion);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(HttpConstants.Versions.v2020_07_15), HttpConstants.Versions.CurrentVersionUTF8);

            ulong capabilitites = SDKSupportedCapabilitiesHelpers.GetSDKSupportedCapabilities();
            Assert.AreEqual(capabilitites & (ulong)SDKSupportedCapabilities.PartitionMerge, (ulong)SDKSupportedCapabilities.PartitionMerge);
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

        [TestMethod]
        public void GenerateKeyAuthorizationSignatureContractTest()
        {
            Assembly clientAssembly = typeof(AuthorizationHelper).GetAssembly();
            object[] internals = clientAssembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false);
            bool foundPortalBackend = internals.Cast<InternalsVisibleToAttribute>()
                                  .Any(x => x.AssemblyName.Contains("Microsoft.Azure.Cosmos.Portal.Services.Backend"));

            if (foundPortalBackend)
            {
                // Test GenerateKeyAuthorizationSignature method still exists
                string[] generateKeyAuthorizationSignatureArgs = new string[] {
                    "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c",
                    "dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c",
                    "GET",
                    "colls",
                    "Tue, 21 Jul 2020 17:55:37 GMT",
                    "" };
                string key = "VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc=";
                Documents.Collections.RequestNameValueCollection headers = new()
                {
                    { HttpConstants.HttpHeaders.XDate, generateKeyAuthorizationSignatureArgs[4] }
                };

                string keyAuthorizationSignature = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                    verb: generateKeyAuthorizationSignatureArgs[2],
                    resourceId: generateKeyAuthorizationSignatureArgs[1],
                    resourceType: generateKeyAuthorizationSignatureArgs[3],
                    headers: headers,
                    key: key);

                AuthorizationHelper.ParseAuthorizationToken(keyAuthorizationSignature, out ReadOnlyMemory<char> typeOutput1, out ReadOnlyMemory<char> versionoutput1, out ReadOnlyMemory<char> tokenOutput1);
                Assert.AreEqual("master", typeOutput1.ToString());
                Assert.IsTrue(AuthorizationHelper.CheckPayloadUsingKey(
                    tokenOutput1,
                    generateKeyAuthorizationSignatureArgs[2],
                    generateKeyAuthorizationSignatureArgs[1],
                    generateKeyAuthorizationSignatureArgs[3],
                    headers,
                    key));
            }
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