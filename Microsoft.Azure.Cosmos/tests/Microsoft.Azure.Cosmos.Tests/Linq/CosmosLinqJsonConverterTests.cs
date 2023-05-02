//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [TestClass]
    public class CosmosLinqJsonConverterTests
    {
        [TestMethod]
        public void DateTimeKindIsPreservedTest()
        {
            // Should work for UTC time
            DateTime utcDate = new DateTime(2022, 5, 26, 0, 0, 0, DateTimeKind.Utc);
            Expression<Func<TestDocument, bool>> expr = a => a.StartDate <= utcDate;
            string sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"StartDate\"] <= \"2022-05-26\")", sql);

            // Should work for local time
            DateTime localDate = new DateTime(2022, 5, 26, 0, 0, 0, DateTimeKind.Local);
            expr = a => a.StartDate <= localDate;
            sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"StartDate\"] <= \"2022-05-26\")", sql);
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

        [TestMethod]
        public void TestNewtonsoftExtensionDataQuery()
        {
            Expression<Func<DocumentWithExtensionData, bool>> expr = a => (string)a.NewtonsoftExtensionData["foo"] == "bar";
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("(a[\"foo\"] = \"bar\")", sql);
        }

        [TestMethod]
        public void TestSystemTextJsonExtensionDataQuery()
        {
            Expression<Func<DocumentWithExtensionData, bool>> expr = a => ((object)a.NetExtensionData["foo"]) == "bar";
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            // TODO: This is a limitation in the translator. It should be able to handle STJ extension data, if a custom
            // JSON serializer is specified.
            Assert.AreEqual("(a[\"NetExtensionData\"][\"foo\"] = \"bar\")", sql);
        }

        class DocumentWithExtensionData
        {
            [Newtonsoft.Json.JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> NewtonsoftExtensionData { get; set; }

            [System.Text.Json.Serialization.JsonExtensionData()]
            public Dictionary<string, System.Text.Json.JsonElement> NetExtensionData { get; set; }
        }
    }
}
