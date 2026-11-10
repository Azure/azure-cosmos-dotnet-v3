//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Rntbd;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartialPartitionKeyTests
    {
        private const string Prefix = "[\"tenant\",42]";
        private const ushort TokenIdentifier = 0x0108;

        [TestMethod]
        public void KnownHeaderSupportsStorageCopyAndRemoval()
        {
            const string header = "x-ms-documentdb-partialpartitionkey";
            Assert.AreEqual(header, HttpConstants.HttpHeaders.PartialPartitionKey);
            RequestNameValueCollection headers = new RequestNameValueCollection();
            headers.Add(header.ToUpperInvariant(), Prefix);
            Assert.AreEqual(Prefix, headers.PartialPartitionKey);
            Assert.AreEqual(Prefix, headers[header]);
            CollectionAssert.AreEqual(new[] { Prefix }, headers.GetValues(header));
            CollectionAssert.AreEqual(new[] { header }, headers.Keys().ToArray());
            Assert.ThrowsException<ArgumentException>(() => headers.Add(header, Prefix));
            Assert.ThrowsException<ArgumentException>(() => headers.Add(header.ToUpperInvariant(), Prefix));

            RequestNameValueCollection[] copies =
            {
                (RequestNameValueCollection)headers.Clone(),
                new RequestNameValueCollection((INameValueCollection)headers),
                new RequestNameValueCollection(new Dictionary<string, string> { [header] = Prefix }),
                RequestNameValueCollection.BuildRequestNameValueCollectionWithKnownHeadersOnly(
                    (INameValueCollection)headers),
                RequestNameValueCollection.BuildRequestNameValueCollectionWithKnownHeadersOnly(
                    new Dictionary<string, string> { [header.ToUpperInvariant()] = Prefix, ["unknown"] = "ignored" }),
            };

            foreach (RequestNameValueCollection copy in copies)
            {
                Assert.AreEqual(Prefix, copy.PartialPartitionKey);
                Assert.AreEqual(1, copy.Count());
                Assert.AreEqual(Prefix, copy.ToNameValueCollection()[header]);
                copy.Clear();
                Assert.IsNull(copy.PartialPartitionKey);
                Assert.AreEqual(0, copy.Count());
            }

            headers.Set(header.ToUpperInvariant(), "[\"other\"]");
            Assert.AreEqual("[\"other\"]", headers.PartialPartitionKey);
            headers.Remove(header.ToUpperInvariant());
            Assert.IsNull(headers.PartialPartitionKey);
            Assert.IsNull(headers.Get(header));
            Assert.AreEqual(0, headers.Count());
        }

        [TestMethod]
        public void KnownHeaderSurvivesEnumerationFallback()
        {
            INameValueCollection headers = new InterruptedHeaders { PartialPartitionKey = Prefix };
            RequestNameValueCollection copy =
                RequestNameValueCollection.BuildRequestNameValueCollectionWithKnownHeadersOnly(headers);
            Assert.AreEqual(Prefix, copy.PartialPartitionKey);
        }

        [DataTestMethod]
        [DataRow("[\"tenant\"]", false)]
        [DataRow(Prefix, true)]
        [DataRow("[\"t\u00e9nant\",null,true]", false)]
        [DataRow(null, false)]
        [DataRow(null, true)]
        [DataRow("", true)]
        public void OutboundRntbdPreservesPrefixAndOmitsAbsentHeader(string prefix, bool useKnownHeaders)
        {
            using DocumentServiceRequest request = CreateRequest(prefix, useKnownHeaders);
            byte[] buffer = Serialize(request);
            RntbdConstants.Request parsed = Parse(buffer);
            Assert.AreEqual(TokenIdentifier, parsed.partialPartitionKey.GetTokenIdentifier());
            Assert.AreEqual(RntbdTokenTypes.String, parsed.partialPartitionKey.GetTokenType());
            Assert.IsFalse(parsed.partialPartitionKey.IsRequired());
            Assert.AreEqual(!string.IsNullOrEmpty(prefix), parsed.partialPartitionKey.isPresent);
            if (!string.IsNullOrEmpty(prefix))
            {
                Assert.AreEqual(prefix, Encoding.UTF8.GetString(parsed.partialPartitionKey.value.valueBytes.ToArray()));
            }

            Assert.IsFalse(parsed.partitionKey.isPresent, "A prefix must not become a full logical partition key.");
            Assert.AreEqual("0", Encoding.UTF8.GetString(parsed.partitionKeyRangeId.value.valueBytes.ToArray()));
            Assert.IsTrue(parsed.payloadPresent.isPresent);
            Assert.IsTrue(parsed.replicaPath.isPresent);
        }

        [TestMethod]
        public void RequestSchemaKeepsIdentifiersAlignedAndUnique()
        {
            RntbdConstants.Request schema = new RntbdConstants.Request();
            ushort[] identifiers = Enum.GetValues(typeof(RntbdConstants.RequestIdentifiers)).Cast<ushort>().ToArray();
            Assert.AreEqual(identifiers.Length, identifiers.Distinct().Count());
            foreach (ushort identifier in identifiers)
            {
                Assert.IsNotNull(schema.tokens[identifier]);
                Assert.AreEqual(identifier, schema.tokens[identifier].GetTokenIdentifier());
            }

            Assert.AreSame(schema.partialPartitionKey, schema.tokens[TokenIdentifier]);
            Assert.AreEqual((ushort)0x0101, schema.supportedSerializationFeatures.GetTokenIdentifier());
            Assert.AreEqual(RntbdTokenTypes.UShort, schema.supportedSerializationFeatures.GetTokenType());
            for (int identifier = 0x0102; identifier < TokenIdentifier; identifier++)
            {
                Assert.IsNull(schema.tokens[identifier], "Unported upstream identifiers must remain unassigned.");
            }
        }

        [TestMethod]
        public void OlderSchemaSkipsUnknownPrefixAndReadsFollowingToken()
        {
            using DocumentServiceRequest request = CreateRequest(Prefix, useKnownHeaders: false);
            byte[] serialized = Serialize(request);

            // Model a receiver that knows a later token but not PartialPartitionKey.
            // This does not model old Proxy builds that assigned 0x0108 to a Byte.
            const ushort laterIdentifier = 0x010A;
            byte[] buffer = serialized.Concat(new byte[] { 0x0A, 0x01, (byte)RntbdTokenTypes.Byte, 1 }).ToArray();
            RntbdConstants.Request olderSchema = new RntbdConstants.Request();
            Array.Resize(ref olderSchema.tokens, laterIdentifier + 1);
            olderSchema.tokens[TokenIdentifier] = null;
            RntbdToken laterToken = new RntbdToken(false, RntbdTokenTypes.Byte, laterIdentifier);
            olderSchema.tokens[laterIdentifier] = laterToken;
            BytesDeserializer reader = new BytesDeserializer(buffer, buffer.Length);
            reader.ReadBytes(24);
            olderSchema.ParseFrom(ref reader);
            Assert.IsFalse(olderSchema.partialPartitionKey.isPresent);
            Assert.IsTrue(laterToken.isPresent);
            Assert.AreEqual((byte)1, laterToken.value.valueByte);
            Assert.AreEqual(buffer.Length, reader.Position);

            // A schema whose table ends before 0x0108 must also skip the unknown token.
            RntbdConstants.Request shorterSchema = new RntbdConstants.Request();
            Array.Resize(ref shorterSchema.tokens, 0x0102);
            reader = new BytesDeserializer(serialized, serialized.Length);
            reader.ReadBytes(24);
            shorterSchema.ParseFrom(ref reader);
            Assert.IsFalse(shorterSchema.partialPartitionKey.isPresent);
            Assert.IsTrue(shorterSchema.partitionKeyRangeId.isPresent);
            Assert.AreEqual(serialized.Length, reader.Position);
        }

        [TestMethod]
        public void ResetClearsPrefixBeforeRequestReuse()
        {
            using DocumentServiceRequest request = CreateRequest(Prefix, useKnownHeaders: true);
            RntbdConstants.Request parsed = Parse(Serialize(request));
            Assert.IsTrue(parsed.partialPartitionKey.isPresent);
            parsed.Reset();
            Assert.IsFalse(parsed.partialPartitionKey.isPresent);
            Assert.AreEqual(0, parsed.partialPartitionKey.value.valueBytes.Length);

            request.Headers.Remove(HttpConstants.HttpHeaders.PartialPartitionKey);
            Assert.IsFalse(Parse(Serialize(request)).partialPartitionKey.isPresent);
        }

        [DataTestMethod]
        [DataRow(Prefix)]
        [DataRow(null)]
        public async Task DirectHttpsForwardsOnlySuppliedPrefix(string prefix)
        {
            using DocumentServiceRequest request = CreateRequest(prefix, useKnownHeaders: true);
            await request.EnsureBufferedBodyAsync();
            using HttpTransportClient client = new HttpTransportClient(10, eventSource: null);
            MethodInfo prepare = typeof(HttpTransportClient).GetMethod(
                "PrepareHttpMessage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(prepare);
            using HttpRequestMessage message = (HttpRequestMessage)prepare.Invoke(
                client,
                new object[]
                {
                    Guid.NewGuid(),
                    new Uri("https://localhost/"),
                    new ResourceOperation(OperationType.Query, ResourceType.Document),
                    request,
                });
            bool present = message.Headers.TryGetValues(HttpConstants.HttpHeaders.PartialPartitionKey, out IEnumerable<string> values);
            Assert.AreEqual(prefix != null, present);
            if (present)
            {
                Assert.AreEqual(prefix, values.Single());
            }
        }

        private static DocumentServiceRequest CreateRequest(string prefix, bool useKnownHeaders)
        {
            INameValueCollection headers = useKnownHeaders
                ? new RequestNameValueCollection()
                : new DictionaryNameValueCollection();
            if (prefix != null)
            {
                headers.Add(HttpConstants.HttpHeaders.PartialPartitionKey, prefix);
            }

            headers.Add(HttpConstants.HttpHeaders.PartitionKeyRangeId, "0");
            return new DocumentServiceRequest(
                OperationType.Query,
                ResourceType.Document,
                "dbs/wo1ZAA==/colls/wo1ZAP7zFQA=/",
                new MemoryStream(Encoding.UTF8.GetBytes("{\"query\":\"SELECT * FROM c\"}")),
                AuthorizationTokenType.PrimaryMasterKey,
                headers);
        }

        private static byte[] Serialize(DocumentServiceRequest request)
        {
            using TransportSerialization.SerializedRequest serialized = TransportSerialization.BuildRequest(
                request,
                "DummyReplicaPath",
                new ResourceOperation(OperationType.Query, ResourceType.Document),
                Guid.NewGuid(),
                new BufferProvider(),
                "1",
                out int headerSize,
                out _);
            byte[] buffer = new byte[serialized.RequestSize];
            serialized.CopyTo(new ArraySegment<byte>(buffer));
            return buffer.Take(headerSize).ToArray();
        }

        private static RntbdConstants.Request Parse(byte[] buffer)
        {
            BytesDeserializer reader = new BytesDeserializer(buffer, buffer.Length);
            reader.ReadBytes(24);
            RntbdConstants.Request parsed = new RntbdConstants.Request();
            parsed.ParseFrom(ref reader);
            return parsed;
        }

        private sealed class InterruptedHeaders : RequestNameValueCollection, IEnumerable
        {
            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new InvalidOperationException("Simulated concurrent header modification.");
            }
        }
    }
}
