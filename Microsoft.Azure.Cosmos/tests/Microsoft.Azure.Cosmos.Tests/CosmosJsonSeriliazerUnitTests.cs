//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    [TestClass]
    public class CosmosJsonSeriliazerUnitTests
    {
        private ToDoActivity toDoActivity = new ToDoActivity()
        {
            id = "c1d433c1-369d-430e-91e5-14e3ce588f71",
            taskNum = 42,
            cost = double.MaxValue,
            description = "cosmos json serializer",
            status = "TBD"
        };

        private string toDoActivityJson = @"{""id"":""c1d433c1-369d-430e-91e5-14e3ce588f71"",""taskNum"":42,""cost"":1.7976931348623157E+308,""description"":""cosmos json serializer"",""status"":""TBD""}";

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
        public async Task ValidateResponseFactoryJsonSerializer()
        {
            ResponseMessage databaseResponse = this.CreateResponse();
            ResponseMessage containerResponse = this.CreateResponse();
            ResponseMessage storedProcedureExecuteResponse = this.CreateResponse();
            ResponseMessage storedProcedureResponse = this.CreateResponse();
            ResponseMessage triggerResponse = this.CreateResponse();
            ResponseMessage udfResponse = this.CreateResponse();
            ResponseMessage itemResponse = this.CreateResponse();
            ResponseMessage feedResponse = this.CreateResponse();

            Mock<CosmosSerializer> mockUserJsonSerializer = new Mock<CosmosSerializer>();
            Mock<CosmosSerializer> mockDefaultJsonSerializer = new Mock<CosmosSerializer>();
            CosmosResponseFactory cosmosResponseFactory = new CosmosResponseFactory(
               defaultJsonSerializer: mockDefaultJsonSerializer.Object,
               userJsonSerializer: mockUserJsonSerializer.Object);

            // Test the user specified response
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(itemResponse.Content)).Returns(new ToDoActivity());
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(storedProcedureExecuteResponse.Content)).Returns(new ToDoActivity());
            mockUserJsonSerializer.Setup(x => x.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(feedResponse.Content)).Returns(new CosmosFeedResponseUtil<ToDoActivity>() { Data = new Collection<ToDoActivity>() });

            // Verify all the user types use the user specified version
            await cosmosResponseFactory.CreateItemResponseAsync<ToDoActivity>(Task.FromResult(itemResponse));
            await cosmosResponseFactory.CreateStoredProcedureExecuteResponseAsync<ToDoActivity>(Task.FromResult(storedProcedureExecuteResponse));
            cosmosResponseFactory.CreateQueryFeedResponse<ToDoActivity>(feedResponse);

            // Throw if the setups were not called
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

            mockDefaultJsonSerializer.Setup(x => x.FromStream<DatabaseProperties>(databaseResponse.Content)).Returns(databaseSettings);
            mockDefaultJsonSerializer.Setup(x => x.FromStream<ContainerProperties>(containerResponse.Content)).Returns(containerSettings);
            mockDefaultJsonSerializer.Setup(x => x.FromStream<StoredProcedureProperties>(storedProcedureResponse.Content)).Returns(cosmosStoredProcedureSettings);
            mockDefaultJsonSerializer.Setup(x => x.FromStream<TriggerProperties>(triggerResponse.Content)).Returns(cosmosTriggerSettings);
            mockDefaultJsonSerializer.Setup(x => x.FromStream<UserDefinedFunctionProperties>(udfResponse.Content)).Returns(cosmosUserDefinedFunctionSettings);

            Mock<Container> mockContainer = new Mock<Container>();
            Mock<Database> mockDatabase = new Mock<Database>();

            // Verify all the system types that should always use default
            await cosmosResponseFactory.CreateContainerResponseAsync(mockContainer.Object, Task.FromResult(containerResponse));
            await cosmosResponseFactory.CreateDatabaseResponseAsync(mockDatabase.Object, Task.FromResult(databaseResponse));
            await cosmosResponseFactory.CreateStoredProcedureResponseAsync(Task.FromResult(storedProcedureResponse));
            await cosmosResponseFactory.CreateTriggerResponseAsync(Task.FromResult(triggerResponse));
            await cosmosResponseFactory.CreateUserDefinedFunctionResponseAsync(Task.FromResult(udfResponse));

            // Throw if the setups were not called
            mockDefaultJsonSerializer.VerifyAll();
        }

        [TestMethod]
        public void ValidateSqlQuerySpecSerializer()
        {
            List<SqlQuerySpec> sqlQuerySpecs = new List<SqlQuerySpec>();
            sqlQuerySpecs.Add(new SqlQuerySpec()
            {
                QueryText = "SELECT root._rid, [{\"item\": root[\"NumberField\"]}] AS orderByItems, root AS payload\nFROM root\nWHERE (true)\nORDER BY root[\"NumberField\"] DESC"
            });

            sqlQuerySpecs.Add(new SqlQuerySpec()
            {
                QueryText = "Select * from something"
            });
           
            sqlQuerySpecs.Add(new SqlQuerySpec()
            {
                QueryText = "Select * from something",
                Parameters = new SqlParameterCollection()
            });

            SqlParameterCollection sqlParameters = new SqlParameterCollection();
            sqlParameters.Add(new SqlParameter("@id", "test1"));

            sqlQuerySpecs.Add(new SqlQuerySpec()
            {
                QueryText = "Select * from something",
                Parameters = sqlParameters
            });

            sqlParameters = new SqlParameterCollection();
            sqlParameters.Add(new SqlParameter("@id", "test2"));
            sqlParameters.Add(new SqlParameter("@double", 42.42));
            sqlParameters.Add(new SqlParameter("@int", 9001));
            sqlParameters.Add(new SqlParameter("@null", null));
            sqlParameters.Add(new SqlParameter("@datetime", DateTime.UtcNow));

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
