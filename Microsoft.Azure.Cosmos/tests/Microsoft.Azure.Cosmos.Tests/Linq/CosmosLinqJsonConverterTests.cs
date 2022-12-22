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

        [TestMethod]
        public void DecimalIsPreservedTest()
        {
            Dictionary<decimal, string> decimalTranslationDictionary = new Dictionary<decimal, string>()
            {
                { decimal.MaxValue,  "79228162514264337593543950335" },
                { decimal.MinValue,  "-79228162514264337593543950335" },
                { 123m, "123" },
                { 104.37644171779141m, "104.37644171779141" },
                { 0.00000000000000000012m, "0.00000000000000000012" },
                { -0.00000000000000000012m, "-0.00000000000000000012" },
            };

            foreach(decimal key in decimalTranslationDictionary.Keys)
            {
                Expression<Func<TestDocument, bool>> expr = a => a.DecimalValue == key;

                string sql = SqlTranslator.TranslateExpression(expr.Body);
                Assert.AreEqual($"(a[\"DecimalValue\"] = {decimalTranslationDictionary[key]})", sql);
            }
        }

        class TestDocument
        {
            [JsonConverter(typeof(DateJsonConverter))]
            public DateTime StartDate { get; set; }

            public decimal DecimalValue { get; set; }
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
