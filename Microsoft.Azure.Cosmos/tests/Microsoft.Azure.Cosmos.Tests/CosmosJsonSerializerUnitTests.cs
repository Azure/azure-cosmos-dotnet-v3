//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosJsonSerializerUnitTests
    {
        private readonly ToDoActivity toDoActivity = new ToDoActivity()
        {
            Id = "c1d433c1-369d-430e-91e5-14e3ce588f71",
            TaskNum = 42,
            Cost = double.MaxValue,
            Description = "cosmos json serializer",
            Status = "TBD"
        };

        private readonly string toDoActivityJson = @"{""id"":""c1d433c1-369d-430e-91e5-14e3ce588f71"",""taskNum"":42,""cost"":1.7976931348623157E+308,""description"":""cosmos json serializer"",""status"":""TBD""}";

        [TestMethod]
        public void ValidateSerializer()
        {
            CosmosJsonDotNetSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer();
            using (Stream stream = cosmosDefaultJsonSerializer.ToStream<ToDoActivity>(this.toDoActivity))
            {
                Assert.IsNotNull(stream);
                ToDoActivity result = cosmosDefaultJsonSerializer.FromStream<ToDoActivity>(stream);
                Assert.IsNotNull(result);
                Assert.AreEqual(this.toDoActivity.Id, result.Id);
                Assert.AreEqual(this.toDoActivity.TaskNum, result.TaskNum);
                Assert.AreEqual(this.toDoActivity.Cost, result.Cost);
                Assert.AreEqual(this.toDoActivity.Description, result.Description);
                Assert.AreEqual(this.toDoActivity.Status, result.Status);
            }
        }

        [TestMethod]
        public void ValidatePropertySerialization()
        {
            string id = "testId";
            this.TestProperty<AccountProperties>(
                id,
                $@"{{""id"":""{id}"",""writableLocations"":[],""readableLocations"":[],""userConsistencyPolicy"":null,""addresses"":null,""userReplicationPolicy"":null,""systemReplicationPolicy"":null,""readPolicy"":null,""queryEngineConfiguration"":null,""enableMultipleWriteLocations"":false}}");

            this.TestProperty<DatabaseProperties>(
                id,
                $@"{{""id"":""{id}""}}");

            this.TestProperty<ContainerProperties>(
                id,
                $@"{{""id"":""{id}"",""partitionKey"":{{""paths"":[],""kind"":""Hash""}}}}");

            this.TestProperty<StoredProcedureProperties>(
                id,
                $@"{{""body"":""bodyCantBeNull"",""id"":""testId""}}");

            this.TestProperty<TriggerProperties>(
                id,
                $@"{{""body"":null,""triggerType"":""Pre"",""triggerOperation"":""All"",""id"":""{id}""}}");

            this.TestProperty<UserDefinedFunctionProperties>(
                id,
                $@"{{""body"":null,""id"":""{id}""}}");

            this.TestProperty<UserProperties>
                (id,
                $@"{{""id"":""{id}"",""_permissions"":null}}");

            this.TestProperty<PermissionProperties>(
                id,
                $@"{{""id"":""{id}"",""resource"":null,""permissionMode"":0}}");

            this.TestProperty<ConflictProperties>(
               id,
               $@"{{""id"":""{id}"",""operationType"":""Invalid"",""resourceType"":null,""resourceId"":null,""content"":null,""conflict_lsn"":0}}");

            // Throughput doesn't have an id.
            string defaultThroughputJson = @"{}";
            ThroughputProperties property = JsonConvert.DeserializeObject<ThroughputProperties>(defaultThroughputJson);
            Assert.IsNull(property.Throughput);
            string propertyJson = JsonConvert.SerializeObject(property, new JsonSerializerSettings()
            {
                Formatting = Formatting.None
            });
            Assert.AreEqual(defaultThroughputJson, propertyJson);
        }

        private void TestProperty<T>(string id, string defaultJson)
        {
            dynamic property = JsonConvert.DeserializeObject<T>(defaultJson);
            Assert.AreEqual(id, property.Id);
            string propertyJson = JsonConvert.SerializeObject(property, new JsonSerializerSettings()
            {
                Formatting = Formatting.None
            });

            Assert.AreEqual(defaultJson, propertyJson);
            // System properties should be ignored if null
            Assert.IsFalse(propertyJson.Contains("_etag"));
            Assert.IsFalse(propertyJson.Contains("_rid"));
            Assert.IsFalse(propertyJson.Contains("_ts"));
            Assert.IsFalse(propertyJson.Contains("_self"));
        }

        [TestMethod]
        public void ValidateJson()
        {
            CosmosJsonDotNetSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer();
            using (Stream stream = cosmosDefaultJsonSerializer.ToStream<ToDoActivity>(this.toDoActivity))
            {
                Assert.IsNotNull(stream);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string responseAsString = reader.ReadToEnd();
                    Assert.IsNotNull(responseAsString);
                    Assert.AreEqual(this.toDoActivityJson, responseAsString);
                }
            }
        }

        [TestMethod]
        public void ValidateCustomSerializerSettings()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            ToDoActivity toDoActivityNoDescription = new ToDoActivity()
            {
                Id = "c1d433c1-369d-430e-91e5-14e3ce588f71",
                TaskNum = 42,
                Cost = double.MaxValue,
                Description = null,
                Status = "TBD"
            };

            string toDoActivityJson = @"{""id"":""c1d433c1-369d-430e-91e5-14e3ce588f71"",""taskNum"":42,""cost"":1.7976931348623157E+308,""status"":""TBD""}";
            CosmosJsonDotNetSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer(settings);
            using (Stream stream = cosmosDefaultJsonSerializer.ToStream<ToDoActivity>(toDoActivityNoDescription))
            {
                Assert.IsNotNull(stream);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string responseAsString = reader.ReadToEnd();
                    Assert.IsNotNull(responseAsString);
                    Assert.AreEqual(toDoActivityJson, responseAsString);
                }
            }
        }

        [TestMethod]
        public void ValidateResponseFactoryJsonSerializer()
        {
            ResponseMessage databaseResponse = this.CreateResponse();
            ResponseMessage containerResponse = this.CreateResponse();
            ResponseMessage storedProcedureExecuteResponse = this.CreateResponse();
            ResponseMessage storedProcedureResponse = this.CreateResponse();
            ResponseMessage triggerResponse = this.CreateResponse();
            ResponseMessage udfResponse = this.CreateResponse();
            ResponseMessage itemResponse = this.CreateResponse();

            Mock<CosmosSerializer> mockUserJsonSerializer = new Mock<CosmosSerializer>();
            CosmosSerializerCore serializerCore = new CosmosSerializerCore(mockUserJsonSerializer.Object);
            CosmosResponseFactoryInternal cosmosResponseFactory = new CosmosResponseFactoryCore(serializerCore);

            // Verify all the user types use the user specified version
            ItemResponse<ToDoActivity> itemResponseFromFactory = cosmosResponseFactory.CreateItemResponse<ToDoActivity>(itemResponse);
            Assert.IsNotNull(itemResponseFromFactory.Diagnostics);
            // Verify that FromStream is not called as the stream is empty
            mockUserJsonSerializer.Verify(x => x.FromStream<ToDoActivity>(itemResponse.Content), Times.Never);
            cosmosResponseFactory.CreateStoredProcedureExecuteResponse<ToDoActivity>(storedProcedureExecuteResponse);

            // Throw if the setups were not called
            mockUserJsonSerializer.VerifyAll();

            // Test read feed scenario
            ResponseMessage readFeedResponse = this.CreateReadFeedResponse();
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity[]>(It.IsAny<Stream>()))
                .Callback<Stream>(input => input.Dispose())
                .Returns(new ToDoActivity[] { new ToDoActivity() });
            FeedResponse<ToDoActivity> feedResponse = cosmosResponseFactory.CreateItemFeedResponse<ToDoActivity>(readFeedResponse);
            foreach (ToDoActivity toDoActivity in feedResponse)
            {
                Assert.IsNotNull(toDoActivity);
            }

            mockUserJsonSerializer.VerifyAll();

            ResponseMessage changeFeedResponseMessage = this.CreateChangeFeedNotModifiedResponse();
            FeedResponse<ToDoActivity> changeFeedResponse = cosmosResponseFactory.CreateItemFeedResponse<ToDoActivity>(changeFeedResponseMessage);
            Assert.AreEqual(HttpStatusCode.NotModified, changeFeedResponse.StatusCode);

            ResponseMessage queryResponse = this.CreateReadFeedResponse();
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity[]>(It.IsAny<Stream>())).Callback<Stream>(input => input.Dispose()).Returns(new ToDoActivity[] { new ToDoActivity() });
            FeedResponse<ToDoActivity> queryFeedResponse = cosmosResponseFactory.CreateItemFeedResponse<ToDoActivity>(queryResponse);
            foreach (ToDoActivity toDoActivity in queryFeedResponse)
            {
                Assert.IsNotNull(toDoActivity);
            }

            mockUserJsonSerializer.VerifyAll();

            // Test the system specified response
            ContainerProperties containerSettings = new ContainerProperties("mockId", "/pk");
            DatabaseProperties databaseSettings = new DatabaseProperties()
            {
                Id = "mock"
            };

            StoredProcedureProperties cosmosStoredProcedureSettings = new StoredProcedureProperties()
            {
                Id = "mock"
            };

            TriggerProperties cosmosTriggerSettings = new TriggerProperties()
            {
                Id = "mock"
            };

            UserDefinedFunctionProperties cosmosUserDefinedFunctionSettings = new UserDefinedFunctionProperties()
            {
                Id = "mock"
            };

            Mock<Container> mockContainer = new Mock<Container>();
            Mock<Database> mockDatabase = new Mock<Database>();

            // Verify all the system types that should always use default
            cosmosResponseFactory.CreateContainerResponse(mockContainer.Object, containerResponse);
            cosmosResponseFactory.CreateDatabaseResponse(mockDatabase.Object, databaseResponse);
            cosmosResponseFactory.CreateStoredProcedureResponse(storedProcedureResponse);
            cosmosResponseFactory.CreateTriggerResponse(triggerResponse);
            cosmosResponseFactory.CreateUserDefinedFunctionResponse(udfResponse);
        }

        [TestMethod]
        public void ValidateResponseFactoryJsonSerializerWithContent()
        {
            ResponseMessage itemResponse = this.CreateResponseWithContent();

            Mock<CosmosSerializer> mockUserJsonSerializer = new Mock<CosmosSerializer>();
            CosmosSerializerCore serializerCore = new CosmosSerializerCore(mockUserJsonSerializer.Object);
            CosmosResponseFactoryInternal cosmosResponseFactory = new CosmosResponseFactoryCore(serializerCore);

            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(itemResponse.Content)).Callback<Stream>(input => input.Dispose()).Returns(new ToDoActivity());

            // Verify all the user types use the user specified version
            ItemResponse<ToDoActivity> itemResponseFromFactory = cosmosResponseFactory.CreateItemResponse<ToDoActivity>(itemResponse);
            Assert.IsNotNull(itemResponseFromFactory.Diagnostics);
            Assert.IsNotNull(itemResponseFromFactory.Resource);
            Assert.AreEqual(HttpStatusCode.OK, itemResponseFromFactory.StatusCode);

            // Throw if the setups were not called
            mockUserJsonSerializer.VerifyAll();
        }

        [TestMethod]
        public void ValidateSqlQuerySpecSerializerWithResumeFilter()
        {
            // Test serializing of different types
            string queryText = "SELECT * FROM root r";
            (SqlQueryResumeValue resumeValue, string resumeString)[] testValues = new (SqlQueryResumeValue resumeValue, string resumeString)[] {
                (SqlQueryResumeValue.FromCosmosElement(CosmosUndefined.Create()), "[]"),
                (SqlQueryResumeValue.FromCosmosElement(CosmosNull.Create()), "null"),
                (SqlQueryResumeValue.FromCosmosElement(CosmosBoolean.Create(true)), "true"),
                (SqlQueryResumeValue.FromCosmosElement(CosmosBoolean.Create(false)), "false"),
                (SqlQueryResumeValue.FromCosmosElement(CosmosNumber64.Create(10)), "10"),
                (SqlQueryResumeValue.FromCosmosElement(CosmosString.Create("testval")), "\"testval\""),
                (SqlQueryResumeValue.FromCosmosElement(CosmosObject.Parse("{\"type\":\"array\",\"low\":10000,\"high\":20000}")), "{\"type\":\"array\",\"low\":10000,\"high\":20000}"),
                (SqlQueryResumeValue.FromCosmosElement(CosmosObject.Parse("{\"type\":\"object\",\"low\":10000,\"high\":20000}")), "{\"type\":\"object\",\"low\":10000,\"high\":20000}"),
                (SqlQueryResumeValue.FromOrderByValue(CosmosArray.Parse("[]")), "{\"type\":\"array\",\"low\":-6706074647855398782,\"high\":9031114912533472255}"),
                (SqlQueryResumeValue.FromOrderByValue(CosmosObject.Parse("{}")), "{\"type\":\"object\",\"low\":1457042291250783704,\"high\":1493060239874959160}")
            };

            CosmosJsonDotNetSerializer userSerializer = new CosmosJsonDotNetSerializer();
            CosmosJsonDotNetSerializer propertiesSerializer = new CosmosJsonDotNetSerializer();

            CosmosSerializer sqlQuerySpecSerializer = CosmosSqlQuerySpecJsonConverter.CreateSqlQuerySpecSerializer(
                userSerializer,
                propertiesSerializer);

            foreach ((SqlQueryResumeValue resumeValue, string resumeString) in testValues)
            {
                foreach (string rid in new string[] { "rid", null })
                {
                    SqlQuerySpec querySpec = new SqlQuerySpec(
                        queryText,
                        new SqlParameterCollection(),
                        new SqlQueryResumeFilter(new List<SqlQueryResumeValue>() { resumeValue }, rid, true));

                    Stream stream = sqlQuerySpecSerializer.ToStream(querySpec);
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        string result = sr.ReadToEnd();
                        Assert.IsNotNull(result);

                        string expectedValue = string.IsNullOrEmpty(rid)
                            ? $"{{\"query\":\"{queryText}\",\"resumeFilter\":{{\"value\":[{resumeString}],\"exclude\":true}}}}"
                            : $"{{\"query\":\"{queryText}\",\"resumeFilter\":{{\"value\":[{resumeString}],\"rid\":\"{rid}\",\"exclude\":true}}}}";
                        Assert.AreEqual(expectedValue, result);
                    }
                }
            }
        }

        [TestMethod]
        public void ValidateSqlQuerySpecSerializer()
        {
            List<SqlQuerySpec> sqlQuerySpecs = new List<SqlQuerySpec>
            {
                new SqlQuerySpec()
                {
                    QueryText = "SELECT root._rid, [{\"item\": root[\"NumberField\"]}] AS orderByItems, root AS payload\nFROM root\nWHERE (true)\nORDER BY root[\"NumberField\"] DESC"
                },

                new SqlQuerySpec()
                {
                    QueryText = "Select * from something"
                },

                new SqlQuerySpec()
                {
                    QueryText = "Select * from something",
                    Parameters = new SqlParameterCollection()
                }
            };

            SqlParameterCollection sqlParameters = new SqlParameterCollection
            {
                new SqlParameter("@id", "test1")
            };

            sqlQuerySpecs.Add(new SqlQuerySpec()
            {
                QueryText = "Select * from something",
                Parameters = sqlParameters
            });

            sqlParameters = new SqlParameterCollection
            {
                new SqlParameter("@id", "test2"),
                new SqlParameter("@double", 42.42),
                new SqlParameter("@int", 9001),
                new SqlParameter("@null", null),
                new SqlParameter("@datetime", DateTime.UtcNow)
            };

            sqlQuerySpecs.Add(new SqlQuerySpec()
            {
                QueryText = "Select * from something",
                Parameters = sqlParameters
            });

            CosmosJsonDotNetSerializer userSerializer = new CosmosJsonDotNetSerializer();
            CosmosJsonDotNetSerializer propertiesSerializer = new CosmosJsonDotNetSerializer();

            CosmosSerializer sqlQuerySpecSerializer = CosmosSqlQuerySpecJsonConverter.CreateSqlQuerySpecSerializer(
                userSerializer,
                propertiesSerializer);

            foreach (SqlQuerySpec sqlQuerySpec in sqlQuerySpecs)
            {
                Stream stream = propertiesSerializer.ToStream<SqlQuerySpec>(sqlQuerySpec);
                string result1;
                using (StreamReader sr = new StreamReader(stream))
                {
                    result1 = sr.ReadToEnd();
                    Assert.IsNotNull(result1);
                }

                stream = sqlQuerySpecSerializer.ToStream(sqlQuerySpec);
                string result2;
                using (StreamReader sr = new StreamReader(stream))
                {
                    result2 = sr.ReadToEnd();
                    Assert.IsNotNull(result2);
                }

                Assert.AreEqual(result1, result2);
            }
        }

        private ResponseMessage CreateResponse()
        {
            ResponseMessage cosmosResponse = new ResponseMessage(statusCode: HttpStatusCode.OK)
            {
                Content = new MemoryStream()
            };
            return cosmosResponse;
        }

        private ResponseMessage CreateResponseWithContent()
        {
            ResponseMessage cosmosResponse = new ResponseMessage(statusCode: HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(this.toDoActivityJson))
            };
            return cosmosResponse;
        }

        private ResponseMessage CreateQueryResponse()
        {
            List<CosmosElement> cosmosElements = new List<CosmosElement>();
            string serializedItem = this.GetSerializedToDoActivity();
            CosmosObject cosmosObject = CosmosObject.Parse(serializedItem);
            cosmosElements.Add(cosmosObject);

            ResponseMessage cosmosResponse = QueryResponse.CreateSuccess(
                cosmosElements,
                1,
                new CosmosQueryResponseMessageHeaders(
                    continauationToken: null,
                    disallowContinuationTokenMessage: null,
                    resourceType: Documents.ResourceType.Document,
                    "+o4fAPfXPzw="),
                null,
                NoOpTrace.Singleton);

            return cosmosResponse;
        }

        private ResponseMessage CreateChangeFeedNotModifiedResponse()
        {
            ResponseMessage cosmosResponse = new ResponseMessage(statusCode: HttpStatusCode.NotModified)
            {
                Content = null
            };

            return cosmosResponse;
        }

        private ResponseMessage CreateReadFeedResponse()
        {
            string documentWrapper = $"{{\"_rid\":\"+o4fAPfXPzw=\",\"Documents\":[{this.GetSerializedToDoActivity()}],\"_count\":1}}";
            ResponseMessage cosmosResponse = new ResponseMessage(statusCode: HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(documentWrapper))
            };

            return cosmosResponse;
        }

        private string GetSerializedToDoActivity()
        {
            return @"{""id"":""c1d433c1-369d-430e-91e5-14e3ce588f71"",""taskNum"":42,""cost"":1.7976931348623157E+308,""status"":""TBD""}";
        }

        private class ToDoActivity
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("taskNum")]
            public int TaskNum { get; set; }
            [JsonProperty("cost")]
            public double Cost { get; set; }
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("status")]
            public string Status { get; set; }
        }
    }
}