namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class NegativeQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestBadQueriesOverMultiplePartitionsAsync()
        {
            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                try
                {
                    FeedIterator<Document> resultSetIterator = container.GetItemQueryIterator<Document>(
                        @"SELECT * FROM Root r WHERE a = 1",
                        requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

                    await resultSetIterator.ReadNextAsync();

                    Assert.Fail($"Expected {nameof(CosmosException)}");
                }
                catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
                {
                    Assert.IsTrue(exception.Message.Contains(@"Identifier 'a' could not be resolved."),
                        exception.Message);
                }
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                QueryTestsBase.NoDocuments,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestQueryOnInvalidContainerReturnsNotFoundAsync()
        {
            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                // ignore the container and issue a query against a non-existent container.
                Container nonExistentContainer = container.Database.GetContainer(Guid.NewGuid().ToString());
                FeedIterator resultSetIterator = nonExistentContainer.GetItemQueryStreamIterator(
                    @"SELECT * FROM Root r WHERE a = 1",
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

                ResponseMessage message = await resultSetIterator.ReadNextAsync();
                Assert.AreEqual(HttpStatusCode.NotFound, message.StatusCode);
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                QueryTestsBase.NoDocuments,
                ImplementationAsync);
        }

        /// <summary>
        // "SELECT c._ts, c.id, c.TicketNumber, c.PosCustomerNumber, c.CustomerId, c.CustomerUserId, c.ContactEmail, c.ContactPhone, c.StoreCode, c.StoreUid, c.PoNumber, c.OrderPlacedOn, c.OrderType, c.OrderStatus, c.Customer.UserFirstName, c.Customer.UserLastName, c.Customer.Name, c.UpdatedBy, c.UpdatedOn, c.ExpirationDate, c.TotalAmountFROM c ORDER BY c._ts"' created an ArgumentOutofRangeException since ServiceInterop was returning DISP_E_BUFFERTOOSMALL in the case of an invalid query that is also really long.
        /// This test case just double checks that you get the appropriate document client exception instead of just failing.
        /// </summary>
        [TestMethod]
        public async Task TestQueryCrossParitionPartitionProviderInvalidAsync()
        {
            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                await QueryTestsBase.NoOp();
                try
                {
                    /// note that there is no space before the from clause thus this query should fail 
                    /// '"code":"SC2001","message":"Identifier 'c' could not be resolved."'
                    string query = "SELECT c._ts, c.id, c.TicketNumber, c.PosCustomerNumber, c.CustomerId, c.CustomerUserId, c.ContactEmail, c.ContactPhone, c.StoreCode, c.StoreUid, c.PoNumber, c.OrderPlacedOn, c.OrderType, c.OrderStatus, c.Customer.UserFirstName, c.Customer.UserLastName, c.Customer.Name, c.UpdatedBy, c.UpdatedOn, c.ExpirationDate, c.TotalAmountFROM c ORDER BY c._ts";
                    List<Document> expectedValues = new List<Document>();
                    FeedIterator<Document> resultSetIterator = container.GetItemQueryIterator<Document>(
                        query,
                        requestOptions: new QueryRequestOptions() { MaxConcurrency = 0 });

                    while (resultSetIterator.HasMoreResults)
                    {
                        expectedValues.AddRange(await resultSetIterator.ReadNextAsync());
                    }

                    Assert.Fail("Expected to get an exception for this query.");
                }
                catch (CosmosException e) when (e.StatusCode == HttpStatusCode.BadRequest)
                {
                }
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                QueryTestsBase.NoDocuments,
                ImplementationAsync);
        }

        [TestMethod]
        public async Task TestTopOffsetLimitClientRanges()
        {
            async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                await QueryTestsBase.NoOp();

                foreach((string parameterName, string query) in new[]
                    {
                        ("QueryInfo.Offset", "SELECT c.name FROM c OFFSET 2147483648 LIMIT 10"),
                        ("QueryInfo.Limit",  "SELECT c.name FROM c OFFSET 10 LIMIT 2147483648"),
                        ("QueryInfo.Top",    "SELECT TOP 2147483648 c.name FROM c"),
                    })
                try
                {
                    List<Document> expectedValues = new List<Document>();
                    FeedIterator<Document> resultSetIterator = container.GetItemQueryIterator<Document>(
                        query,
                        requestOptions: new QueryRequestOptions() { MaxConcurrency = 0 });

                    while (resultSetIterator.HasMoreResults)
                    {
                        expectedValues.AddRange(await resultSetIterator.ReadNextAsync());
                    }

                    Assert.Fail("Expected to get an exception for this query.");
                }
                catch (CosmosException e)
                {
                    Assert.IsTrue(e.StatusCode == HttpStatusCode.BadRequest);
                    Assert.IsTrue(e.InnerException?.InnerException is ArgumentException ex &&
                        ex.Message.Contains(parameterName));
                }
            }

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                QueryTestsBase.NoDocuments,
                ImplementationAsync);
        }
    }
}
