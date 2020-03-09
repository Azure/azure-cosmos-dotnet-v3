namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class NegativeCrossPartitionQueryTests : CrossPartitionQueryTestsBase
    {
        [TestMethod]
        public async Task TestBadQueriesOverMultiplePartitionsAsync()
        {
            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
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

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                CrossPartitionQueryTestsBase.NoDocuments,
                ImplementationAsync);
        }

        /// <summary>
        //"SELECT c._ts, c.id, c.TicketNumber, c.PosCustomerNumber, c.CustomerId, c.CustomerUserId, c.ContactEmail, c.ContactPhone, c.StoreCode, c.StoreUid, c.PoNumber, c.OrderPlacedOn, c.OrderType, c.OrderStatus, c.Customer.UserFirstName, c.Customer.UserLastName, c.Customer.Name, c.UpdatedBy, c.UpdatedOn, c.ExpirationDate, c.TotalAmountFROM c ORDER BY c._ts"' created an ArgumentOutofRangeException since ServiceInterop was returning DISP_E_BUFFERTOOSMALL in the case of an invalid query that is also really long.
        /// This test case just double checks that you get the appropriate document client exception instead of just failing.
        /// </summary>
        [TestMethod]
        public async Task TestQueryCrossParitionPartitionProviderInvalid()
        {
            async Task ImplementationAsync(Container container, IEnumerable<Document> documents)
            {
                await CrossPartitionQueryTestsBase.NoOp();
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

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.MultiPartition,
                CrossPartitionQueryTestsBase.NoDocuments,
                ImplementationAsync);
        }
    }
}
