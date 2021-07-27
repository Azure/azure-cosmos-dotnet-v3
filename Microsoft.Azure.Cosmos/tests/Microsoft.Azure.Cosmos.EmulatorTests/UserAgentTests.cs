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
            const string suffix = " UserApplicationName/1.0";

            CosmosClientOptions clientOptions = this.SetEnvironmentInformation(useMacOs);
            clientOptions.ApplicationName = suffix;

            using (CosmosClient client = TestCommon.CreateCosmosClient(clientOptions))
            {
                Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy(client.ClientId).UserAgentContainer;

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
            CosmosClient.numberOfClientsCreated = 0;
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
        public void VerifyUserAgentContent()
        {
            EnvironmentInformation envInfo = new EnvironmentInformation();
            Cosmos.UserAgentContainer userAgentContainer = new Cosmos.UserAgentContainer(clientId: 0);
            string serialization = userAgentContainer.UserAgent;

            Assert.IsTrue(serialization.Contains(envInfo.ProcessArchitecture));
            string[] values = serialization.Split('|');
            Assert.AreEqual($"cosmos-netstandard-sdk/{envInfo.ClientVersion}", values[0]);
            Assert.AreEqual(envInfo.DirectVersion, values[1]);
            Assert.AreEqual("0", values[2]);
            Assert.AreEqual(envInfo.ProcessArchitecture, values[3]);
            Assert.AreEqual(envInfo.OperatingSystem, values[4]);
            Assert.AreEqual(envInfo.RuntimeFramework, values[5]);
        }

        [TestMethod]
        public async Task VerifyUserAgentWithRegionConfiguration()
        {
            string databaseName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();

            {
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions();

                // N - None. The user did not configure anything
                string userAgentContentToValidate = "|N|";
                await this.ValidateUserAgentStringAsync(
                    cosmosClientOptions,
                    userAgentContentToValidate,
                    databaseName,
                    containerName);
            }

            {
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
                {
                    LimitToEndpoint = true
                };
                // D - Disabled endpoint discovery, N - None. The user did not configure anything
                string userAgentContentToValidate = "|DN|";
                await this.ValidateUserAgentStringAsync(
                    cosmosClientOptions,
                    userAgentContentToValidate,
                    databaseName,
                    containerName);
            }

            {
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
                {
                    ApplicationRegion = Regions.EastUS
                };

                // S - Single application region is set
                string userAgentContentToValidate = "|S|";
                await this.ValidateUserAgentStringAsync(
                    cosmosClientOptions,
                    userAgentContentToValidate,
                    databaseName,
                    containerName);
            }

            {
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
                {
                    LimitToEndpoint = false,
                    ApplicationRegion = null,
                    ApplicationPreferredRegions = new List<string>()
                    {
                        Regions.EastUS,
                        Regions.WestUS
                    }
                };

                // L - List of region is set
                string userAgentContentToValidate = "|L|";
                await this.ValidateUserAgentStringAsync(
                    cosmosClientOptions,
                    userAgentContentToValidate,
                    databaseName,
                    containerName);
            }

            using (CosmosClient client = TestCommon.CreateCosmosClient())
            {
                await client.GetDatabase(databaseName).DeleteStreamAsync();
            }
        }

        [TestMethod]
        public void VerifyDefaultUserAgentContainsRegionConfig()
        {
            UserAgentContainer userAgentContainer = new Cosmos.UserAgentContainer(clientId: 0);
            Assert.IsTrue(userAgentContainer.UserAgent.Contains("|NS|"));
        }

        [TestMethod]
        public void VerifyClientIDForUserAgentString()
        {
            CosmosClient.numberOfClientsCreated = 0; // reset
            const int max = 10;
            for (int i = 1; i < max + 5; i++)
            {
                using (CosmosClient client = TestCommon.CreateCosmosClient())
                {
                    string userAgentString = client.DocumentClient.ConnectionPolicy.UserAgentContainer.UserAgent;
                    if (i <= max)
                    {
                        Assert.AreEqual(userAgentString.Split('|')[2], i.ToString());
                    }
                    else
                    {
                        Assert.AreEqual(userAgentString.Split('|')[2], max.ToString());
                    }
                }
            }
        }

        [TestMethod]
        public async Task VerifyClientIDIncrements_Concurrent()
        {
            CosmosClient.numberOfClientsCreated = 0; // reset
            const int max = 10;
            List<int> expected = new List<int>();
            for (int i = 1; i < max + 5; i++)
            {
                expected.Add(i > max ? max : i);
            }

            List<Task<CosmosClient>> tasks = new List<Task<CosmosClient>>();
            for (int i = 1; i < max + 5; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => TestCommon.CreateCosmosClient()));
            }

            await Task.WhenAll(tasks);
            List<int> actual = tasks.Select(r => 
                        int.Parse(r.Result.DocumentClient.ConnectionPolicy.UserAgentContainer.UserAgent.Split('|')[2])).ToList();
            actual.Sort();
            CollectionAssert.AreEqual(expected, actual);
        }

        private async Task ValidateUserAgentStringAsync(
            CosmosClientOptions cosmosClientOptions,
            string userAgentContentToValidate,
            string databaseName,
            string containerName)
        {
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    string userAgent = request.Headers.UserAgent.ToString();
                    Assert.IsTrue(userAgent.Contains(userAgentContentToValidate));
                    return null;
                }
            };

            cosmosClientOptions.HttpClientFactory = () => new HttpClient(httpClientHandlerHelper);

            using (CosmosClient client = TestCommon.CreateCosmosClient(cosmosClientOptions))
            {
                Cosmos.Database db = await client.CreateDatabaseIfNotExistsAsync(databaseName);
                await db.CreateContainerIfNotExistsAsync(containerName, "/pk");
            }
        }

        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public async Task VerifyUserAgentWithFeatures(bool setApplicationName, bool useMacOs)
        {
            CosmosClientOptions cosmosClientOptions = this.SetEnvironmentInformation(useMacOs);

            const string suffix = " UserApplicationName/1.0";
            CosmosClientOptionsFeatures featuresFlags = CosmosClientOptionsFeatures.NoFeatures;
            featuresFlags |= CosmosClientOptionsFeatures.AllowBulkExecution;
            featuresFlags |= CosmosClientOptionsFeatures.HttpClientFactory;

            string features = Convert.ToString((int)featuresFlags, 2).PadLeft(8, '0');

            cosmosClientOptions.AllowBulkExecution = true;
            cosmosClientOptions.HttpClientFactory = () => new HttpClient();
            if (setApplicationName)
            {
                cosmosClientOptions.ApplicationName = suffix;
            }

            using (CosmosClient client = TestCommon.CreateCosmosClient(cosmosClientOptions))
            {
                Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy(client.ClientId).UserAgentContainer;

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

            cosmosClientOptions = this.SetEnvironmentInformation(useMacOs);
            if (setApplicationName)
            {
                cosmosClientOptions.ApplicationName = suffix;
            }

            using (CosmosClient client = TestCommon.CreateCosmosClient(cosmosClientOptions))
            {
                Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy(client.ClientId).UserAgentContainer;

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

        private sealed class MacUserAgentStringClientOptions : CosmosClientOptions
        {
            internal override ConnectionPolicy GetConnectionPolicy(int clientId)
            {
                ConnectionPolicy connectionPolicy = base.GetConnectionPolicy(clientId);
                MacOsUserAgentContainer userAgent = this.CreateUserAgentContainer(clientId);

                connectionPolicy.UserAgentContainer = userAgent;

                return connectionPolicy;
            }

            internal MacOsUserAgentContainer CreateUserAgentContainer(int clientId)
            {
                CosmosClientOptionsFeatures features = CosmosClientOptionsFeatures.NoFeatures;
                if (this.AllowBulkExecution)
                {
                    features |= CosmosClientOptionsFeatures.AllowBulkExecution;
                }

                if (this.HttpClientFactory != null)
                {
                    features |= CosmosClientOptionsFeatures.HttpClientFactory;
                }

                string featureString = null;
                if (features != CosmosClientOptionsFeatures.NoFeatures)
                {
                    featureString = Convert.ToString((int)features, 2).PadLeft(8, '0');
                }

                return new MacOsUserAgentContainer(
                            clientId: clientId,
                            features: featureString,
                            suffix: this.ApplicationName);
            }
        }

        private sealed class MacOsUserAgentContainer : Cosmos.UserAgentContainer
        {
            public MacOsUserAgentContainer(int clientId,
                                            string features = null,
                                            string regionConfiguration = "N",
                                            string suffix = null)
                                               : base(clientId,
                                                     features,
                                                     regionConfiguration,
                                                     suffix)
            {
            }

            protected override void GetEnvironmentInformation(
                out string clientVersion,
                out string directVersion,
                out string processArchitecture,
                out string operatingSystem,
                out string runtimeFramework)
            {
                //This changes the information to simulate a max os x response. Windows user agent are tested by every other emulator test.
                operatingSystem = "Darwin 18.0.0: Darwin/Kernel/Version 18.0.0: Wed Aug 22 20:13:40 PDT 2018; root:xnu-4903.201.2~1/RELEASE_X86_64";

                base.GetEnvironmentInformation(
                    clientVersion: out clientVersion,
                    directVersion: out directVersion,
                    processArchitecture: out processArchitecture,
                    operatingSystem: out _,
                    runtimeFramework: out runtimeFramework);
            }
        }

        private CosmosClientOptions SetEnvironmentInformation(bool useMacOs)
        {
            if (useMacOs)
            {
                return new MacUserAgentStringClientOptions();
            }

            return new CosmosClientOptions();
        }

        private string GetClientIdFromCosmosClient(CosmosClient client)
        {
            Cosmos.UserAgentContainer userAgentContainer = client.ClientOptions.GetConnectionPolicy(client.ClientId).UserAgentContainer;
            string userAgentString = userAgentContainer.UserAgent;
            string clientId = userAgentString.Split('|')[2];
            return clientId;
        }
    }
}
