//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosJsonSeriliazerUnitTests
    {
        private readonly ToDoActivity toDoActivity = new ToDoActivity()
        {
            id = "c1d433c1-369d-430e-91e5-14e3ce588f71",
            taskNum = 42,
            cost = double.MaxValue,
            description = "cosmos json serializer",
            status = "TBD"
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
                Assert.AreEqual(this.toDoActivity.id, result.id);
                Assert.AreEqual(this.toDoActivity.taskNum, result.taskNum);
                Assert.AreEqual(this.toDoActivity.cost, result.cost);
                Assert.AreEqual(this.toDoActivity.description, result.description);
                Assert.AreEqual(this.toDoActivity.status, result.status);
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
                id = "c1d433c1-369d-430e-91e5-14e3ce588f71",
                taskNum = 42,
                cost = double.MaxValue,
                description = null,
                status = "TBD"
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
            CosmosResponseFactoryInternal cosmosResponseFactory = new CosmosResponseFactoryCore(
               serializerCore);

            // Test the user specified response
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(itemResponse.Content)).Callback<Stream>(input => input.Dispose()).Returns(new ToDoActivity());
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(storedProcedureExecuteResponse.Content)).Callback<Stream>(input => input.Dispose()).Returns(new ToDoActivity());

            // Verify all the user types use the user specified version
            cosmosResponseFactory.CreateItemResponse<ToDoActivity>(itemResponse);
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
            try
            {
                cosmosResponseFactory.CreateItemFeedResponse<ToDoActivity>(changeFeedResponseMessage);
                Assert.Fail();
            }
            catch (CosmosException cosmosException)
            {
                Assert.AreEqual(HttpStatusCode.NotModified, cosmosException.StatusCode);
            }

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

        private ResponseMessage CreateQueryResponse()
        {
            List<CosmosElement> cosmosElements = new List<CosmosElement>();
            string serializedItem = this.GetSerializedToDoActivity();
            CosmosObject cosmosObject = CosmosObject.Parse(serializedItem);
            cosmosElements.Add(cosmosObject);

            ResponseMessage cosmosResponse = QueryResponse.CreateSuccess(
                cosmosElements,
                1,
                Encoding.UTF8.GetByteCount(serializedItem),
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

        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }
    }
}
