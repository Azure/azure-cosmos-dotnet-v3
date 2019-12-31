//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests.SystemTextJson
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TextJsonObjectToPrimitiveConverterTests
    {
        [TestMethod]
        public void DeserializationTest()
        {
            DateTime dateTime = DateTime.Now;
            Dictionary<string, object> original = new Dictionary<string, object>();
            original.Add("test", 1L);
            original.Add("test2", "2");
            original.Add("test3", null);
            original.Add("test4", 1.5);
            original.Add("test5", dateTime);
            string serialized = JsonSerializer.Serialize(original);
            Dictionary<string, object> deserialized = TextJsonObjectToPrimitiveConverter.DeserializeDictionary(serialized);
            Assert.AreEqual(original["test"], deserialized["test"]);
            Assert.AreEqual(original["test2"], deserialized["test2"]);
            Assert.AreEqual(original["test3"], deserialized["test3"]);
            Assert.AreEqual(original["test4"], deserialized["test4"]);
            Assert.AreEqual(((DateTime)original["test5"]).Ticks, ((DateTimeOffset)deserialized["test5"]).Ticks);
        }
    }
}
