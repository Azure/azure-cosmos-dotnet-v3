//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LocalCacheSetTests
    {
        [TestMethod]
        public void SetStoresValueRetrievableByGetOrCreate()
        {
            LocalCache<string, string> cache = new LocalCache<string, string>();
            cache.TimeToLive = TimeSpan.FromMinutes(5);

            cache.Set("key1", "value1");

            // GetOrCreate should return the cached value without invoking the factory
            bool factoryInvoked = false;
            string result = cache.GetOrCreate("key1", () =>
            {
                factoryInvoked = true;
                return "fallback";
            });

            Assert.AreEqual("value1", result);
            Assert.IsFalse(factoryInvoked, "Factory should not be invoked when cache has value");
        }

        [TestMethod]
        public void SetOverwritesExistingValue()
        {
            LocalCache<string, string> cache = new LocalCache<string, string>();
            cache.TimeToLive = TimeSpan.FromMinutes(5);

            cache.Set("key1", "original");
            cache.Set("key1", "updated");

            string result = cache.GetOrCreate("key1", () => "fallback");
            Assert.AreEqual("updated", result);
        }

        [TestMethod]
        public void SetIsNoOpWhenTtlIsZero()
        {
            LocalCache<string, string> cache = new LocalCache<string, string>();
            cache.TimeToLive = TimeSpan.Zero;

            cache.Set("key1", "value1");

            // Since TTL is zero, cache is bypassed; factory should be called
            string result = cache.GetOrCreate("key1", () => "fallback");
            Assert.AreEqual("fallback", result);
        }

        [TestMethod]
        public void SetIsNoOpWhenTtlIsNegative()
        {
            LocalCache<string, string> cache = new LocalCache<string, string>();
            cache.TimeToLive = TimeSpan.FromSeconds(-1);

            cache.Set("key1", "value1");

            string result = cache.GetOrCreate("key1", () => "fallback");
            Assert.AreEqual("fallback", result);
        }

        [TestMethod]
        public void SetWorkWithContainsCheck()
        {
            LocalCache<string, string> cache = new LocalCache<string, string>();
            cache.TimeToLive = TimeSpan.FromMinutes(5);

            Assert.IsFalse(cache.Contains("key1"));

            cache.Set("key1", "value1");

            Assert.IsTrue(cache.Contains("key1"));
        }

        [TestMethod]
        public void SetWithTupleKeyMatchesCacheSemantics()
        {
            // This mirrors the PDEK cache key structure: Tuple<string, KeyEncryptionKey, string>
            LocalCache<Tuple<string, string>, string> cache = new LocalCache<Tuple<string, string>, string>();
            cache.TimeToLive = TimeSpan.FromMinutes(5);

            var key = Tuple.Create("name", "hexKey");
            cache.Set(key, "pdek-value");

            // Same logical key (value equality)
            var lookupKey = Tuple.Create("name", "hexKey");
            string result = cache.GetOrCreate(lookupKey, () => "fallback");
            Assert.AreEqual("pdek-value", result);
        }
    }
}
