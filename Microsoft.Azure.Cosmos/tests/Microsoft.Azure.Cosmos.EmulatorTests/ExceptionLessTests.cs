namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [VisualStudio.TestTools.UnitTesting.TestClass]
    public class ExceptionLessTests
    {
        private readonly ConcurrentBag<Exception> Exceptions = new();

#nullable enable
        private void ExceptionCaptureHandler(object? sender, FirstChanceExceptionEventArgs eventArgs)
#nullable disable
        {
            this.Exceptions.Add(eventArgs.Exception);
        }

        [TestInitialize]
        public void TestInit()
        {
            // Subscribe to the FirstChanceException event
            AppDomain.CurrentDomain.FirstChanceException += this.ExceptionCaptureHandler;

            TraceSource traceSource = (TraceSource)typeof(DefaultTrace).GetProperty("TraceSource").GetValue(null);
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Clear();
            traceSource.Listeners.Add(new ConsoleTraceListener());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Subscribe to the FirstChanceException event
            AppDomain.CurrentDomain.FirstChanceException -= this.ExceptionCaptureHandler;
            this.Exceptions.Clear();
        }

        /// <summary>
        /// Test for exception less behavior with session not found scenarios
        /// </summary>
        [TestMethod]
        [Owner("kirankk")]
        [DataRow(ConnectionMode.Gateway, Cosmos.ConsistencyLevel.Session)]
        [DataRow(ConnectionMode.Direct, Cosmos.ConsistencyLevel.Session)]
        public async Task SessionNotFoundTestAsync(ConnectionMode mode,
            Cosmos.ConsistencyLevel consistencyLevel)
        {
            string databaseId = Guid.NewGuid().ToString();
            string containerId = Guid.NewGuid().ToString();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = mode,
                RequestTimeout = TimeSpan.FromHours(10),
                EnableUpgradeConsistencyToLocalQuorum = true,
            };

            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient(clientOptions))
            {
                await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
                await cosmosClient.GetDatabase(databaseId).CreateContainerIfNotExistsAsync(containerId, "/id");

                await cosmosClient.InitializeContainersAsync(new List<(string, string)>() { (databaseId, containerId) }, CancellationToken.None);

                Container container = cosmosClient.GetContainer(databaseId, containerId);
                ContainerProperties containerProperties = await container.ReadContainerAsync();

                TestObj testObj = new TestObj() { id = Guid.NewGuid().ToString() };

                ItemResponse<TestObj> createRespMsg = await container.CreateItemAsync<TestObj>(testObj, new Cosmos.PartitionKey(testObj.id));

                DocumentServiceRequest.DefaultUseStatusCodeFor4041002 = false;

                Trace.TraceInformation($"{Environment.NewLine}First Read (may be cold start) (UseStatusCodeFor4041002={DocumentServiceRequest.DefaultUseStatusCodeFor4041002})");
                ResponseMessage respMsg = await container.ReadItemStreamAsync(testObj.id, new Cosmos.PartitionKey(testObj.id),
                    new ItemRequestOptions() { ConsistencyLevel = consistencyLevel });
                this.TraceResponseMessageAndAssert(respMsg);

                string futureLsn = this.GetFutureLsn(respMsg.Headers.Session);
                Trace.TraceInformation($"{Environment.NewLine}Second ReadFor404-1002 (UseStatusCodeFor4041002={DocumentServiceRequest.DefaultUseStatusCodeFor4041002}): {futureLsn}");
                respMsg = await container.ReadItemStreamAsync(testObj.id, new Cosmos.PartitionKey(testObj.id),
                    new ItemRequestOptions() { SessionToken = futureLsn, ConsistencyLevel = consistencyLevel });
                SummaryDiagnostics summaryDiagnostics1 = new SummaryDiagnostics(((CosmosTraceDiagnostics)respMsg.Diagnostics).Value);
                this.TraceResponseMessageAndAssert(respMsg);

                Trace.TraceInformation($"{Environment.NewLine}Third Read (rebase if-any) (UseStatusCodeFor4041002={DocumentServiceRequest.DefaultUseStatusCodeFor4041002})");
                respMsg = await container.ReadItemStreamAsync(testObj.id, new Cosmos.PartitionKey(testObj.id),
                    new ItemRequestOptions() { ConsistencyLevel = consistencyLevel });
                this.TraceResponseMessageAndAssert(respMsg);

                DocumentServiceRequest.DefaultUseStatusCodeFor4041002 = true;

                Trace.TraceInformation($"{Environment.NewLine}ReadFor404-1002 (UseStatusCodeFor4041002={DocumentServiceRequest.DefaultUseStatusCodeFor4041002}): {futureLsn}");
                respMsg = await container.ReadItemStreamAsync(testObj.id, new Cosmos.PartitionKey(testObj.id),
                    new ItemRequestOptions() { SessionToken = futureLsn, ConsistencyLevel = consistencyLevel });
                SummaryDiagnostics summaryDiagnostics2 = new SummaryDiagnostics(((CosmosTraceDiagnostics)respMsg.Diagnostics).Value);
                this.TraceResponseMessageAndAssert(respMsg, expectedExceptionCount: 0);

                Assert.IsTrue(summaryDiagnostics1.AllRegionsContacted.Value.SetEquals(summaryDiagnostics2.AllRegionsContacted.Value), $"AllRegionsContacted");
                CollectionAssert.AreEquivalent(summaryDiagnostics1.GatewayRequestsSummary.Value, summaryDiagnostics2.GatewayRequestsSummary.Value, "GatewayRequestsSummary");

                // Direct #retries are expected to be different (exception vs exceptionless flows)
                if (mode == ConnectionMode.Direct)
                {
                    CollectionAssert.AreEquivalent(summaryDiagnostics1.DirectRequestsSummary.Value.Keys, summaryDiagnostics2.DirectRequestsSummary.Value.Keys);
                    Assert.AreEqual(1, summaryDiagnostics1.DirectRequestsSummary.Value.Keys.Count);

                    (int statusCode, int subStatusCode) = summaryDiagnostics1.DirectRequestsSummary.Value.Keys.First();
                    int exceptionFlowRetryCount = summaryDiagnostics1.DirectRequestsSummary.Value[(statusCode, subStatusCode)];
                    int exceptionLessFlowRetryCount = summaryDiagnostics2.DirectRequestsSummary.Value[(statusCode, subStatusCode)];
                    Assert.IsTrue(exceptionFlowRetryCount == exceptionLessFlowRetryCount
                        || (exceptionLessFlowRetryCount > exceptionFlowRetryCount && ((exceptionLessFlowRetryCount - exceptionFlowRetryCount) / exceptionFlowRetryCount * 100) < 10),
                    $"DirectRequestsSummary: {string.Join(Environment.NewLine, summaryDiagnostics1.DirectRequestsSummary.Value.Select(e => $"{e.Key} -> {e.Value}"))} {Environment.NewLine} {string.Join(Environment.NewLine, summaryDiagnostics2.DirectRequestsSummary.Value.Select(e => $"{e.Key} -> {e.Value}"))}");
                }

                // Delete the database
                await cosmosClient.GetDatabase(databaseId).DeleteAsync();
            }
        }

        private string GetFutureLsn(string sessionTokenStr)
        {
            if (SessionTokenHelper.TryParse(sessionTokenStr, out string partitionKeyRangeId, out ISessionToken parsedSessionToken))
            {
                VectorSessionToken vectorSessionToken = (VectorSessionToken)parsedSessionToken;
                if (vectorSessionToken != null)
                {
                    ISessionToken futureSessionToken = new VectorSessionToken(vectorSessionToken, vectorSessionToken.LSN + 50);
                    return $"{partitionKeyRangeId}:{futureSessionToken.ConvertToString()}";
                }
            }

            throw new ArgumentException($"Failed for {sessionTokenStr}");
        }

        private void TraceResponseMessageAndAssert(ResponseMessage respMsg,
            int? expectedExceptionCount = null)
        {
            IEnumerable<string> nonHttpExceptions = this.Exceptions.Select(e => e.StackTrace).Where(e => !e.Contains("System.Net.Http.HttpConnection"));
            int currentExceptionCount = nonHttpExceptions.Count();
            Trace.TraceInformation($"(StatusCode, SubStatusCode): {respMsg.StatusCode} -> {respMsg.Headers.SubStatusCode}");
            Trace.TraceInformation($"SessionToken(Request -> Response): {respMsg.RequestMessage.Headers.Session} -> {respMsg.Headers.Session}");
            Trace.TraceInformation($"Exception count: {currentExceptionCount}");
            Trace.TraceInformation($"Distinct Msg's: {string.Join(Environment.NewLine, this.Exceptions.Select(e => e.Message).GroupBy(e => e, (gpkey, gpValues) => $"{gpkey} -> {gpValues.Count()}"))}");
            Trace.TraceInformation(respMsg.Diagnostics.ToString());

            if (expectedExceptionCount.HasValue)
            {
                Assert.AreEqual(expectedExceptionCount, currentExceptionCount,
                    $"{string.Join(Environment.NewLine, nonHttpExceptions.Distinct())}");
            }

            this.Exceptions.Clear();
        }

        public class TestObj
        {
#pragma warning disable SA1300 // Element should begin with upper-case letter
            public string id { get; set; }
#pragma warning restore SA1300 // Element should begin with upper-case letter
        }
    }
}
