namespace WriteWorkload
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
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

                int itemsPerSec = int.Parse(configuration["ItemsPerSec"]);

                using (CosmosClient client = new CosmosClientBuilder(endpoint, authKey).Build())
                {
                    Container container = client.GetContainer("samples", "source");
                    Stopwatch s = Stopwatch.StartNew();
                    while (true)
                    {
                        long startMs = s.ElapsedMilliseconds;
                        List<Task> tasks = new List<Task>();
                        for (int i = 0; i < itemsPerSec; i++)
                        {
                            MemoryStream docStream = GetNextDocItem(out PartitionKey pk);
                            tasks.Add(container.CreateItemStreamAsync(docStream, pk));
                        }
                        await Task.WhenAll(tasks);
                        long endMs = s.ElapsedMilliseconds;
                        if (endMs - startMs > 30)
                        {
                            await Task.Delay((int)(endMs - startMs - 30));
                        }
                    }
                }
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static MemoryStream GetNextDocItem(out PartitionKey partitionKeyValue)
        {
            string partitionKey = Guid.NewGuid().ToString();
            string id = Guid.NewGuid().ToString();

            MyDocument myDocument = new MyDocument() { id = id, pk = partitionKey, other = string.Empty };
            string value = JsonConvert.SerializeObject(myDocument);
            partitionKeyValue = new PartitionKey(partitionKey);

            return new MemoryStream(Encoding.UTF8.GetBytes(value));
        }


        private class MyDocument
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string other { get; set; }
        }
    }
}
