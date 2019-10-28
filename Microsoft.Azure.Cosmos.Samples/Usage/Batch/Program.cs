namespace Cosmos.Samples
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://azure.microsoft.com/en-us/itemation/articles/itemdb-create-account/
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic usage of the Batch API that allows atomic CRUD operations against items
    // that have the same partition key in a container.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private const string databaseId = "samples";
        private const string containerId = "batchApi";
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static Database database = null;

        // <Main>
        public static async Task Main(string[] args)
        {
            try
            {
                // Read the Cosmos endpointUrl and authorisationKeys from configuration
                // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                // Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
                }

                string authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
                }

                // Read the Cosmos endpointUrl and authorization key from configuration
                // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                // NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await Program.InitializeAsync(client);
                    await Program.RunDemoAsync();
                    await Program.CleanupAsync();
                }
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        private static async Task InitializeAsync(CosmosClient client)
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(Program.databaseId);

            // Delete the existing container to prevent create item conflicts
            using (await Program.database.GetContainer(containerId).DeleteContainerStreamAsync())
            { }

            Console.WriteLine("The demo will create a 1000 RU/s container, press any key to continue.");
            Console.ReadKey();

            // Create a container with a throughput of 1000 RU/s
            await Program.database.DefineContainer(containerId, "/GameId").CreateAsync(1000);
        }

        private static async Task RunDemoAsync()
        {
            Container gamesContainer = Program.database.GetContainer(containerId);

            // This code demonstrates interactions by a multi-player game service that hosts games with the database to save game state.
            // The objective of this game is to collect 2 balls of the same color, or a golden ball.
            // Players move about the 10x10 map and try to find balls. 
            // When held by players, balls may disappear after a while.
            // After 5 minutes, if the game is not complete, the player with highest number of balls wins.

            // =========== At the start of the game, the balls are added on the map, and the players are added ======================================
            int gameId = 420;
            int playerCount = 3;
            int ballCount = 5;

            // The below batch request is used to create the game balls and participants in an atomic fashion
            BatchResponse gameStartResponse = await gamesContainer.CreateBatch(new PartitionKey(gameId))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Red, 4, 2))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Green, 5, 3))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Blue, 6, 4))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Green, 8, 7))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Red, 8, 8))
                .CreateItem<GameParticipant>(GameParticipant.Create(gameId, "alice"))
                .CreateItem<GameParticipant>(GameParticipant.Create(gameId, "bob"))
                .CreateItem<GameParticipant>(GameParticipant.Create(gameId, "carla"))
                .ExecuteAsync();

            GameBall[] balls = new GameBall[ballCount];
            GameParticipant alice, bob, carla;
            GameBall blueBall, secondRedBall;

            using (gameStartResponse)
            {
                // Batch requests do not throw exceptions on execution failures as long as the request is valid, so we need to check the response status explicitly.
                // A HTTP 200 (OK) StatusCode on the BatchResponse indicates that all operations succeeded.
                // If one or more operations within the batch have failed, HTTP 207 (Multistatus) status code may be returned (example later).
                // Other status codes such as HTTP 429 (Too Many Requests) and HTTP 5xx on server errors may also be returned.
                // Given a batch request is atomic, in case any operation within a batch fails, no changes from the batch will be committed.
                if (gameStartResponse.StatusCode != HttpStatusCode.OK)
                {
                    // log exception and handle failure
                }


                // Refresh in-memory state from response
                // The BatchResponse has a list of BatchOperationResult, one for each operation within the batch request in the order of operations.
                for (int index = 0; index < ballCount; index++)
                {
                    // The GetOperationResultAtIndex method returns the result of the batch operation with a Resource deserialized to the provided type.
                    BatchOperationResult<GameBall> gameBallResult = gameStartResponse.GetOperationResultAtIndex<GameBall>(index);
                    balls[index] = gameBallResult.Resource;
                }

                blueBall = balls[3];
                secondRedBall = balls[4];

                List<GameParticipant> players = new List<GameParticipant>();
                for (int index = ballCount; index < gameStartResponse.Count; index++)
                {
                    players.Add(gameStartResponse.GetOperationResultAtIndex<GameParticipant>(index).Resource);
                }

                alice = players.Single(p => p.Nickname == "alice");
                bob = players.Single(p => p.Nickname == "bob");
                carla = players.Single(p => p.Nickname == "carla");
            }

            //  =========== Alice goes to 6, 4 and finds a blue ball ================================================================================
            blueBall.AssignedToNickName = "alice";
            alice.BlueCount++;

            // Upserts maybe used to replace items, or create them if they are not already present.
            // Existing entities maybe replaced along with concurrency checks using ETags returned in the responses of earlier requests on the item
            // or without these checks when they are not required.
            BatchResponse aliceFoundBallResponse = await gamesContainer.CreateBatch(new PartitionKey(gameId))
                .UpsertItem<ParticipantLastActive>(ParticipantLastActive.Create(gameId, "alice"))
                .ReplaceItem<GameBall>(blueBall.Id, blueBall, new BatchItemRequestOptions { IfMatchEtag = blueBall.ETag })
                .ReplaceItem<GameParticipant>(alice.Nickname, alice)
                .ExecuteAsync();

            using (aliceFoundBallResponse)
            {
                if (aliceFoundBallResponse.StatusCode != HttpStatusCode.OK)
                {
                    // log exception and handle failure
                }

                // Refresh in-memory state
                // We only update the etag as we have the rest of the state we care about here already as needed.
                blueBall.ETag = aliceFoundBallResponse[1].ETag;
                alice.ETag = aliceFoundBallResponse[2].ETag;
            }

            //  =========== Bob goes to 8, 8 and finds a red ball ===================================================================================
            secondRedBall.AssignedToNickName = "bob";
            bob.RedCount++;

            // Stream variants for all batch operations that accept an item are also available for use when the item is available as a Stream.
            Stream bobIsActiveStream = ParticipantLastActive.CreateStream(gameId, "bob");

            BatchResponse bobFoundBallResponse = await gamesContainer.CreateBatch(new PartitionKey(gameId))
                .UpsertItemStream(bobIsActiveStream)
                .ReplaceItemStream(secondRedBall.Id, Program.AsStream(secondRedBall), new BatchItemRequestOptions { IfMatchEtag = secondRedBall.ETag })
                .ReplaceItemStream(bob.Nickname, Program.AsStream(bob))
                .ExecuteAsync();

            using (bobFoundBallResponse)
            {
                if (bobFoundBallResponse.StatusCode != HttpStatusCode.OK)
                {
                    // log exception and handle failure
                }

                // Refresh in-memory state
                // The resultant item for the operations is also available as a Stream that can be used for example if the response is just
                // going to be transferred to some other system.
                Stream updatedBallAsStream = bobFoundBallResponse[1].ResourceStream;
                blueBall = Program.FromStream<GameBall>(updatedBallAsStream);

                bob.ETag = bobFoundBallResponse[2].ETag;
            }

            // ============ Alice has held the blue ball for too long and the ball disappears =======================================================
            alice.BlueCount--;

            BatchResponse deleteBallResponse = await gamesContainer.CreateBatch(new PartitionKey(gameId))
               .ReplaceItem<GameParticipant>(alice.Nickname, alice)
               .DeleteItem(blueBall.Id)
               .ExecuteAsync();

            using (deleteBallResponse)
            {
                if (deleteBallResponse.StatusCode != HttpStatusCode.OK)
                {
                    // log exception and handle failure
                }
            }

            // =========== Add a golden ball near each of the players to see if any of them pick it up ==============================================
            BatchResponse goldenBallResponse = await gamesContainer.CreateBatch(new PartitionKey(gameId))
               .CreateItem<GameBall>(GameBall.Create(gameId, Color.Gold, 2, 2))
               .CreateItem<GameBall>(GameBall.Create(gameId, Color.Gold, 6, 3))
               // oops - there is already a ball at 8, 7
               .CreateItem<GameBall>(GameBall.Create(gameId, Color.Gold, 8, 7))
               .ExecuteAsync();

            using (goldenBallResponse)
            {
                // If one or more operations within the batch have failed, a HTTP Status Code of 207 MultiStatus could be returned 
                // as the StatusCode of the BatchResponse that indicates that 
                // the response needs to be examined to understand details of the execution and failure. 
                // The first operation to fail (for example if we have a conflict because we are trying to create an item 
                // that already exists) will have the StatusCode on its corresponding BatchOperationResult set with the actual failure reason
                // (HttpStatusCode.Conflict in this example).
                // All other operations will be aborted - these would return a status code of HTTP 424 Failed Dependency.
                //
                // Other status codes such as HTTP 429 (Too Many Requests) and HTTP 5xx on server errors may also be returned for the BatchResponse.
                // Given a batch request is atomic, in case any operation within a batch fails, no changes from the batch will be committed.
                if (goldenBallResponse.StatusCode != HttpStatusCode.OK)
                {
                    foreach (BatchOperationResult operationResult in goldenBallResponse)
                    {
                        if ((int)operationResult.StatusCode == 424)
                        {
                            // This operation failed because it was batched along with another operation where the latter
                            // was the actual cause of failure.
                            continue;
                        }

                        // Log and handle failure
                    }
                }
            }

            // ========== 5 minutes have passed, but we don't have a winner yet. Determine winner as player with highest balls ======================
            BatchResponse playersResponse = await gamesContainer.CreateBatch(new PartitionKey(gameId))
                .ReadItem(alice.Nickname)
                .ReadItem(bob.Nickname)
                .ReadItem(carla.Nickname)
                .ExecuteAsync();

            GameParticipant winner = null;

            using (playersResponse)
            {
                if (playersResponse.StatusCode != HttpStatusCode.OK)
                {
                    // log exception and handle failure
                }

                for (int index = 0; index < playerCount; index++)
                {
                    GameParticipant current = playersResponse.GetOperationResultAtIndex<GameParticipant>(index).Resource;

                    // Not handling ties
                    if (winner == null || current.TotalCount > winner.TotalCount)
                    {
                        winner = current;
                    }
                }
            }
        }

        private static async Task CleanupAsync()
        {
            if (Program.database != null)
            {
                await Program.database.DeleteAsync();
            }
        }

        private static Stream AsStream<T>(T obj)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj)));
        }

        private static T FromStream<T>(Stream stream)
        {
            StreamReader streamReader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<T>(streamReader.ReadToEnd());
        }

        private enum Color
        {
            Red,
            Green,
            Blue,
            Gold
        }

        private class GameBall
        {
            public int GameId { get; set; }

            public Color Color { get; set; }

            public int XCoord { get; set; }

            public int YCoord { get; set; }

            public string AssignedToNickName { get; set; }

            [JsonProperty("id")]
            internal string Id { get { return $"{XCoord}:{YCoord}"; } }

            [JsonProperty("_etag")]
            internal string ETag { get; set; }

            public static GameBall Create(int gameId, Color color, int xCoord, int yCoord)
            {
                return new GameBall()
                {
                    GameId = gameId,
                    Color = color,
                    XCoord = xCoord,
                    YCoord = yCoord
                };
            }
        }

        public class ParticipantLastActive
        {
            public int GameId { get; set; }

            public string Nickname { get; set; }

            [JsonProperty("id")]
            internal string Id { get { return $"Activity_{Nickname}"; } }

            public DateTime LastActive { get; set; }

            public static ParticipantLastActive Create(int gameId, string nickname)
            {
                return new ParticipantLastActive
                {
                    GameId = gameId,
                    Nickname = nickname,
                    LastActive = DateTime.UtcNow
                };
            }

            public static Stream CreateStream(int gameId, string nickname)
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ParticipantLastActive.Create(gameId, nickname))));
            }
        }

        public class GameParticipant
        {
            public int GameId { get; set; }

            [JsonProperty("id")]
            public string Nickname { get; set; }

            public int RedCount { get; set; }

            public int GreenCount { get; set; }

            public int BlueCount { get; set; }

            public int TotalCount { get { return this.RedCount + this.GreenCount + this.BlueCount; } }

            [JsonProperty("_etag")]
            internal string ETag { get; set; }

            public static GameParticipant Create(int gameId, string nickname)
            {
                return new GameParticipant
                {
                    GameId = gameId,
                    Nickname = nickname,
                };
            }
        }
    }
}

