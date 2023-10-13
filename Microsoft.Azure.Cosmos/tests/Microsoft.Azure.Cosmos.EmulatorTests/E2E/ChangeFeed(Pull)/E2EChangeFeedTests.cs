//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.E2E.ChangeFeed_Pull_
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// The intention of these tests is to assert that change feed (pull model) is functioning as expected
    /// for LatestVersions and AllVersionsAndDeletes ChangeFeedMode requests against a test live database account endpoint.
    /// </summary>
    [Ignore]
    [TestClass]
    [TestCategory("E2E ChangeFeed")]
    public class E2EChangeFeedTests : BaseE2EChangeFeedTests
    {
        [TestMethod]
        [Owner("philipthomas")]
        [DataRow(10, 1)]
        public async Task GivenSplitContainerWhenPreLoadedDocumentsThenExpectsDocumentsTestAsync(
            int documentCount,
            int splitCount)
        {
            // AAA
            //   I. Arrange
            CosmosClient cosmosClient = this.CreateCosmosClient("Microsoft.Azure.Cosmos.SDK.EmulatorTests.E2E ChangeFeed");
            Database database = await this.CreateDatabaseAsync(cosmosClient);

            try
            {
                ContainerResponse containerResponse = await this.CreateAndSplitContainerAsync(
                    cosmosClient: cosmosClient,
                    database: database,
                    documentCount: documentCount,
                    splitCount: splitCount,
                    cancellationToken: this.CancellationToken);
                string continuationToken = BuildContinuationToken(containerResponse);

                FeedIterator<ChangeFeedItemChange<dynamic>> changeFeedIterator = containerResponse.Container.GetChangeFeedIterator<ChangeFeedItemChange<dynamic>>(
                    changeFeedStartFrom: ChangeFeedStartFrom.ContinuationToken(continuationToken),
                    changeFeedMode: ChangeFeedMode.AllVersionsAndDeletes,
                    changeFeedRequestOptions: new ChangeFeedRequestOptions
                    {
                        PageSizeHint = 5000,
                    });

                while (changeFeedIterator.HasMoreResults)
                {
                    int actualDocumentCount = await this.ReadDocumentsAsync(
                        retryAttempts: 0,
                        changeFeedIterator: changeFeedIterator,
                        cancellationToken: this.CancellationToken);

                    //   III. Assert

                    Assert.AreEqual(
                        expected: documentCount,
                        actual: actualDocumentCount);

                    Debug.WriteLine($"(E2E ChangeFeed){nameof(documentCount)}: {documentCount}");

                    break;
                }
            }
            catch (CosmosException cosmosException)
            {
                Debug.WriteLine($"(E2E ChangeFeed){nameof(cosmosException)}: {cosmosException}");
                Debug.WriteLine($"(E2E ChangeFeed){nameof(cosmosException.Diagnostics)}: {cosmosException.Diagnostics}");

                Assert.Fail();
            }
            finally
            {
                _ = database.DeleteAsync(cancellationToken: this.CancellationToken);

                Debug.WriteLine($"(E2E ChangeFeed)The database with an id of '{database.Id}' has been removed.");
            }
        }

        private async Task<int> ReadDocumentsAsync(
            int retryAttempts,
            FeedIterator<ChangeFeedItemChange<dynamic>> changeFeedIterator,
            CancellationToken cancellationToken)
        {
            bool shouldRetry;
            int actualDocumentCount = 0;

            do
            {
                try
                {
                    FeedResponse<ChangeFeedItemChange<dynamic>> feedResponse = await changeFeedIterator.ReadNextAsync(cancellationToken);
                    Debug.WriteLine($"(E2E ChangeFeed) Attempt successsful on retry attempt {retryAttempts}. {JsonConvert.SerializeObject(feedResponse.Resource)}");
                    actualDocumentCount += feedResponse.Count;
                    shouldRetry = false;
                    retryAttempts = 0;

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        break;
                    }
                }
                catch (CosmosException ex)
                {
                    Debug.WriteLine($"(E2E ChangeFeed){ex}");
                    Debug.WriteLine($"(E2E ChangeFeed){ex.Diagnostics}");

                    if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && ex.SubStatusCode == 3101)
                    {
                        shouldRetry = true;
                        retryAttempts++;

                        Debug.WriteLine($"(E2E ChangeFeed){nameof(retryAttempts)}: {retryAttempts}");
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
            } while (shouldRetry == true || retryAttempts > 10);

            return actualDocumentCount;
        }

        private static string BuildContinuationToken(ContainerResponse containerResponse)
        {
            int startingLSN = 1;
            string continuationToken = $@"{{""V"":2,""Rid"":""{containerResponse.Resource.ResourceId}"",""Continuation"":[{{""FeedRange"":{{""type"":""Effective Partition Key Range"",""value"":{{""min"":"""",""max"":""FF""}}}},""State"":{{""type"":""continuation"",""value"":""\""{startingLSN}\""""}}}}]}}";

            Debug.WriteLine($"(E2E ChangeFeed){nameof(continuationToken)}: {continuationToken}");

            return continuationToken;
        }
    }
}
