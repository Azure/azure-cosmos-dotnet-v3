//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UserAgentTests
    {
        [TestMethod]
        public void ValidateCustomUserAgentHeader()
        {
            const string suffix = " MyCustomUserAgent/1.0";
            ConnectionPolicy policy = new ConnectionPolicy();
            policy.UserAgentSuffix = suffix;
            Assert.IsTrue(policy.UserAgentContainer.UserAgent.EndsWith(suffix));

            byte[] expectedUserAgentUTF8 = Encoding.UTF8.GetBytes(policy.UserAgentContainer.UserAgent);
            CollectionAssert.AreEqual(expectedUserAgentUTF8, policy.UserAgentContainer.UserAgentUTF8);
        }

        [TestMethod]
        public void ValidateUniqueClientIdHeader()
        {
            using (CosmosClient client = TestCommon.CreateCosmosClient())
            {
                string firstClientId = this.GetClientIdFromCosmosClient(client);

                using (CosmosClient innerClient = TestCommon.CreateCosmosClient())
                {
                    string secondClientId = this.GetClientIdFromCosmosClient(innerClient);
                    Assert.AreNotEqual(firstClientId, secondClientId);
                }
            }
        }

        [TestMethod]
        public async Task ValidateUserAgentHeaderWithCustomOs()
        {
            //This changes the runtime information to simulate a max os x response
            const string invalidOsField = "Darwin 18.0.0: Darwin/Kernel/Version 18.0.0: Wed Aug 22 20:13:40 PDT 2018; root:xnu-4903.201.2~1/RELEASE_X86_64";
            FieldInfo fieldInfo = typeof(RuntimeInformation).GetField("s_osDescription", BindingFlags.Static | BindingFlags.NonPublic);
            fieldInfo.SetValue(null, invalidOsField);
            string updatedRuntime = RuntimeInformation.OSDescription;
            Assert.AreEqual(invalidOsField, updatedRuntime);

            const string suffix = " MyCustomUserAgent/1.0";

            using (CosmosClient client = TestCommon.CreateCosmosClient(builder => builder.WithApplicationName(suffix)))
            {
                Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy().UserAgentContainer;

                string userAgentString = userAgentContainer.UserAgent;
                Assert.IsTrue(userAgentString.Contains(suffix));
                Assert.IsTrue(userAgentString.Contains("Darwin 18.0.0"));
                Cosmos.Database db = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
                Assert.IsNotNull(db);
                await db.DeleteAsync();
            }
        }

        private string GetClientIdFromCosmosClient(CosmosClient client)
        {
            Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy().UserAgentContainer;
            string userAgentString = userAgentContainer.UserAgent;
            string clientId = userAgentString.Split('|')[3];
            Assert.AreEqual(5, clientId.Length);
            return clientId;
        }
    }
}
