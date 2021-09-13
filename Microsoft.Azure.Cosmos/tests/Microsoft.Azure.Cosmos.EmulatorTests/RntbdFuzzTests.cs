//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class RntbdFuzzTests
    {
        [TestMethod]
        public async Task SendFuzzedRequestTest()
        {
            //Environment.SetEnvironmentVariable("FuzzLogLocation", "C:\\FuzzTest1");
            Environment.SetEnvironmentVariable("SendFuzzedRequest", "true");
            await this.Fuzz(5);
            Environment.SetEnvironmentVariable("SendFuzzedRequest", null);
        }

        private async Task Fuzz(int iterationCount)
        {
            this.InitializeFuzzLogDirectory();
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(true);
            Cosmos.Database database = (await cosmosClient.CreateDatabaseAsync("FuzzDb_" + Guid.NewGuid().ToString())).Database;
            Container container = await database.CreateContainerAsync("FuzzContainer_" + Guid.NewGuid().ToString(), "/pk");
            await container.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity());


            CosmosClientOptions options = new CosmosClientOptions
            {
                RequestTimeout = new TimeSpan(0, 0, 1),
                ConnectionMode = ConnectionMode.Direct
            };
            await this.RunFuzzAction(
                1,
                "Create Document",
                async delegate {
                    using (CosmosClient client = TestCommon.CreateCosmosClient(options))
                    {
                        Container newContainer = client.GetContainer(database.Id, container.Id);
                        ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                        await newContainer.CreateItemAsync<ToDoActivity>(item);
                    }
                },
                iterationCount);

            await this.VerifyServiceAvailableAsync(container);
            await database.DeleteAsync();
            cosmosClient.Dispose();
        }

        private async Task RunFuzzAction(int testIndex, string testName, Func<Task> action, int numberOfIterations)
        {
            string fuzzLogFileLocation = Environment.GetEnvironmentVariable("FuzzLogLocation");
            bool createFuzzLogFile = !string.IsNullOrEmpty(fuzzLogFileLocation);
            string logFileNameStart = "fuzzOutput_" + testIndex + "_" + testName + "_";

            for (int i = 0; i < numberOfIterations; i++)
            {
                if (createFuzzLogFile && i % 1000 == 0)
                {
                    // create a new log file for each 1000 iterations
                    int logFileIndex = i / 1000;
                    using (StreamWriter outFile = new StreamWriter(fuzzLogFileLocation + "\\" + logFileNameStart + logFileIndex + ".txt", true))
                    {
                        outFile.WriteLine(testName);
                    }
                }

                try
                {
                    if (createFuzzLogFile)
                    {
                        this.LogFuzzInfomation(string.Format(CultureInfo.CurrentCulture, "Iteration {0}", i));
                    }

                    await action();
                }
                catch (Exception ex)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(ex.GetType().FullName);
                    sb.AppendLine(ex.Message);

                    if (createFuzzLogFile)
                    {
                        this.LogFuzzInfomation(sb.ToString());
                    }
                }
            }
        }

        private async Task VerifyServiceAvailableAsync(Container container)
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            try
            {
                ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync(item);
                Assert.IsNotNull(itemResponse.Resource);
            }
            catch (CosmosException ex)
            {
                Assert.Fail(ex.Diagnostics.ToString());
            }
        }

        private void LogFuzzInfomation(string s)
        {
            string fuzzLogLocation = Environment.GetEnvironmentVariable("FuzzLogLocation");
            DirectoryInfo di = new DirectoryInfo(fuzzLogLocation);
            FileInfo[] logFiles = di.GetFiles("fuzzOutput_*.txt");
            string logFileName = logFiles[logFiles.Length - 1].Name;

            using (StreamWriter outFile = new StreamWriter(fuzzLogLocation + "\\" + logFileName, true))
            {
                outFile.WriteLine(s);
            }
        }

        private void InitializeFuzzLogDirectory()
        {
            string fuzzLogLocation = Environment.GetEnvironmentVariable("FuzzLogLocation");
            if (Directory.Exists(fuzzLogLocation))
            {
                DirectoryInfo di = new DirectoryInfo(fuzzLogLocation);
                FileInfo[] logFiles = di.GetFiles("fuzzOutput_*.txt");
                foreach (FileInfo file in logFiles)
                {
                    File.Delete(fuzzLogLocation + "\\" + file.Name);
                }
            }
            else if (!string.IsNullOrEmpty(fuzzLogLocation))
            {
                Directory.CreateDirectory(fuzzLogLocation);
            }
        }
    }
}
