//-----------------------------------------------------------------------
// <copyright file="LinqScalarFunctionBaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestCommon = Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestCommon;

    /// <summary>
    /// Contains test that cleans up databases left over during debugging of LINQ tests.
    /// This test does not run by default, but automates the process of deleting the databases left over during debugging session.
    /// </summary>
    [TestClass]
    public class LinqCleanupTests
    {
        //[Ignore]
        [TestMethod]
        public async Task CleanupLinqTestDatabases()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);
            Uri uri = client.ClientContext.Client.Endpoint;
            if (uri.ToString().StartsWith(@"https://localhost:") ||
                uri.ToString().StartsWith(@"https://127.0.0.1:"))
            {
                Debug.WriteLine($"Executing against local endpoint '{uri}', continuing.");
                FeedIterator<DatabaseProperties> feedIterator = client
                    .GetDatabaseQueryIterator<DatabaseProperties>(
                        queryDefinition: null,
                        continuationToken: null,
                        requestOptions: new QueryRequestOptions() { MaxItemCount = 2 });

                Regex linqTestDatabaseRegex = new Regex("^Linq.*Baseline(Tests)?-[0-9A-Fa-f]{32}$");
                List<string> databasesToDelete = new List<string>();
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<DatabaseProperties> databasePropertiesResponse = await feedIterator.ReadNextAsync();
                    foreach (DatabaseProperties database in databasePropertiesResponse)
                    {
                        if (linqTestDatabaseRegex.IsMatch(database.Id))
                        {
                            Debug.WriteLine($"Recognized database for deletion : '{database.Id}'");
                            databasesToDelete.Add(database.Id);
                        }
                        else
                        {
                            Debug.WriteLine($"Database not recognized for deletion : '{database.Id}'");
                        }
                    }
                }

                foreach (string databaseToDelete in databasesToDelete)
                {
                    Debug.WriteLine($"Deleting database '{databaseToDelete}'");
                    Database database = client.GetDatabase(databaseToDelete);
                    DatabaseResponse response = await database.DeleteAsync();
                }
            }
            else
            {
                Debug.WriteLine($"Executing against non-local endpoint '{uri}', aborting.");
            }
        }
    }
}
