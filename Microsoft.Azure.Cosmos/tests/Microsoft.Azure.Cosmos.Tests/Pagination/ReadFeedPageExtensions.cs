//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Documents;

    internal static class ReadFeedPageExtensions
    {
        public static IReadOnlyList<Record> GetRecords(this ReadFeedPage page)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                page.Content.CopyTo(memoryStream);
                CosmosObject responseEnvolope = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                if (!responseEnvolope.TryGetValue("Documents", out CosmosArray documents))
                {
                    throw new InvalidOperationException();
                }

                List<Record> records = new List<Record>();
                foreach (CosmosElement document in documents)
                {
                    CosmosObject documentObject = (CosmosObject)document;
                    ResourceId rid = ResourceId.Parse(((CosmosString)documentObject["_rid"]).Value);
                    long ticks = Number64.ToLong(((CosmosNumber)documentObject["_ts"]).Value);
                    string id = ((CosmosString)documentObject["id"]).Value;
                    CosmosObject payload = documentObject;

                    Record record = new Record(rid, new DateTime(ticks: ticks, DateTimeKind.Utc), id, payload);
                    records.Add(record);
                }

                return records;
            }
        }
    }
}