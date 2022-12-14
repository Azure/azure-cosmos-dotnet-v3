namespace Cosmos.Samples.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;

    internal class Program
    {
        private static string databaseName = "";
        private static string containerName = "";

        private static int feedRangeStartIndex = 0;
        private static int feedRangeCount = 0;

        public static async Task Main(string[] args)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .AddCommandLine(args)
                    .Build();

                databaseName = configuration["databaseName"];
                containerName = configuration["containerName"];
                feedRangeStartIndex = int.Parse(configuration["feedRangeStartIndex"]);
                feedRangeCount = int.Parse(configuration["feedRangeCount"]);
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
                    await Program.RunDemoAsync(client);
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
        }

        private class IdAndTs
        {
            public string Id { get; set; }
            public string Rid { get; set; }
            public string ETag { get; set; }
            public long TS { get; set; }

            public override int GetHashCode()
            {
                return this.Id.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return (obj as IdAndTs).Id == this.Id;
            }
        }

        private static async Task RunDemoAsync(CosmosClient client)
        {
            Container container = client.GetDatabase(databaseName).GetContainer(containerName);

            ContainerProperties containerProperties = await container.ReadContainerAsync();
            string[] selfLinkSegments = containerProperties.SelfLink.Split('/');
            string databaseRid = selfLinkSegments[1];
            string containerRid = selfLinkSegments[3];
            Container containerByRid = client.GetContainer(databaseRid, containerRid);

            QueryDefinition query = new QueryDefinition("SELECT c.id, c.ky, c._ts, c._rid, c._etag FROM c ORDER by c.ky asc");
            IReadOnlyList<FeedRange> ranges = await container.GetFeedRangesAsync();
            List<Task> tasks = new List<Task>();
            long tot = 0;
            long dupe = 0;
            DateTime startDateTime = DateTime.UtcNow;
            foreach (FeedRange range1 in ranges.Skip(feedRangeStartIndex).Take(feedRangeCount))
            {
                FeedRange range = range1;
                Console.WriteLine(range.ToJsonString());
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        string prevky = null;
                        HashSet<IdAndTs> idAndTss = new HashSet<IdAndTs>();

                        using (FeedIterator<JObject> resultSetIterator = container.GetItemQueryIterator<JObject>
                            (range, query))
                        {
                            while (resultSetIterator.HasMoreResults)
                            {
                                FeedResponse<JObject> response = await resultSetIterator.ReadNextAsync();
                                Interlocked.Add(ref tot, response.Count);
                                foreach (JObject result in response)
                                {
                                    string currentky = result["ky"].Value<string>();
                                    IdAndTs current = new IdAndTs();
                                    current.Id = result["id"].Value<string>();
                                    current.Rid = result["_rid"].Value<string>();
                                    current.ETag = result["_etag"].Value<string>();
                                    current.TS = result["_ts"].Value<long>();

                                    if (prevky != currentky)
                                    {
                                        idAndTss.Clear();
                                    }

                                    if (idAndTss.TryGetValue(current, out IdAndTs old))
                                    {
                                        Interlocked.Increment(ref dupe);
                                        if (old.TS < current.TS)
                                        {
                                            ResponseMessage r = await containerByRid.DeleteItemStreamAsync(
                                                old.Rid,
                                                new PartitionKey(currentky),
                                                new ItemRequestOptions()
                                                {
                                                    IfMatchEtag = old.ETag
                                                }
                                            );

                                            r.EnsureSuccessStatusCode();
                                        }
                                        else if (current.TS < old.TS)
                                        {
                                            ResponseMessage r = await containerByRid.DeleteItemStreamAsync(
                                                current.Rid,
                                                new PartitionKey(currentky),
                                                new ItemRequestOptions()
                                                {
                                                    IfMatchEtag = current.ETag
                                                }
                                            );

                                            r.EnsureSuccessStatusCode();
                                        }
                                        else
                                        {
                                            throw new Exception("Same ts" + currentky + " " + current.Id + " " + current.TS + " " + current.Rid + " " + old.Rid);
                                        }
                                    }

                                    idAndTss.Add(current);
                                    prevky = currentky;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error " + range1.ToJsonString() + " " + ex);
                    }
                }));
            }

            await Task.WhenAny(Task.WhenAll(tasks), Task.Run(async () =>
            {
                while (true)
                {
                    Console.WriteLine((DateTime.UtcNow - startDateTime).ToString("c") + " " + Interlocked.Read(ref dupe) + " " + Interlocked.Read(ref tot));
                    await Task.Delay(5000);
                }
            }));
        }
    }
}