//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Data.Cosmos;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    internal static class TestCommon
    {
        public const int MinimumOfferThroughputToCreateElasticCollectionInTests = 10100;
        public const int CollectionQuotaForDatabaseAccountForQuotaCheckTests = 2;
        public const int NumberOfPartitionsPerCollectionInLocalEmulatorTest = 5;
        public const int CollectionQuotaForDatabaseAccountInTests = 16;
        public const int CollectionPartitionQuotaForDatabaseAccountInTests = 100;
        public const int TimeinMSTakenByTheMxQuotaConfigUpdateToRefreshInTheBackEnd = 240000; // 240 seconds
        public const int Gen3MaxCollectionCount = 16;
        public const int Gen3MaxCollectionSizeInKB = 256 * 1024;
        public const int MaxCollectionSizeInKBWithRuntimeServiceBindingEnabled = 1024 * 1024;
        public const int ReplicationFactor = 3;
        public static readonly int TimeToWaitForOperationCommitInSec = 2;

        private static readonly int serverStalenessIntervalInSeconds;
        private static readonly int masterStalenessIntervalInSeconds;
        public static readonly CosmosSerializer Serializer = new CosmosJsonDotNetSerializer();

        static TestCommon()
        {
            TestCommon.serverStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["ServerStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
            TestCommon.masterStalenessIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["MasterStalenessIntervalInSeconds"], CultureInfo.InvariantCulture);
        }

        internal static (string endpoint, string authKey) GetAccountInfo()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            return (endpoint, authKey);
        }

        internal static CosmosClient CreateCosmosClient(CosmosClientOptions options = null)
        {
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            return new CosmosClient(endpoint, authKey, options);
        }
    }
}