//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests.SystemTextJson
{
    using System;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;

    [TestClass]
    public class TextJsonUnixDateTimeConverterTests
    {
        [TestMethod]
        public void DeserializationTest()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new TextJsonUnixDateTimeConverter());

            DateTime? dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
            string serialized = JsonSerializer.Serialize(dateTime, options);

            DateTime? deserialized = JsonSerializer.Deserialize<DateTime?>(serialized, options);

            Assert.AreEqual(dateTime.Value.Ticks, deserialized.Value.Ticks);
        }

        [TestMethod]
        public void DeserializationTestNull()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new TextJsonUnixDateTimeConverter());

            DateTime? dateTime = null;
            string serialized = JsonSerializer.Serialize(dateTime, options);

            DateTime? deserialized = JsonSerializer.Deserialize<DateTime?>(serialized, options);

            Assert.IsFalse(deserialized.HasValue);
        }

        [TestMethod]
        public void NewtonsoftJsonCompatibility()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new TextJsonUnixDateTimeConverter());

            Newtonsoft.Json.JsonSerializerSettings jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings();
            jsonSerializerSettings.Converters.Add(new UnixDateTimeConverter());

            DateTime? dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);

            // Match serialization value
            Assert.AreEqual(JsonSerializer.Serialize(dateTime, options),
                Newtonsoft.Json.JsonConvert.SerializeObject(dateTime, jsonSerializerSettings));

            // System.Text.Json -> Newtonsoft
            string serialized = JsonSerializer.Serialize(dateTime, options);

            DateTime? deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<DateTime?>(serialized, jsonSerializerSettings);

            Assert.AreEqual(dateTime.Value.Ticks, deserialized.Value.Ticks);

            // Newtonsoft -> System.Text.Json
            serialized = Newtonsoft.Json.JsonConvert.SerializeObject(dateTime, jsonSerializerSettings);

            deserialized = JsonSerializer.Deserialize<DateTime?>(serialized, options);

            Assert.AreEqual(dateTime.Value.Ticks, deserialized.Value.Ticks);
        }
    }
}
