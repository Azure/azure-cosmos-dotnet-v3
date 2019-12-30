//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class SystemTextJsonUtils
    {
        public static void AssertJsonElementEquals(
            object expected,
            object actual,
            string message = "")
        {
            if (actual !is JsonElement
                && actual! is JsonDocument)
            {
                Assert.AreEqual(expected, actual, message);
            }
            else
            {
                JsonElement jsonElement = (JsonElement)actual;
                if (expected is string)
                {
                    Assert.AreEqual(expected, jsonElement.GetString(), message);
                    return;
                }
                else if (expected is int)
                {
                    Assert.AreEqual(expected, jsonElement.GetInt32(), message);
                    return;
                }
                else if (expected is long)
                {
                    Assert.AreEqual(expected, jsonElement.GetInt64(), message);
                    return;
                }
                else if (expected is double)
                {
                    Assert.AreEqual(expected, jsonElement.GetDouble(), message);
                    return;
                }
                else if (expected is bool)
                {
                    Assert.AreEqual(expected, jsonElement.GetBoolean(), message);
                    return;
                }
                else if (expected == null)
                {
                    Assert.IsTrue(jsonElement.ValueKind == JsonValueKind.Null, message);
                    return;
                }

                throw new NotSupportedException("Cannot compare JsonElement to object " + expected.GetType().FullName);
            }
        }
    }
}
