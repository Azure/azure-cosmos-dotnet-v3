namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.EmulatorTests.Tracing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using OpenTelemetry.Trace;
    using OpenTelemetry;
    using AzureCore = global::Azure.Core;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using System.Runtime.InteropServices;
    using System.Globalization;
    using System.Threading;

    [VisualStudio.TestTools.UnitTesting.TestClass]
    public sealed class DistributedTracingOTelTests
    {
        public static CosmosClient client;
        public static Database database;
        public static Container container;

        ////[DataRow( $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Operation")]
        //[DataRow( $"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")]
        //[TestMethod]
        //public async Task OperationScopeEnabled(string source)
        //{
        //    AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
        //    AzureCore.ActivityExtensions.ResetFeatureSwitch();
        //    CustomOtelExporter exporter = new CustomOtelExporter();
        //    using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
        //        .AddCustomOtelExporter()
        //        .AddSource(source)
        //        .Build())
        //    {
        //        client = TestCommon.CreateCosmosClient(
        //            useGateway: false,
        //            enableDistributingTracing: true);

        //        database = await client.CreateDatabaseAsync(
        //                Guid.NewGuid().ToString(),
        //                cancellationToken: default);

        //        List<Activity> a = CustomOtelExporter.CollectedActivities.ToList();
        //        Console.WriteLine(a);
        //        Assert.AreEqual(1, CustomOtelExporter.CollectedActivities.Count());
        //    }
        //}

        [TestMethod]
        public async Task NetworkScopeEnabled()
        {
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            CustomOtelExporter exporter = new CustomOtelExporter();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.Request")
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                    useGateway: false,
                    enableDistributingTracing: true);

                database = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);

                List<Activity> activityCollection = exporter.ExportData();
                Assert.AreEqual(1, activityCollection.Count());
            }
        }

        [TestMethod]
        public async Task NoScopeEnabled()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            CustomOtelExporter exporter = new CustomOtelExporter();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource()
                .Build())
            {
                client = TestCommon.CreateCosmosClient(
                    useGateway: false,
                    enableDistributingTracing: true);

                database = await client.CreateDatabaseAsync(
                        Guid.NewGuid().ToString(),
                        cancellationToken: default);

                Assert.AreEqual(0, CustomOtelExporter.CollectedActivities.Count());
            }
        }

        [TestMethod]
        public void NoScopeEnabled2()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            //CustomOtelExporter exporter = new CustomOtelExporter();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource()
                .Build())
            {
                Activity activity = new Activity("Test");
                activity.Start();
                Console.WriteLine("hello");
                activity.Stop();
                activity.Dispose();
                Assert.AreEqual(0, CustomOtelExporter.CollectedActivities.Count());
            }
        }
        [TestMethod]
        public async Task NewTest()
        {
            try

            {
                using CosmosClient client = new(

                accountEndpoint: "https://localhost:8081",

                authKeyOrResourceToken: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
                client.ClientOptions.IsDistributedTracingEnabled = true;

                // Database reference with creation if it does not already exist

                Database database = await client.CreateDatabaseIfNotExistsAsync(

                    id: "adventureworks"

                );
                Console.WriteLine(Activity.Current?.Id);
                // Container reference with creation if it does not alredy exist

                Container container = await database.CreateContainerIfNotExistsAsync(

                    id: "products",

                    partitionKeyPath: "/category",

                    throughput: 400

                );

                Container container2 = await database.CreateContainerIfNotExistsAsync(

                    id: "products2",

                    partitionKeyPath: "/category2",

                    throughput: 400

                );
                Console.WriteLine(Activity.Current?.Id);

                // Create new object and upsert (create or replace) to container

                Product newItem = new(

                    Id: "68719518391",

                    Category: "gear-surf-surfboards",

                    Name: "Yamba Surfboard"

                );
                Console.WriteLine(Activity.Current?.Id);
                ItemResponse<Product> createdItem = await container.UpsertItemAsync<Product>(

                    item: newItem,

                    partitionKey: new PartitionKey("gear-surf-surfboards")

                );

                Product readItem = await container.ReadItemAsync<Product>(

                   id: "68719518391",

                   partitionKey: new PartitionKey("gear-surf-surfboards")

               );

            }

            catch (CosmosException cosmosException)

            {
                Console.WriteLine("The current UI culture is {0}",

                                   Thread.CurrentThread.CurrentUICulture.Name);

                string a = cosmosException.Diagnostics.ToString();

                //Console.WriteLine($"Error log:\t{cosmosException.ToString()}");

            }

            catch (Exception ex)

            {

                Console.WriteLine("Error Custom {0}", ex.Message.ToString());

            }

        }

        [TestCleanup]
        public async Task CleanUp()
        {
            if (database != null)
            {
                await database.DeleteStreamAsync();
            }

            client?.Dispose();
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);
            AzureCore.ActivityExtensions.ResetFeatureSwitch();
        }
        private static void AssertAndResetActivityInformation()
        {
            AssertActivity.AreEqualAcrossListeners();
            CustomOtelExporter.CollectedActivities = new();
        }

    }

    internal class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public Product() { }

        public Product(string Id, string Category, string Name)
        {
            this.Id = Id;
            this.Category = Category;
            this.Name = Name;
        }
    }
}