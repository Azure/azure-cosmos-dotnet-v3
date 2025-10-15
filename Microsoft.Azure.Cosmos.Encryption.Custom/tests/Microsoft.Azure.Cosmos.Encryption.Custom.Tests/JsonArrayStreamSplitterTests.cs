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

            List<MemoryStream> results = new ();

            try
            {
                await foreach (MemoryStream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
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
                foreach (MemoryStream stream in results)
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

            List<MemoryStream> results = new ();

            try
            {
                await foreach (MemoryStream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
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
                foreach (MemoryStream stream in results)
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
            await foreach (MemoryStream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
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

            await foreach (MemoryStream documentStream in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
            {
                using MemoryStream ownedStream = documentStream;
                ownedStream.Position = 0;
                using JsonDocument doc = JsonDocument.Parse(ownedStream);
                documentIds.Add(doc.RootElement.GetProperty("id").GetString());
            }

            CollectionAssert.AreEqual(new[] { "docA", "docB" }, documentIds);
        }

        [TestMethod]
        public async Task SplitIntoSubstreamsAsync_WithBareArrayPayload_ShouldThrow()
        {
            string jsonArray = "[{\"id\":\"doc1\"}]";

            using MemoryStream inputStream = new (Encoding.UTF8.GetBytes(jsonArray));

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await foreach (MemoryStream _ in JsonArrayStreamSplitter.SplitIntoSubstreamsAsync(inputStream, CancellationToken.None))
                {
                }
            });

            StringAssert.Contains(ex.Message, "start with '{'");
        }
    }
}
