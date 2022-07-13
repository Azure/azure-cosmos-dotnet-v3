//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    public abstract class CosmosItemIdEncodingTestsBase : BaseCosmosClientHelper
    {
        private static readonly Encoding utf8Encoding = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        private Container Container = null;
        private ContainerProperties containerSettings = null;

        protected abstract void ConfigureClientBuilder(CosmosClientBuilder builder);

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(
                validateSinglePartitionKeyRangeCacheCall: true,
                customizeClientBuilder: this.ConfigureClientBuilder);

            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                throughput: 15000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task PlainVanillaId()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(PlainVanillaId),
                Id = "Test",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithWhitespaces()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithWhitespaces),
                Id = "This is a test",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdStartingWithWhitespace()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdStartingWithWhitespace),
                Id = " Test",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdStartingWithWhitespaces()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdStartingWithWhitespaces),
                Id = "  Test",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdEndingWithWhitespace()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdEndingWithWhitespace),
                Id = "Test ",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedReplaceStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedDeleteStatusCode = HttpStatusCode.Unauthorized
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdEndingWithWhitespaces()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdEndingWithWhitespaces),
                Id = "Test  ",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedReplaceStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedDeleteStatusCode = HttpStatusCode.Unauthorized
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithUnicodeCharacters()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithUnicodeCharacters),
                Id = "WithUnicode鱀",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithAllowedSpecialCharacters()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithAllowedSpecialCharacters),
                Id = "WithAllowedSpecial,=.:~+-@()^${}[]!_Chars",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithBase64EncodedIdCharacters()
        {
            string base64EncodedId = "BQE1D3PdG4N4bzU9TKaCIM3qc0TVcZ2/Y3jnsRfwdHC1ombkX3F1dot/SG0/UTq9AbgdX3kOWoP6qL6lJqWeKgV3zwWWPZO/t5X0ehJzv9LGkWld07LID2rhWhGT6huBM6Q=";
            string safeBase64EncodedId = base64EncodedId.Replace("/", "-");

            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithAllowedSpecialCharacters),
                Id = safeBase64EncodedId,
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdEndingWithPercentEncodedWhitespace()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithPercentEncodedSpecialChar),
                Id = "IdEndingWithPercentEncodedWhitespace%20",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedReplaceStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedDeleteStatusCode = HttpStatusCode.Unauthorized
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithPercentEncodedSpecialChar()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithPercentEncodedSpecialChar),
                Id = "WithPercentEncodedSpecialChar%E9%B1%80",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedReplaceStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedDeleteStatusCode = HttpStatusCode.Unauthorized
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithDisallowedCharQuestionMark()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithDisallowedCharQuestionMark),
                Id = "Disallowed?Chars",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithDisallowedCharForwardSlash()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithDisallowedCharForwardSlash),
                Id = "Disallowed/Chars",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.NotFound,
                    ExpectedReplaceStatusCode = HttpStatusCode.NotFound,
                    ExpectedDeleteStatusCode = HttpStatusCode.NotFound
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.NotFound,
                    ExpectedReplaceStatusCode = HttpStatusCode.NotFound,
                    ExpectedDeleteStatusCode = HttpStatusCode.NotFound
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithDisallowedCharBackSlash()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithDisallowedCharBackSlash),
                Id = "Disallowed\\Chars",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.BadRequest,
                    ExpectedReadStatusCode = HttpStatusCode.NotFound,
                    ExpectedReplaceStatusCode = HttpStatusCode.NotFound,
                    ExpectedDeleteStatusCode = HttpStatusCode.NotFound
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.BadRequest,
                    ExpectedReadStatusCode = HttpStatusCode.NotFound,
                    ExpectedReplaceStatusCode = HttpStatusCode.NotFound,
                    ExpectedDeleteStatusCode = HttpStatusCode.NotFound
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithDisallowedCharPoundSign()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithDisallowedCharPoundSign),
                Id = "Disallowed#Chars",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedReplaceStatusCode = HttpStatusCode.Unauthorized,
                    ExpectedDeleteStatusCode = HttpStatusCode.Unauthorized
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithCarriageReturn()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithCarriageReturn),
                Id = "With\rCarriageReturn",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.NoContent,
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithTab()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithTab),
                Id = "With\tTab",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.NoContent,
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        [TestMethod]
        public async Task IdWithLineFeed()
        {
            TestScenario scenario = new TestScenario
            {
                Name = nameof(IdWithLineFeed),
                Id = "With\nLineFeed",
                Gateway = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Gateway,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.BadRequest,// CGW - HttpStatusCode.NoContent,
                },
                Direct = new TestScenarioExpectations
                {
                    TransportMode = ConnectionMode.Direct,
                    ExpectedCreateStatusCode = HttpStatusCode.Created,
                    ExpectedReadStatusCode = HttpStatusCode.OK,
                    ExpectedReplaceStatusCode = HttpStatusCode.OK,
                    ExpectedDeleteStatusCode = HttpStatusCode.NoContent
                },
            };

            await this.ExecuteTestCase(scenario);
        }

        private async Task ExecuteTestCase(TestScenario scenario)
        {
            TestScenarioExpectations expected =
                this.cosmosClient.ClientOptions.ConnectionMode == ConnectionMode.Gateway ?
                    scenario.Gateway : scenario.Direct;

            Console.WriteLine($"Scenario: {scenario.Name}, Id: \"{scenario.Id}\"");

            ResponseMessage response = await this.Container.CreateItemStreamAsync(
                await CreateItemPayload(scenario.Id),
                new PartitionKey(scenario.Id));

            Assert.AreEqual(expected.ExpectedCreateStatusCode, response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                await DeserializeAndValidatePayload(response.Content, scenario.Id);
            }

            if (!expected.ExpectedCreateStatusCode.IsSuccess())
            {
                return;
            }

            response = await this.Container.ReadItemStreamAsync(
                scenario.Id,
                new PartitionKey(scenario.Id));

            Assert.AreEqual(expected.ExpectedReadStatusCode, response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                await DeserializeAndValidatePayload(response.Content, scenario.Id);
            }

            response = await this.Container.ReplaceItemStreamAsync(
                await CreateItemPayload(scenario.Id),
                scenario.Id,
                new PartitionKey(scenario.Id));

            Assert.AreEqual(expected.ExpectedReplaceStatusCode, response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                await DeserializeAndValidatePayload(response.Content, scenario.Id);
            }

            response = await this.Container.DeleteItemStreamAsync(
                scenario.Id,
                new PartitionKey(scenario.Id));

            Assert.AreEqual(expected.ExpectedDeleteStatusCode, response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                if (this.cosmosClient.ClientOptions.ConnectionMode == ConnectionMode.Gateway)
                {
                    await ValidateEmptyPayload(response.Content);
                }
                else
                {
                    Assert.IsNull(response.Content);
                }
            }
        }

        private static async Task<Stream> CreateItemPayload(String id)
        {
            MemoryStream stream = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(stream, utf8Encoding, 4096, leaveOpen: true))
            {
                string json = $"{{ \"pk\": \"{id}\", \"id\": \"{id}\"}}";
                await writer.WriteAsync(json);
                await writer.FlushAsync();
                stream.Position = 0;
            }

            return stream;
        }

        private static async Task<ItemPayload> DeserializeAndValidatePayload(Stream content, string expectedId)
        {
            using (StreamReader reader = new StreamReader(content, utf8Encoding))
            {
                ItemPayload payload = JsonConvert.DeserializeObject<ItemPayload>(await reader.ReadToEndAsync());
                Assert.AreEqual(expectedId, payload.Id);
                Assert.AreEqual(expectedId, payload.PartitionKey);

                return payload;
            }
        }

        private static async Task ValidateEmptyPayload(Stream content)
        {
            using (StreamReader reader = new StreamReader(content, utf8Encoding))
            {
                String payload = await reader.ReadToEndAsync();
                Assert.AreEqual(String.Empty, payload);
            }
        }

        [JsonObject]
        private class ItemPayload
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("pk")]
            public string PartitionKey { get; set; }
        }

        private class TestScenario
        {
            public string Name { get; set; }

            public string Id { get; set; }

            public TestScenarioExpectations Gateway { get; set; }

            public TestScenarioExpectations Direct { get; set; }
        }

        private class TestScenarioExpectations
        {
            public ConnectionMode TransportMode { get; set; }

            public HttpStatusCode ExpectedCreateStatusCode { get; set; }

            public HttpStatusCode ExpectedReadStatusCode { get; set; }

            public HttpStatusCode ExpectedReplaceStatusCode { get; set; }

            public HttpStatusCode ExpectedDeleteStatusCode { get; set; }
        }
    }
}
