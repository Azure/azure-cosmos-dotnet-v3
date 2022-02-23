namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos;
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.IO;

    [TestClass]
    public class BinaryVsTextPerfTest
    {
        private static Database CosmosDatabase;
        private readonly FindMetrics FindMetrics = new FindMetrics();
        private readonly List<double> EndtoEndTime = new List<double>();
        private readonly List<double> PocoTime = new List<double>();
        private readonly List<double> GetCosmosElementResponseTime = new List<double>();
        private static readonly string CosmosDatabaseId = "db";
        private static readonly string ContainerId = "container";
        private readonly string ContentSerialization = "JsonText";
        private readonly string Query = "SELECT VALUE COUNT(1) FROM c WHERE c.region = 'Utah'";
        private readonly int NumberOfIterations = 20;
        private readonly int WarmupRounds = 3;


        [TestMethod]
        public async Task ConnectEndpoint()
        {
            try
            {
                string BinaryEndpoint = "https://balaperu-stagetestdb-eastus2.documents-staging.windows-ppe.net";
                string BinaryMasterKey = "miX1FrTOsDqD2QmUQeBdZlqkP9IJWWAurynHG6Wd9PMPeJ5V0VSu61sOyvblVhK7bAIVMoHzzXN2WcA2GdOrQQ==";
                string TextEndpoint = "https://binarycomparison-text.documents.azure.com:443/";
                string TextMasterKey = "AnMJJ3UDgzQ0nynskY4bmWMqD59mE6Y5ozSiSrvdCB9VN3QpTMSt2NE79JhTmmHGlSa8I4iVdua4aFBzuk4d7w==";

                using (CosmosClient client = new CosmosClient(BinaryEndpoint, BinaryMasterKey,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Direct
                    }))
                {
                    await this.RunAsync(client);
                }
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
                return;
            }
        }

        public async Task RunAsync(CosmosClient client)
        {
            CosmosDatabase = client.GetDatabase(CosmosDatabaseId);
            Container container = CosmosDatabase.GetContainer(ContainerId);
            string strFilePath = @"C:\Users\hdetroja\source\repos\Data.csv";
            StringBuilder sbOutput = new StringBuilder();
            for (int i = 0; i < this.NumberOfIterations; i++)
            {
                await this.RunQueryAsync(container, this.Query);
            }

            using (StreamWriter file = File.CreateText(strFilePath))
            {
                foreach (double arr in this.EndtoEndTime)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.RetrievedDocumentCount)
                {
                    file.WriteLine(string.Join(",", arr)); 
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.RetrievedDocumentSize)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.OutputDocumentCount)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.OutputDocumentSize)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.TotalQueryExecutionTime)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.DocumentLoadTime)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.DocumentWriteTime)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.Created)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.ChannelAcquisitionStarted)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.Pipelined)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.TransitTime)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.Received)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.FindMetrics.Completed)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.PocoTime)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.WriteLine();
                foreach (double arr in this.GetCosmosElementResponseTime)
                {
                    file.WriteLine(string.Join(",", arr));
                }
                file.Close();
            }

            Console.WriteLine(this.CalculateAverage(this.EndtoEndTime));
            Console.WriteLine((double)this.FindMetrics.RetrievedDocumentCount.Count / 20);
            if(this.FindMetrics.RetrievedDocumentCount.Count > 20) 
            {
                List<double> DistinctList = this.FindMetrics.RetrievedDocumentCount.Distinct().ToList();
                Console.WriteLine();
                DistinctList.ForEach(Console.WriteLine);
                Console.WriteLine();
            }
            else { Console.WriteLine(this.CalculateAverage(this.FindMetrics.RetrievedDocumentCount));  }
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.RetrievedDocumentSize));
            if (this.FindMetrics.OutputDocumentCount.Count > 20)
            {
                List<double> DistinctList = this.FindMetrics.OutputDocumentCount.Distinct().ToList();
                Console.WriteLine();
                DistinctList.ForEach(Console.WriteLine);
                Console.WriteLine();
            }
            else { Console.WriteLine(this.CalculateAverage(this.FindMetrics.OutputDocumentCount)); }
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.OutputDocumentSize));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.TotalQueryExecutionTime));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.DocumentLoadTime));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.DocumentWriteTime));
            Console.WriteLine();
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.Created));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.ChannelAcquisitionStarted));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.Pipelined));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.TransitTime));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.Received));
            Console.WriteLine(this.CalculateAverage(this.FindMetrics.Completed));
            Console.WriteLine();
            Console.WriteLine(this.CalculateAverage(this.PocoTime));
            Console.WriteLine(this.CalculateAverage(this.GetCosmosElementResponseTime));
            Console.WriteLine("\nMedian\n");
            Console.WriteLine(this.CalculateMedian(this.EndtoEndTime));
            Console.WriteLine((double)this.FindMetrics.TotalQueryExecutionTime.Count / 20);
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.RetrievedDocumentCount));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.RetrievedDocumentSize));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.OutputDocumentCount));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.OutputDocumentSize));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.TotalQueryExecutionTime));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.DocumentLoadTime));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.DocumentWriteTime));
            Console.WriteLine();
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.Created));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.ChannelAcquisitionStarted));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.Pipelined));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.TransitTime));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.Received));
            Console.WriteLine(this.CalculateMedian(this.FindMetrics.Completed));
            Console.WriteLine();
            Console.WriteLine(this.CalculateMedian(this.PocoTime));
            Console.WriteLine(this.CalculateMedian(this.GetCosmosElementResponseTime));
        }

        public async Task RunQueryAsync(Container container, string sql)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            using (FeedIterator<dynamic> iterator = container.GetItemQueryIterator<dynamic>(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = -1,
                    CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                        this.ContentSerialization,
                        (content) => JsonNavigator.Create(content),
                        () => JsonWriter.Create(JsonSerializationFormat.Text))
                }))
            {
                while (iterator.HasMoreResults)
                {
                    FeedResponse<dynamic> response = await iterator.ReadNextAsync();
                    stopwatch.Stop();
                    this.GetTrace(response);
                    stopwatch.Start();
                }
            }
            stopwatch.Stop();
            this.EndtoEndTime.Add(stopwatch.ElapsedMilliseconds);
        }

        private void GetTrace(FeedResponse<dynamic> Response)
        {
            ITrace Trace = ((CosmosTraceDiagnostics)Response.Diagnostics).Value;
            BinaryVsTextPerfTest BinaryPerf = new BinaryVsTextPerfTest();
            ITrace BackendMetrics = BinaryPerf.FindQueryBackendMetrics(Trace);
            if (BackendMetrics != null)
            {
                foreach (KeyValuePair<string, object> kvp in BackendMetrics.Data)
                {
                    this.FindMetrics.Visit((QueryMetricsTraceDatum)kvp.Value);
                    break;
                }
            }
            ITrace TransitMetrics = BinaryPerf.FindQueryTransitMetrics(Trace);
            if (TransitMetrics != null)
            {
                foreach (KeyValuePair<string, object> kvp in TransitMetrics.Data)
                {
                    this.FindMetrics.Visit((ClientSideRequestStatisticsTraceDatum)kvp.Value);
                    break;
                }
            }
            ITrace Poco = BinaryPerf.FindQueryClientMetrics(Trace, "POCO Materialization");
            if (Poco != null)
            {
                this.PocoTime.Add(Poco.Duration.TotalMilliseconds);
            }
            ITrace GetCosmosElementResponse = BinaryPerf.FindQueryClientMetrics(Trace, "Get Cosmos Element Response");
            if (GetCosmosElementResponse != null)
            {
                this.GetCosmosElementResponseTime.Add(GetCosmosElementResponse.Duration.TotalMilliseconds);
            }
        }

        private ITrace FindQueryBackendMetrics(ITrace trace)
        {
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);

            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (node.Data.ContainsKey("Query Metrics"))
                {
                    return node;
                }
                foreach (ITrace child in node.Children)
                    queue.Enqueue(child);
            }
            return null;
        }

        private ITrace FindQueryTransitMetrics(ITrace trace)
        {
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);

            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (node.Data.ContainsKey("Client Side Request Stats") & node.Name == "Microsoft.Azure.Documents.ServerStoreModel Transport Request")
                {
                    return node;
                }
                foreach (ITrace child in node.Children)
                    queue.Enqueue(child);
            }
            return null;
        }

        private ITrace FindQueryClientMetrics(ITrace trace, string name)
        {
            Queue<ITrace> queue = new Queue<ITrace>();
            queue.Enqueue(trace);

            while (queue.Count > 0)
            {
                ITrace node = queue.Dequeue();
                if (node.Name == name)
                {
                    return node;
                }
                foreach (ITrace child in node.Children)
                    queue.Enqueue(child);
            }
            return null;
        }

        private double CalculateAverage(List<double> AvgList)
        {
            AvgList = AvgList.GetRange(this.WarmupRounds, AvgList.Count - this.WarmupRounds);
            return Queryable.Average(AvgList.AsQueryable());
        }

        private double CalculateMedian(List<double> MedianList)
        {
            MedianList = MedianList.GetRange(this.WarmupRounds, MedianList.Count - this.WarmupRounds);
            int ListCount = MedianList.Count();
            int Mid = MedianList.Count() / 2;
            IOrderedEnumerable<double> SortedNumbers = MedianList.OrderBy(n => n);
            double Median = (ListCount % 2) == 0
                ? (SortedNumbers.ElementAt(Mid) + SortedNumbers.ElementAt(Mid - 1)) / 2
                : SortedNumbers.ElementAt(Mid);
            return Median;
        }
    }
}
