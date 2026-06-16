//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonArrayStreamSplitterTests
    {
        [DataTestMethod]
        [DataRow("{\"Documents\":[{\"id\":\"doc1\"")]  // truncated mid-object
        [DataRow("{\"Documents\":[{\"id\":\"doc1\"},{\"id\":\"doc")]  // truncated mid-second-object
        [DataRow("{\"Documents\":[{")]  // truncated immediately after open brace
        [DataRow("{\"Documents\":[")]  // truncated immediately after array open
        public async Task SplitIntoSubstreamsAsync_TruncatedInput_ThrowsCleanJsonExceptionWithoutBufferGrowth(string payload)
        {
            // Contract: truncated/malformed feed envelopes must surface as a clean JsonException, not as a
            // misleading "maximum buffer size" error after wasteful buffer doublings. The strict final-block
            // Utf8JsonReader throws JsonReaderException (a JsonException) the moment the stream is exhausted;
            // this test pins that so a future change can't let truncated input degrade into the growth path.
            // We assert "is JsonException" (base-or-derived) since JsonReaderException is an STJ detail.
            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(payload));

            Exception caught = null;
            try
            {
                await foreach (Stream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
                {
                    documentStream.Dispose();
                }
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught, "Truncated input must throw.");
            Assert.IsInstanceOfType(caught, typeof(JsonException), $"Expected a JsonException (or subclass). Actual: {caught.GetType().Name}: {caught.Message}");
            Assert.IsFalse(
                caught.Message.Contains("maximum buffer size", StringComparison.OrdinalIgnoreCase),
                $"Truncated input must not degrade to a buffer-size error. Actual: {caught.Message}");
        }

        [TestMethod]
        public async Task SplitIntoSubstreamsAsync_WithLargeDocument_ShouldHandleBufferBoundary()
        {
            const int largeValueSize = 2048 * 1024;
            string largeEncryptedValue = new ('B', largeValueSize);
            string jsonArray = $@"{{
    ""Documents"": [
        {{
            ""id"": ""doc2"",
            ""encryptedData"": ""{largeEncryptedValue}""
        }}
    ],
    ""_count"": 1
}}";

            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonArray);
            using MemoryStream inputStream = new (jsonBytes);

            List<Stream> results = new ();

            try
            {
                await foreach (Stream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
                {
                    Assert.IsNotNull(documentStream);
                    results.Add(documentStream);
                }

                Assert.AreEqual(1, results.Count);

                results[0].Position = 0;
                using JsonDocument doc = JsonDocument.Parse(results[0]);
                JsonElement root = doc.RootElement;

                Assert.AreEqual("doc2", root.GetProperty("id").GetString());
                Assert.AreEqual(largeEncryptedValue, root.GetProperty("encryptedData").GetString());
            }
            finally
            {
                foreach (Stream stream in results)
                {
                    stream?.Dispose();
                }
            }
        }

        [TestMethod]
        public async Task SplitIntoSubstreamsAsync_WithMultipleLargeDocuments_ShouldSplitCorrectly()
        {
            const int documentCount = 10;
            const int documentSize = 100 * 1024;

            StringBuilder jsonBuilder = new ();
            jsonBuilder.AppendLine("{");
            jsonBuilder.AppendLine("  \"Documents\": [");

            for (int i = 0; i < documentCount; i++)
            {
                if (i > 0)
                {
                    jsonBuilder.Append(',');
                }

                string largeValue = new ((char)('A' + (i % 26)), documentSize);
                jsonBuilder.Append($@"
  {{
    ""id"": ""doc{i}"",
    ""index"": {i},
    ""encryptedData"": ""{largeValue}""
  }}");
            }

            jsonBuilder.AppendLine();
            jsonBuilder.AppendLine("  ],");
            jsonBuilder.AppendLine("  \"_count\": 10");
            jsonBuilder.Append('}');

            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonBuilder.ToString());
            using MemoryStream inputStream = new MemoryStream(jsonBytes);

            List<Stream> results = new ();

            try
            {
                await foreach (Stream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
                {
                    Assert.IsNotNull(documentStream);
                    results.Add(documentStream);
                }

                Assert.AreEqual(documentCount, results.Count);

                for (int i = 0; i < documentCount; i++)
                {
                    results[i].Position = 0;
                    using JsonDocument doc = JsonDocument.Parse(results[i]);
                    JsonElement root = doc.RootElement;

                    Assert.AreEqual($"doc{i}", root.GetProperty("id").GetString());
                    Assert.AreEqual(i, root.GetProperty("index").GetInt32());
                }
            }
            finally
            {
                foreach (Stream stream in results)
                {
                    stream?.Dispose();
                }
            }
        }

        [TestMethod]
        public async Task SplitIntoSubstreamsAsync_WithEmptyDocumentsArray_ShouldYieldNoResults()
        {
            string jsonPayload = "{\"Documents\":[],\"_count\":0}";

            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(jsonPayload));

            int documentCount = 0;
            await foreach (Stream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
            {
                documentCount++;
                documentStream.Dispose();
            }

            Assert.AreEqual(0, documentCount, "Empty documents array should produce no document streams.");
        }

        [TestMethod]
        public async Task SplitIntoSubstreamsAsync_WhenPayloadContainsUnrelatedArrays_ShouldOnlyReturnDocuments()
        {
            string jsonPayload = @"{
  ""Metadata"": [{ ""info"": ""value"" }],
  ""Documents"": [{ ""id"": ""docA"" }, { ""id"": ""docB"" }],
  ""Diagnostics"": [1, 2, 3],
  ""_count"": 2
}";

            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(jsonPayload));
            List<string> documentIds = new ();

            await foreach (Stream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
            {
                using Stream ownedStream = documentStream;
                ownedStream.Position = 0;
                using JsonDocument doc = JsonDocument.Parse(ownedStream);
                documentIds.Add(doc.RootElement.GetProperty("id").GetString());
            }

            CollectionAssert.AreEqual(new[] { "docA", "docB" }, documentIds);
        }

        [DataTestMethod]
        [DataRow("[{\"id\":\"doc1\"}]")]
        [DataRow("42")]
        [DataRow("\"hi\"")]
        [DataRow("null")]
        [DataRow("true")]
        [DataRow("false")]
        public async Task SplitIntoSubstreamsAsync_WithMalformedRootPayload_ShouldThrow(string payload)
        {
            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(payload));

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await foreach (Stream _ in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
                {
                }
            });

            StringAssert.Contains(ex.Message, "start with '{'");
        }

        [DataTestMethod]
        [DataRow("{}", "feed response")]
        [DataRow("{\"_count\":0}", "feed response")]
        [DataRow("{\"Documents\":null}", "must be a JSON array")]
        [DataRow("{\"Documents\":42}", "must be a JSON array")]
        [DataRow("{\"Documents\":\"x\"}", "must be a JSON array")]
        public async Task SplitIntoSubstreamsAsync_WithMalformedFeedStructure_ShouldThrow(string payload, string expectedMessageFragment)
        {
            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(payload));

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await foreach (Stream _ in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
                {
                }
            });

            StringAssert.Contains(ex.Message, expectedMessageFragment);
        }

        [DataTestMethod]
        [DataRow("{\"Documents\":[42]}")]
        [DataRow("{\"Documents\":[null]}")]
        [DataRow("{\"Documents\":[\"x\"]}")]
        public async Task SplitIntoSubstreamsAsync_DocumentsContainsOnlyNonObjects_YieldsNothing(string payload)
        {
            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(payload));
            int yielded = 0;

            await foreach (Stream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
            {
                documentStream.Dispose();
                yielded++;
            }

            Assert.AreEqual(0, yielded);
        }

        [DataTestMethod]
        [DataRow("{\"Documents\":[],\"Documents\":[{\"id\":\"y\"}]}", "y")]
        [DataRow("{\"Documents\":[],\"Documents\":[],\"Documents\":[{\"id\":\"z\"}]}", "z")]
        [DataRow("{\"Documents\":[{\"id\":\"x\"}],\"Documents\":[{\"id\":\"y\"}]}", "x,y")]
        // The Cosmos gateway will never emit duplicate root-property keys, so this behavior is
        // intentionally permissive (yield from every Documents array seen) rather than strict
        // (reject or last-wins). Tracking which Documents key "won" would require a second pass
        // over the envelope; instead, the splitter treats every occurrence as content. If a future
        // maintainer "fixes" this to last-wins to match strict JSON rules, drop this test row first
        // — the rest of the splitter assumes any object inside a Documents array is a document.
        public async Task SplitIntoSubstreamsAsync_DuplicateDocumentsProperty_YieldsFromAllDocumentsArrays(string payload, string expectedIdsCsv)
        {
            await AssertYieldedIdsAsync(payload, expectedIdsCsv);
        }

        [DataTestMethod]
        [DataRow("{\"Documents\":[42,{\"id\":\"a\"},\"str\",{\"id\":\"b\"},null]}", "a,b")]
        [DataRow("{\"Documents\":[[1,2,3],{\"id\":\"a\"}]}", "a")]
        public async Task SplitIntoSubstreamsAsync_DocumentsContainsMixedContents_YieldsOnlyObjects(string payload, string expectedIdsCsv)
        {
            await AssertYieldedIdsAsync(payload, expectedIdsCsv);
        }

        private static async Task AssertYieldedIdsAsync(string payload, string expectedIdsCsv)
        {
            string[] expectedIds = expectedIdsCsv.Split(',');
            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(payload));
            List<string> actualIds = new ();

            await foreach (Stream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
            {
                using Stream owned = documentStream;
                owned.Position = 0;
                using JsonDocument doc = JsonDocument.Parse(owned);
                actualIds.Add(doc.RootElement.GetProperty("id").GetString());
            }

            CollectionAssert.AreEqual(expectedIds, actualIds);
        }
    }
}
