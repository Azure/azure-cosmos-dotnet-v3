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
        [DataRow(true)]
        [DataRow(false)]
        public async Task ValidateUserAgentHeaderWithMacOs(bool useMacOs)
        {
            this.SetEnvironmentInformation(useMacOs);

            const string suffix = " UserApplicationName/1.0";

            using (CosmosClient client = TestCommon.CreateCosmosClient(builder => builder.WithApplicationName(suffix)))
            {
                Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy().UserAgentContainer;

                string userAgentString = userAgentContainer.UserAgent;
                Assert.IsTrue(userAgentString.Contains(suffix));
                if (useMacOs)
                {
                    Assert.IsTrue(userAgentString.Contains("Darwin 18.0.0"));
                }

                Cosmos.Database db = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
                Assert.IsNotNull(db);
                await db.DeleteAsync();
            }
        }

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
        [DataRow(true)]
        [DataRow(false)]
        public void VerifyUserAgentContent(bool useMacOs)
        {
            this.SetEnvironmentInformation(useMacOs);

            EnvironmentInformation envInfo = new EnvironmentInformation();
            Cosmos.UserAgentContainer userAgentContainer = new Cosmos.UserAgentContainer();
            string serialization = userAgentContainer.UserAgent;

            Assert.IsTrue(serialization.Contains(envInfo.ProcessArchitecture));
            string[] values = serialization.Split('|');
            Assert.AreEqual($"cosmos-netstandard-sdk/{envInfo.ClientVersion}", values[0]);
            Assert.AreEqual(envInfo.DirectVersion, values[1]);
            Assert.AreEqual(envInfo.ClientId.Length, values[2].Length);
            Assert.AreEqual(envInfo.ProcessArchitecture, values[3]);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(values[4]));
            if (useMacOs)
            {
                Assert.AreEqual("Darwin 18.0.0 Darwin Kernel V", values[4]);
            }
            Assert.AreEqual(envInfo.RuntimeFramework, values[5]);
        }

        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public async Task VerifyUserAgentWithFeatures(bool setApplicationName, bool useMacOs)
        {
            this.SetEnvironmentInformation(useMacOs);

            const string suffix = " UserApplicationName/1.0";

            string features = Convert.ToString((int)CosmosClientOptionsFeatures.AllowBulkExecution, 2).PadLeft(8, '0');

            Action<Fluent.CosmosClientBuilder> applicationNameBuilder = (builder) =>
            {
                if (setApplicationName)
                {
                    builder.WithApplicationName(suffix);
                }
            };

            using (CosmosClient client = TestCommon.CreateCosmosClient(builder => applicationNameBuilder(builder.WithBulkExecution(true))))
            {
                Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy().UserAgentContainer;

                string userAgentString = userAgentContainer.UserAgent;
                if (setApplicationName)
                {
                    Assert.IsTrue(userAgentString.Contains(suffix));
                }
                else
                {
                    Assert.IsFalse(userAgentString.Contains(suffix));
                }

                Assert.IsTrue(userAgentString.Contains($"|F {features}"));
                if (useMacOs)
                {
                    Assert.IsTrue(userAgentString.Contains("Darwin 18.0.0"));
                }

                Cosmos.Database db = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
                Assert.IsNotNull(db);
                await db.DeleteAsync();
            }

            using (CosmosClient client = TestCommon.CreateCosmosClient(builder => applicationNameBuilder(builder)))
            {
                Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy().UserAgentContainer;

                string userAgentString = userAgentContainer.UserAgent;
                if (setApplicationName)
                {
                    Assert.IsTrue(userAgentString.Contains(suffix));
                }
                else
                {
                    Assert.IsFalse(userAgentString.Contains(suffix));
                }

                Assert.IsFalse(userAgentString.Contains($"|F {features}"));
            }
        }

        private void SetEnvironmentInformation(bool useMacOs)
        {
            //This changes the runtime information to simulate a max os x response. Windows user agent are tested by every other emulator test.
            const string invalidOsField = "Darwin 18.0.0: Darwin/Kernel/Version 18.0.0: Wed Aug 22 20:13:40 PDT 2018; root:xnu-4903.201.2~1/RELEASE_X86_64";

            FieldInfo fieldInfo = typeof(EnvironmentInformation).GetField("os", BindingFlags.Static | BindingFlags.NonPublic);
            fieldInfo.SetValue(null, useMacOs ? invalidOsField : RuntimeInformation.OSDescription);
        }

        private string GetClientIdFromCosmosClient(CosmosClient client)
        {
            Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy().UserAgentContainer;
            string userAgentString = userAgentContainer.UserAgent;
            string clientId = userAgentString.Split('|')[2];
            Assert.AreEqual(5, clientId.Length);
            return clientId;
        }
    }
}
