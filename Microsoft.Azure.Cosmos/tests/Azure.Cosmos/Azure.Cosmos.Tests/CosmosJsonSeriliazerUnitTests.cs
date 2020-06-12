//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Scripts;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

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
            CosmosSerializer cosmosDefaultJsonSerializer = CosmosTextJsonSerializer.CreateSerializer();
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
            CosmosSerializer cosmosDefaultJsonSerializer = CosmosTextJsonSerializer.CreateSerializer();
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
            JsonSerializerOptions settings = new JsonSerializerOptions()
            {
                IgnoreNullValues = true
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
            CosmosSerializer cosmosDefaultJsonSerializer = CosmosTextJsonSerializer.CreateSerializer(settings);
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
            Response databaseResponse = this.CreateResponse();
            Response containerResponse = this.CreateResponse();
            Response storedProcedureExecuteResponse = this.CreateResponse();
            Response storedProcedureResponse = this.CreateResponse();
            Response triggerResponse = this.CreateResponse();
            Response udfResponse = this.CreateResponse();
            Response itemResponse = this.CreateResponse();
            Response feedResponse = this.CreateResponse();

            Mock<CosmosSerializer> mockUserJsonSerializer = new Mock<CosmosSerializer>();
            Mock<CosmosSerializer> mockDefaultJsonSerializer = new Mock<CosmosSerializer>();
            CosmosResponseFactory cosmosResponseFactory = new CosmosResponseFactory(
               defaultJsonSerializer: mockDefaultJsonSerializer.Object,
               userJsonSerializer: mockUserJsonSerializer.Object);

            // Test the user specified response
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(itemResponse.ContentStream)).Returns(new ToDoActivity());
            mockUserJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(storedProcedureExecuteResponse.ContentStream)).Returns(new ToDoActivity());
            mockUserJsonSerializer.Setup(x => x.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(feedResponse.ContentStream)).Returns(new CosmosFeedResponseUtil<ToDoActivity>() { Data = new Collection<ToDoActivity>() });

            // Verify all the user types use the user specified version
            await cosmosResponseFactory.CreateItemResponseAsync<ToDoActivity>(Task.FromResult(itemResponse), default(CancellationToken));
            await cosmosResponseFactory.CreateStoredProcedureExecuteResponseAsync<ToDoActivity>(Task.FromResult(storedProcedureExecuteResponse), default(CancellationToken));
            cosmosResponseFactory.CreateQueryFeedResponse<ToDoActivity>(feedResponse);

            // Throw if the setups were not called
            mockUserJsonSerializer.VerifyAll();

            // Test the system specified response
            CosmosContainerProperties containerSettings = new CosmosContainerProperties("mockId", "/pk");
            CosmosDatabaseProperties databaseSettings = new CosmosDatabaseProperties()
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

            mockDefaultJsonSerializer.Setup(x => x.FromStream<CosmosDatabaseProperties>(databaseResponse.ContentStream)).Returns(databaseSettings);
            mockDefaultJsonSerializer.Setup(x => x.FromStream<CosmosContainerProperties>(containerResponse.ContentStream)).Returns(containerSettings);

            Mock<CosmosContainer> mockContainer = new Mock<CosmosContainer>();
            Mock<CosmosDatabase> mockDatabase = new Mock<CosmosDatabase>();

            // Verify all the system types that should always use default
            await cosmosResponseFactory.CreateContainerResponseAsync(mockContainer.Object, Task.FromResult(containerResponse), default(CancellationToken));
            await cosmosResponseFactory.CreateDatabaseResponseAsync(mockDatabase.Object, Task.FromResult(databaseResponse), default(CancellationToken));

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

            CosmosSerializer userSerializer = CosmosTextJsonSerializer.CreateSerializer();
            CosmosSerializer propertiesSerializer = CosmosTextJsonSerializer.CreatePropertiesSerializer();

            CosmosSerializer sqlQuerySpecSerializer = TextJsonCosmosSqlQuerySpecConverter.CreateSqlQuerySpecSerializer(
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

        private Response CreateResponse()
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
