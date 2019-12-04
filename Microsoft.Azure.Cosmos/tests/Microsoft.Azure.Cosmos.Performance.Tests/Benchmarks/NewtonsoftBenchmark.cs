// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    public class NewtonsoftBenchmark
    {
        private readonly CosmosClient clientForTests;
        private readonly SqlQuerySpec sqlQuerySpec;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public NewtonsoftBenchmark()
        {
            this.sqlQuerySpec = new SqlQuerySpec(
                @"SELECT * FROM document d 
                    WHERE(d.id = @documentId)
                    AND(d.deviceId = @deviceId)",
                new SqlParameterCollection()
                {
                    new SqlParameter("@documentId", "deviceEXISTS"),
                    new SqlParameter("@deviceId", "devicetoken|5fe8caf682ba0e7d/deviceCREATE")
                });

            this.clientForTests = MockDocumentClient.CreateMockCosmosClient((builder) => builder.WithSerializerOptions(new CosmosSerializationOptions() { IgnoreNullValues = false, Indented = false, PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }));
        }

        /// <summary>
        /// Benchmark for CreateItemAsync.
        /// </summary>
        [Benchmark]
        public void CreateSqlQuerySpec()
        {
            using (Stream stream = this.clientForTests.ClientContext.SqlQuerySpecSerializer.ToStream<SqlQuerySpec>(this.sqlQuerySpec))
            {
                if (stream == null)
                {
                    throw new Exception();
                }
            }
        }
    }
}