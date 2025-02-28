//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class OpenTelemetryRecorderTests
    {
        private const string DllName = "Microsoft.Azure.Cosmos.Client";

        private static Assembly GetAssemblyLocally(string name)
        {
            Assembly.Load(name);
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            return loadedAssemblies
                .Where((candidate) => candidate.FullName.Contains(name + ","))
                .FirstOrDefault();
        }

        [TestMethod]
        public void CheckExceptionsCompatibility()
        {
            Assembly asm = OpenTelemetryRecorderTests.GetAssemblyLocally(DllName);

            // Get all types (including internal) defined in the assembly
            IEnumerable<Type> actualClasses = asm
                .GetTypes()
                .Where(type => type.Namespace == "Microsoft.Azure.Cosmos" && type.Name.EndsWith("Exception"));

            foreach (Type className in actualClasses)
            {
                Assert.IsTrue(OpenTelemetryCoreRecorder.OTelCompatibleExceptions.Keys.Contains(className), $"{className.Name} is not added in {typeof(OpenTelemetryCoreRecorder).Name} Class OTelCompatibleExceptions dictionary");
            }

        }
        /// <summary>
        /// This test verifies whether OpenTelemetryResponse can be initialized using a specific type of response available in the SDK. 
        /// If any new response is added to the SDK in the future, it must be configured in either of the following dictionaries.
        /// </summary>
        [TestMethod]
        public async Task CheckResponseCompatibility()
        {
            // This list contains all the response types which are not compatible with Open Telemetry Response
            IReadOnlyList<string> excludedResponses = new List<string>()
            {
                "MediaResponse", // Part of dead code
                "DocumentFeedResponse`1",// Part of dead code
                "CosmosQuotaResponse",// Part of dead code
                "StoredProcedureResponse`1" // Not supported as of now
            };

            // This dictionary contains a Key-Value pair where the Key represents the Response Type compatible with Open Telemetry Response, and the corresponding Value is a mocked instance.
            // Essentially, at some point in the code, we send an instance of the response to Open Telemetry to retrieve relevant information.
            IDictionary<string, object> responseInstances = new Dictionary<string, object>()
            {
                { "PartitionKeyRangeBatchResponse", await OpenTelemetryRecorderTests.GetPartitionKeyRangeBatchResponse() },
                { "TransactionalBatchResponse", await OpenTelemetryRecorderTests.GetTransactionalBatchResponse() },
                { "QueryResponse", new Mock<QueryResponse>().Object },
                { "QueryResponse`1", OpenTelemetryRecorderTests.GetQueryResponseWithGenerics()},
                { "ReadFeedResponse`1", ReadFeedResponse<object>.CreateResponse<object> (new ResponseMessage(HttpStatusCode.OK),MockCosmosUtil.Serializer)},
                { "ClientEncryptionKeyResponse", new Mock<ClientEncryptionKeyResponse>().Object},
                { "ContainerResponse", new Mock<ContainerResponse>().Object },
                { "ItemResponse`1", new Mock<ItemResponse<object>>().Object },
                { "DatabaseResponse", new Mock<DatabaseResponse>().Object },
                { "PermissionResponse", new Mock<PermissionResponse>().Object },
                { "ThroughputResponse", new Mock<ThroughputResponse>().Object },
                { "UserResponse", new Mock<UserResponse>().Object },
                { "ChangeFeedEstimatorFeedResponse",  ChangefeedResponseFunc},
                { "ChangeFeedEstimatorEmptyFeedResponse", ChangeFeedEstimatorEmptyFeedResponseFunc },
                { "StoredProcedureExecuteResponse`1",new Mock<StoredProcedureExecuteResponse<object>>().Object },
                { "StoredProcedureResponse", new Mock<StoredProcedureResponse>().Object },
                { "TriggerResponse", new Mock<TriggerResponse>().Object },
                { "UserDefinedFunctionResponse", new Mock<UserDefinedFunctionResponse>().Object },
                { "HedgingResponse", "HedgingResponse" },
            };

            Assembly asm = OpenTelemetryRecorderTests.GetAssemblyLocally(DllName);

            // Get all types (including internal) defined in the assembly
            IEnumerable<Type> actualClasses = asm
                .GetTypes()
                .Where(type =>
                                (type.Name.EndsWith("Response") || type.Name.EndsWith("Response`1")) && // Ending with Response and Response<T>
                                !type.Name.Contains("OpenTelemetryResponse") && // Excluding OpenTelemetryResponse because we are testing this class
                                !type.IsAbstract && // Excluding abstract classes
                                !type.IsInterface && // Excluding interfaces
                                !excludedResponses.Contains(type.Name)); // Excluding all the types defined in excludedResponses list

            foreach (Type className in actualClasses)
            {
                Assert.IsTrue(responseInstances.TryGetValue(className.Name, out object instance), $" New Response type found i.e.{className.Name}");

                if (instance is TransactionalBatchResponse transactionInstance)
                {
                    _ = new OpenTelemetryResponse(transactionInstance);
                }
                else if (instance is ResponseMessage responseMessageInstance)
                {
                    _ = new OpenTelemetryResponse(responseMessageInstance);
                }
                else if (instance is FeedResponse<object> feedResponseInstance)
                {
                    _ = new OpenTelemetryResponse<object>(feedResponseInstance);
                }
                else if (instance is Response<object> responseInstance)
                {
                    _ = new OpenTelemetryResponse<object>(responseInstance);
                }
                else if (instance is Response<ClientEncryptionKeyProperties> encrypResponse)
                {
                    _ = new OpenTelemetryResponse<ClientEncryptionKeyProperties>(encrypResponse);
                }
                else if (instance is Response<ContainerProperties> containerPropertiesResponse)
                {
                    _ = new OpenTelemetryResponse<ContainerProperties>(containerPropertiesResponse);
                }
                else if (instance is Response<DatabaseProperties> databasePropertiesResponse)
                {
                    _ = new OpenTelemetryResponse<DatabaseProperties>(databasePropertiesResponse);
                }
                else if (instance is Response<PermissionProperties> permissionPropertiesResponse)
                {
                    _ = new OpenTelemetryResponse<PermissionProperties>(permissionPropertiesResponse);
                }
                else if (instance is Response<ThroughputProperties> throughputPropertiesResponse)
                {
                    _ = new OpenTelemetryResponse<ThroughputProperties>(throughputPropertiesResponse);
                }
                else if (instance is Response<UserProperties> userPropertiesResponse)
                {
                    _ = new OpenTelemetryResponse<UserProperties>(userPropertiesResponse);
                }
                else if (instance is Func<Type, FeedResponse<ChangeFeedProcessorState>> fucntion)
                {
                    _ = new OpenTelemetryResponse<ChangeFeedProcessorState>(fucntion(className));
                }
                else if (instance is Response<StoredProcedureProperties> storedProcedureResponse)
                {
                    _ = new OpenTelemetryResponse<StoredProcedureProperties>(storedProcedureResponse);
                }
                else if (instance is Response<TriggerProperties> triggerResponse)
                {
                    _ = new OpenTelemetryResponse<TriggerProperties>(triggerResponse);
                }
                else if (instance is Response<UserDefinedFunctionProperties> userDefinedFunctionResponse)
                {
                    _ = new OpenTelemetryResponse<UserDefinedFunctionProperties>(userDefinedFunctionResponse);
                }
                else if (instance is Response<StoredProcedureExecuteResponse<object>> storedProcedureExecuteResponse)
                {
                    _ = new OpenTelemetryResponse<StoredProcedureExecuteResponse<object>>(storedProcedureExecuteResponse);
                }
                else if (instance is string hedgingResponse)
                {
                    Assert.AreEqual(
                        "HedgingResponse",
                        hedgingResponse,
                        "HedgingResponse is only used internally in the CrossRegionHedgingAvailabilityStrategy and is never returned. No support Needed.");
                }
                else
                {
                    Assert.Fail("Opentelemetry does not support this response type " + className.Name);
                }
            }
        }

        private static readonly Func<Type, FeedResponse<ChangeFeedProcessorState>> ChangefeedResponseFunc = (Type type) =>
        {
            ConstructorInfo constructorInfo = type
                                    .GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(ITrace), typeof(ReadOnlyCollection<ChangeFeedProcessorState>), typeof(double) }, null);
            if (constructorInfo != null)
            {
                return (FeedResponse<ChangeFeedProcessorState>)constructorInfo.Invoke(
                    new object[] {
                NoOpTrace.Singleton, new List<ChangeFeedProcessorState>().AsReadOnly(), 10 });
            }

            return null;
        };

        private static readonly Func<Type, FeedResponse<ChangeFeedProcessorState>> ChangeFeedEstimatorEmptyFeedResponseFunc = (Type type) =>
        {
            ConstructorInfo constructorInfo = type
                                    .GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(ITrace) }, null);
            if (constructorInfo != null)
            {
                return (FeedResponse<ChangeFeedProcessorState>)constructorInfo.Invoke(
                    new object[] {
                NoOpTrace.Singleton});
            }

            return null;
        };

        private static QueryResponse<object> GetQueryResponseWithGenerics()
        {
            return QueryResponse<object>.CreateResponse<object>(
                    QueryResponse.CreateFailure(
                        new CosmosQueryResponseMessageHeaders(null, null, Documents.ResourceType.Document, "col"), HttpStatusCode.OK, new RequestMessage(), null, NoOpTrace.Singleton),
                    MockCosmosUtil.Serializer);
        }

        private static async Task<TransactionalBatchResponse> GetTransactionalBatchResponse(ItemBatchOperation[] arrayOperations = null)
        {
            if (arrayOperations == null)
            {
                arrayOperations = new ItemBatchOperation[1];
                arrayOperations[0] = new ItemBatchOperation(Documents.OperationType.Read, 0, new PartitionKey("0"));
            }

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
               partitionKey: null,
               operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
               serializerCore: MockCosmosUtil.Serializer,
               trace: NoOpTrace.Singleton,
               cancellationToken: default);

            return await TransactionalBatchResponse.FromResponseMessageAsync(
                new ResponseMessage(HttpStatusCode.OK) { Content = new System.IO.MemoryStream() },
                batchRequest,
                MockCosmosUtil.Serializer,
                true,
                NoOpTrace.Singleton,
                CancellationToken.None);
        }

        private static async Task<PartitionKeyRangeBatchResponse> GetPartitionKeyRangeBatchResponse()
        {
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];

            arrayOperations[0] = new ItemBatchOperation(Documents.OperationType.Read, 0, new PartitionKey("0"));
            PartitionKeyRangeBatchResponse partitionKeyRangeBatchResponse = new PartitionKeyRangeBatchResponse(
                arrayOperations.Length,
                await OpenTelemetryRecorderTests.GetTransactionalBatchResponse(arrayOperations),
                MockCosmosUtil.Serializer);

            return partitionKeyRangeBatchResponse;
        }

        [TestMethod]
        public void MarkFailedTest()
        {
            Assert.IsFalse(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new Exception(),
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosNullReferenceException(
                    new NullReferenceException(),
                    NoOpTrace.Singleton),
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosObjectDisposedException(
                    new ObjectDisposedException("dummyobject"),
                    MockCosmosUtil.CreateMockCosmosClient(),
                    NoOpTrace.Singleton),
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosOperationCanceledException(
                    new OperationCanceledException(),
                    new CosmosTraceDiagnostics(NoOpTrace.Singleton)),
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosException(
                    System.Net.HttpStatusCode.OK,
                    "dummyMessage",
                    "dummyStacktrace",
                    null,
                    NoOpTrace.Singleton,
                    default,
                    null),
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new ChangeFeedProcessorUserException(
                    new Exception(),
                    default),
                default));

            // If there is an unregistered exception is thrown, defaut exception wil be called
            Assert.IsFalse(OpenTelemetryCoreRecorder.IsExceptionRegistered(
              new NewCosmosException(),
              default));

            // If there is an child exception of non sealed exception, corresponding parent exception class will be called
            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
             new NewCosmosObjectDisposedException(
                 new ObjectDisposedException("dummyobject"),
                 MockCosmosUtil.CreateMockCosmosClient(),
                 NoOpTrace.Singleton),
             default));
        }
    }

    internal class NewCosmosException : System.Exception
    {
        /// <inheritdoc/>
        public override string Message => "dummy exception message";
    }

    internal class NewCosmosObjectDisposedException : CosmosObjectDisposedException
    {
        internal NewCosmosObjectDisposedException(ObjectDisposedException originalException, CosmosClient cosmosClient, ITrace trace)
            : base(originalException, cosmosClient, trace)
        {
        }

        /// <inheritdoc/>
        public override string Message => "dummy CosmosObjectDisposedException message";
    }

}