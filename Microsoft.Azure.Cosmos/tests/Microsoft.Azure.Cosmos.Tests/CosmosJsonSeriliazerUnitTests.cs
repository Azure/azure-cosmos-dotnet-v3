//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
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
            CosmosJsonSerializerCore cosmosDefaultJsonSerializer = new CosmosJsonSerializerCore();
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
            CosmosJsonSerializerCore cosmosDefaultJsonSerializer = new CosmosJsonSerializerCore();
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
        public async Task ValidateResponseFactoryJsonSerializer()
        {
            CosmosResponseMessage databaseResponse = this.CreateResponse();
            CosmosResponseMessage containerResponse = this.CreateResponse();
            CosmosResponseMessage storedProcedureExecuteResponse = this.CreateResponse();
            CosmosResponseMessage storedProcedureResponse = this.CreateResponse();
            CosmosResponseMessage triggerResponse = this.CreateResponse();
            CosmosResponseMessage udfResponse = this.CreateResponse();
            CosmosResponseMessage itemResponse = this.CreateResponse();
            CosmosResponseMessage feedResponse = this.CreateResponse();

            Mock<CosmosJsonSerializer> mockUserJsonSerializer = new Mock<CosmosJsonSerializer>();
            Mock<CosmosJsonSerializer> mockDefaultJsonSerializer = new Mock<CosmosJsonSerializer>();
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
            cosmosResponseFactory.CreateResultSetQueryResponse<ToDoActivity>(feedResponse);

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
            Mock<CosmosDatabase> mockDatabase = new Mock<CosmosDatabase>();

            // Verify all the system types that should always use default
            await cosmosResponseFactory.CreateContainerResponseAsync(mockContainer.Object, Task.FromResult(containerResponse));
            await cosmosResponseFactory.CreateDatabaseResponseAsync(mockDatabase.Object, Task.FromResult(databaseResponse));
            await cosmosResponseFactory.CreateStoredProcedureResponseAsync(Task.FromResult(storedProcedureResponse));
            await cosmosResponseFactory.CreateTriggerResponseAsync(Task.FromResult(triggerResponse));
            await cosmosResponseFactory.CreateUserDefinedFunctionResponseAsync(Task.FromResult(udfResponse));

            // Throw if the setups were not called
            mockDefaultJsonSerializer.VerifyAll();
        }

        private CosmosResponseMessage CreateResponse()
        {
            CosmosResponseMessage cosmosResponse = new CosmosResponseMessage(statusCode: HttpStatusCode.OK)
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
