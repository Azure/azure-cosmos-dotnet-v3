namespace Cosmos.Samples.TransactionalBatch
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
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates the basic usage of the TransactionalBatch API that allows atomic CRUD operations against items
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
                // Read the Cosmos endpointUrl and authorizationKey from configuration.
                // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys".
                // Keep these values in a safe and secure location. Together they provide administrative access to your Cosmos account.
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

            // Delete the existing container to prevent create item conflicts.
            using (await Program.database.GetContainer(Program.containerId).DeleteContainerStreamAsync())
            { }

            Console.WriteLine("The demo will create a 1000 RU/s container, press any key to continue.");
            Console.ReadKey();

            // Create a container with a throughput of 1000 RU/s.
            await Program.database.DefineContainer(Program.containerId, "/GameId").CreateAsync(1000);
        }

        private static async Task RunDemoAsync()
        {
            Container gamesContainer = Program.database.GetContainer(containerId);

            // This code demonstrates interactions by a multi-player game service that hosts games with the database to save game state.
            // In this fictional game, players move about the 10x10 map and try to find balls. 
            // The objective is to collect 2 balls of the same color, or a golden ball if one appears.
            // After 5 minutes, if the game is not complete, the player with highest number of balls wins.
            int gameId = 420;
            int playerCount = 3;
            int ballCount = 4;
            List<GameBall> balls = new List<GameBall>();
            List<GameParticipant> players = new List<GameParticipant>();

            Console.WriteLine("At the start of the game, the balls are added on the map, and the players are added ...");

            // The below batch request is used to create the game balls and participants in an atomic fashion.
            TransactionalBatchResponse gameStartResponse = await gamesContainer.CreateTransactionalBatch(new PartitionKey(gameId))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Red, 4, 2))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Blue, 6, 4))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Blue, 8, 7))
                .CreateItem<GameBall>(GameBall.Create(gameId, Color.Red, 8, 8))
                .CreateItem<GameParticipant>(GameParticipant.Create(gameId, "alice"))
                .CreateItem<GameParticipant>(GameParticipant.Create(gameId, "bob"))
                .CreateItem<GameParticipant>(GameParticipant.Create(gameId, "carla"))
                .ExecuteAsync();

            GameParticipant alice, bob, carla;
            GameBall firstBlueBall, secondRedBall;

            using (gameStartResponse)
            {
                // Batch requests do not throw exceptions on execution failures as long as the request is valid, so we need to check the response status explicitly.
                // A HTTP 200 (OK) StatusCode on the batch response indicates that all operations succeeded.
                // An example later demonstrates a failure case.
                if (!gameStartResponse.IsSuccessStatusCode)
                {
                    // Log and handle failure
                    LogFailure(gameStartResponse);
                    return;
                }

                // Refresh in-memory state from response.
                // The TransactionalBatchResponse has a list of TransactionalBatchOperationResult, one for each operation within the batch request in the order
                // the operations were added to the TransactionalBatch.
                for (int index = 0; index < ballCount; index++)
                {
                    // The GetOperationResultAtIndex method returns the result of the operation at the given index with a Resource deserialized to the provided type.
                    TransactionalBatchOperationResult<GameBall> gameBallResult = gameStartResponse.GetOperationResultAtIndex<GameBall>(index);
                    balls.Add(gameBallResult.Resource);
                }

                firstBlueBall = balls[1];
                secondRedBall = balls[3];

                for (int index = ballCount; index < gameStartResponse.Count; index++)
                {
                    players.Add(gameStartResponse.GetOperationResultAtIndex<GameParticipant>(index).Resource);
                }

                alice = players.Single(p => p.Nickname == "alice");
                bob = players.Single(p => p.Nickname == "bob");
                carla = players.Single(p => p.Nickname == "carla");
            }

            PrintState(players, balls);

            Console.WriteLine("Alice goes to 6, 4 and finds a blue ball ...");
            alice.BlueCount++;

            // Upserts maybe used to replace items or create them if they are not already present.
            // An existing item maybe replaced along with concurrency checks the ETag returned in the responses of earlier requests on the item
            // or without these checks if they are not required.
            // Item deletes may also be a part of batch requests.
            TransactionalBatchResponse aliceFoundBallResponse = await gamesContainer.CreateTransactionalBatch(new PartitionKey(gameId))
                .UpsertItem<ParticipantLastActive>(ParticipantLastActive.Create(gameId, "alice"))
                .ReplaceItem<GameParticipant>(alice.Nickname, alice, new TransactionalBatchItemRequestOptions { IfMatchEtag = alice.ETag })
                .DeleteItem(firstBlueBall.Id)
                .ExecuteAsync();

            using (aliceFoundBallResponse)
            {
                if (!aliceFoundBallResponse.IsSuccessStatusCode)
                {
                    // Log and handle failure
                    alice.BlueCount--;
                    LogFailure(aliceFoundBallResponse);
                    return;
                }

                // Refresh in-memory state from response.
                balls.Remove(firstBlueBall);

                // We only update the etag as we have the rest of the state we care about here already as needed.
                alice.ETag = aliceFoundBallResponse[1].ETag;
            }

            PrintState(players, balls);

            Console.WriteLine("Bob goes to 8, 8 and finds a red ball ...");
            bob.RedCount++;

            // Stream variants for all batch operations that accept an item are also available for use when the item is available as a Stream.
            Stream bobIsActiveStream = ParticipantLastActive.CreateStream(gameId, "bob");
            Stream bobAsStream = Program.AsStream(bob);

            using (bobIsActiveStream)
            using (bobAsStream)
            {
                TransactionalBatchResponse bobFoundBallResponse = await gamesContainer.CreateTransactionalBatch(new PartitionKey(gameId))
                    .UpsertItemStream(bobIsActiveStream)
                    .ReplaceItemStream(bob.Nickname, bobAsStream, new TransactionalBatchItemRequestOptions { IfMatchEtag = bob.ETag })
                    .DeleteItem(secondRedBall.Id)
                    .ExecuteAsync();

                using (bobFoundBallResponse)
                {
                    if (!bobFoundBallResponse.IsSuccessStatusCode)
                    {
                        // Log and handle failure.
                        bob.RedCount--;
                        LogFailure(bobFoundBallResponse);
                        return;
                    }

                    // Refresh in-memory state from response.
                    balls.Remove(secondRedBall);

                    // The resultant item for each operation is also available as a Stream that can be used for example if the response is just
                    // going to be transferred to some other system.
                    Stream updatedPlayerAsStream = bobFoundBallResponse[1].ResourceStream;

                    bob = Program.FromStream<GameParticipant>(updatedPlayerAsStream);
                }
            }

            PrintState(players, balls);

            Console.WriteLine("A golden ball appears near each of the players to select an instant winner ...");
            TransactionalBatchResponse goldenBallResponse = await gamesContainer.CreateTransactionalBatch(new PartitionKey(gameId))
               .CreateItem<GameBall>(GameBall.Create(gameId, Color.Gold, 2, 2))
               .CreateItem<GameBall>(GameBall.Create(gameId, Color.Gold, 6, 3))
               // oops - there is already a ball at 8, 7
               .CreateItem<GameBall>(GameBall.Create(gameId, Color.Gold, 8, 7))
               .ExecuteAsync();

            using (goldenBallResponse)
            {
                // If an operation within the TransactionalBatch fails during execution, the TransactionalBatchResponse will have a status code of the failing operation.
                // The TransactionalBatchOperationResult entries within the response can be read to get details about the specific operation that failed.
                // The failing operation (for example if we have a conflict because we are trying to create an item 
                // that already exists) will have the StatusCode on its corresponding TransactionalBatchOperationResult set to the actual failure status
                // (HttpStatusCode.Conflict in this example). All other result entries will have a status code of HTTP 424 Failed Dependency.
                // In case any operation within a TransactionalBatch fails, no changes from the batch will be committed.
                // Other status codes such as HTTP 429 (Too Many Requests) and HTTP 5xx on server errors may also be returned on the TransactionalBatchResponse.
                if (!goldenBallResponse.IsSuccessStatusCode)
                {
                    if (goldenBallResponse.StatusCode == HttpStatusCode.Conflict)
                    {
                        for (int index = 0; index < goldenBallResponse.Count; index++)
                        {
                            TransactionalBatchOperationResult operationResult = goldenBallResponse[index];
                            if ((int)operationResult.StatusCode == 424)
                            {
                                // This operation failed because it was in a TransactionalBatch along with another operation where the latter was the actual cause of failure.
                                continue;
                            }
                            else if (operationResult.StatusCode == HttpStatusCode.Conflict)
                            {
                                Console.WriteLine("Creation of the {0}rd golden ball failed because there was already an existing ball at that position.", index + 1);
                            }
                        }
                    }
                    else
                    {
                        // Log and handle other failures
                        LogFailure(goldenBallResponse);
                        return;
                    }
                }
            }

            PrintState(players, balls);

            Console.WriteLine("We need to end the game now; determining the winner as the player with highest balls ...");

            // Batch requests may also be used to atomically read multiple items with the same partition key.
            TransactionalBatchResponse playersResponse = await gamesContainer.CreateTransactionalBatch(new PartitionKey(gameId))
                .ReadItem(alice.Nickname)
                .ReadItem(bob.Nickname)
                .ReadItem(carla.Nickname)
                .ExecuteAsync();

            GameParticipant winner = null;
            bool isTied = false;

            using (playersResponse)
            {
                if (!playersResponse.IsSuccessStatusCode)
                {
                    // Log and handle failure
                    LogFailure(playersResponse);
                    return;
                }

                for (int index = 0; index < playerCount; index++)
                {
                    GameParticipant current;

                    if (index == 0)
                    {
                        // The item returned can be made available as the required POCO type using GetOperationResultAtIndex.
                        // A single batch request can be used to read items that can be deserialized to different POCOs as well.
                        current = playersResponse.GetOperationResultAtIndex<GameParticipant>(index).Resource;
                    }
                    else
                    {
                        // The item returned can also instead be accessed directly as a Stream (for example to pass as-is to another component).
                        Stream aliceInfo = playersResponse[index].ResourceStream;

                        current = Program.FromStream<GameParticipant>(aliceInfo);
                    }

                    if (winner == null || current.TotalCount > winner.TotalCount)
                    {
                        winner = current;
                        isTied = false;
                    }
                    else if(current.TotalCount == winner.TotalCount)
                    {
                        isTied = true;
                    }
                }
            }

            if (!isTied)
            {
                Console.WriteLine($"{winner.Nickname} has won the game!\n");
            }
            else
            {
                Console.WriteLine("The game is a tie; there is no clear winner.\n");
            }
        }

        private static void PrintState(List<GameParticipant> players, List<GameBall> balls)
        {
            Console.WriteLine("{0,-8}{1,6}{2,6}{3,6}", "Nick", "Red", "Blue", "Total");
            foreach(GameParticipant player in players)
            {
                Console.WriteLine("{0,-8}{1,6}{2,6}{3,6}", player.Nickname, player.RedCount, player.BlueCount, player.TotalCount);
            }

            Console.Write("Ball positions: ");
            foreach(GameBall ball in balls)
            {
                Console.Write("[{0},{1}] ", ball.XCoord, ball.YCoord);
            }

            Console.WriteLine("\n===================================================================================\n");
        }

        private static void LogFailure(TransactionalBatchResponse batchResponse)
        {
            Console.WriteLine("Unexpected error in executing batch requests in the sample. Please retry the sample.");
            Console.WriteLine("Timestamp: {0}\nDiagnostics: {1}", DateTime.UtcNow, batchResponse.Diagnostics.ToString());
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
            Blue,
            Gold
        }

        private class GameBall
        {
            public int GameId { get; set; }

            public Color Color { get; set; }

            public int XCoord { get; set; }

            public int YCoord { get; set; }

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

            public override bool Equals(object obj)
            {
                var ball = obj as GameBall;
                return ball != null &&
                       GameId == ball.GameId &&
                       Color == ball.Color &&
                       XCoord == ball.XCoord &&
                       YCoord == ball.YCoord &&
                       Id == ball.Id;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(GameId, Color, XCoord, YCoord, Id);
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

            public int BlueCount { get; set; }

            public int TotalCount { get { return this.RedCount + this.BlueCount; } }

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

            public override bool Equals(object obj)
            {
                var participant = obj as GameParticipant;
                return participant != null &&
                       GameId == participant.GameId &&
                       Nickname == participant.Nickname &&
                       RedCount == participant.RedCount &&
                       BlueCount == participant.BlueCount;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(GameId, Nickname, RedCount, BlueCount);
            }
        }
    }
}

