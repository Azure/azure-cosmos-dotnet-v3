//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using SqlObjects;

    [TestClass]
    public class CosmosLinqJsonConverterTests
    {
        [TestMethod]
        public void DateTimeKindIsPreservedTest()
        {
            // NOTE: This test does not actually use the emulator, just a convenient way to get access
            // to a container to create a Linq queryable
            const string account = "https://localhost:8081";
            const string key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            using CosmosClient client = new CosmosClient(account, key);
            Container container = client.GetContainer("NotEven", "MakingARequest");

            // Should work for UTC time
            DateTime utcDate = new DateTime(2022, 5, 26, 0, 0, 0, DateTimeKind.Utc);
            IQueryable<TestDocument> query = container.GetItemLinqQueryable<TestDocument>().Where(a => a.StartDate <= utcDate);
            Assert.IsTrue(query.ToString().Contains("2022-05-26"));

            // Should work for local time
            DateTime localDate = new DateTime(2022, 5, 26, 0, 0, 0, DateTimeKind.Local);
            query = container.GetItemLinqQueryable<TestDocument>().Where(a => a.StartDate <= localDate);
            Assert.IsTrue(query.ToString().Contains("2022-05-26"));
        }

        class TestDocument
        {
            [JsonConverter(typeof(DateJsonConverter))]
            public DateTime StartDate { get; set; }
        }

        class DateJsonConverter : IsoDateTimeConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is DateTime dateTime)
                {
                    writer.WriteValue(dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
                else
                {
                    base.WriteJson(writer, value, serializer);
                }
            }
        }
    }
}
