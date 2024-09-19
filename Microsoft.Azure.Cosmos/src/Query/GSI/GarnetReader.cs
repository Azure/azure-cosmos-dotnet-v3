//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using StackExchange.Redis;

    internal class GarnetReader : IGarnetReader
    {
        private readonly ConnectionPool pool;
        public GarnetReader()
        {
            WriterConfig writerConfig = new WriterConfig
            {
                ConnectionCount = 16
            };

            this.pool = ConnectionPool.getConnectionPool(writerConfig);
        }

        public string HashGet(string key1, string key2)
        {
            return this.pool.GetDatabase().HashGet(key1, key2).ToString();
        }

        public async Task<List<string>> BatchHashGetAsync(List<Tuple<string, string>> secondaryIndexTermAndPartitionKeyRangeId)
        {
            Task<RedisValue>[] inParallel = new Task<RedisValue>[secondaryIndexTermAndPartitionKeyRangeId.Count];
            IDatabase db = this.pool.GetDatabase();
            int i = 0;

            IBatch batch = db.CreateBatch();
            secondaryIndexTermAndPartitionKeyRangeId.ForEach(x => inParallel[i++] = batch.HashGetAsync(
                    x.Item1,
                    x.Item2));

            batch.Execute();

            await Task.WhenAll(inParallel);

            List<string> result = new List<string>();

            foreach (Task<RedisValue> item in inParallel)
            {
                if (item.Result.HasValue)
                {
                    result.Add(item.Result.ToString());
                }
            }

            return result;
        }

        public async Task<string> GetStatsAsync()
        {
            IDatabase db = this.pool.GetDatabase();
            RedisResult result = await db.ExecuteAsync("memory", "stats");
            return result.ToString();
        }

        public async Task<List<string>> GetPartitionIdsWithSecondaryIndexTermAsync(string term)
        {
            List<Tuple<string, string>> list = new List<Tuple<string, string>>();
            for (int i = 0; i <= 16; i++)
            {
                list.Add(Tuple.Create(i.ToString(), term));
            }
            return await this.BatchHashGetAsync(list);
        }
    }
}
