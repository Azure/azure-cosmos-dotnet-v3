//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Numerics;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using JsonReader = Microsoft.Azure.Cosmos.Json.JsonReader;
    using JsonWriter = Microsoft.Azure.Cosmos.Json.JsonWriter;

    [TestClass]
    public class BinaryNumericWireTests : BaseCosmosClientHelper
    {
        private string containerId;

        [TestInitialize]
        public async Task InitializeAsync()
        {
            await this.TestInit();
            this.containerId = Guid.NewGuid().ToString();
            await this.database.CreateContainerAsync(this.containerId, "/pk", throughput: 400);
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            await this.TestCleanup();
        }

        [DataTestMethod]
        [DataRow("9007199254740993", false)]
        [DataRow("9007199254740993", true)]
        [DataRow("9223372036854775807", false)]
        [DataRow("9223372036854775807", true)]
        [DataRow("-9223372036854775808", false)]
        [DataRow("-9223372036854775808", true)]
        [DataRow("0.1", false)]
        [DataRow("0.1", true)]
        [DataRow("0.125", false)]
        [DataRow("0.125", true)]
        [DataRow("-1234.5678", false)]
        [DataRow("-1234.5678", true)]
        [DataRow("1e-6", false)]
        [DataRow("1e-6", true)]
        [Timeout(60000)]
        public async Task PointRoundTripPreservesNumberAndRequestedWireFormat(string number, bool binary)
        {
            string original = Environment.GetEnvironmentVariable(Cosmos.ConfigurationManager.BinaryEncodingEnabled);
            try
            {
                Environment.SetEnvironmentVariable(Cosmos.ConfigurationManager.BinaryEncodingEnabled, binary.ToString());
                CosmosClient client = TestCommon.CreateCosmosClient(new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });
                this.SetClient(client);
                Container container = client.GetContainer(this.database.Id, this.containerId);
                string id = Guid.NewGuid().ToString();
                string json = $"{{\"id\":\"{id}\",\"pk\":\"{id}\",\"value\":{number}}}";
                using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
                ItemRequestOptions options = new ItemRequestOptions { EnableBinaryResponseOnPointOperations = binary };
                using ResponseMessage created = await container.CreateItemStreamAsync(
                    input, new PartitionKey(id), options, this.cancellationToken);
                Assert.AreEqual(HttpStatusCode.Created, created.StatusCode, created.ErrorMessage);
                using ResponseMessage read = await container.ReadItemStreamAsync(id, new PartitionKey(id), options, this.cancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, read.StatusCode, read.ErrorMessage);
                using MemoryStream output = new MemoryStream();
                await read.Content.CopyToAsync(output, this.cancellationToken);
                byte[] wire = output.ToArray();
                Assert.IsTrue(wire.Length > 0);
                Assert.AreEqual(binary ? (byte)JsonSerializationFormat.Binary : (byte)'{', wire[0],
                    "The real service response must exercise the requested wire format.");
                IJsonReader reader = JsonReader.Create(wire);
                IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);
                reader.WriteAll(writer);
                using JsonDocument document = JsonDocument.Parse(writer.GetResult());
                Assert.AreEqual(id, document.RootElement.GetProperty("id").GetString());
                string actual = document.RootElement.GetProperty("value").GetRawText();
                (BigInteger expectedNumerator, BigInteger expectedDenominator) = Rational(number);
                (BigInteger actualNumerator, BigInteger actualDenominator) = Rational(actual);
                Assert.AreEqual(expectedNumerator * actualDenominator, actualNumerator * expectedDenominator,
                    $"Number {number} changed to {actual} after a {(binary ? "binary" : "text")} service roundtrip.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(Cosmos.ConfigurationManager.BinaryEncodingEnabled, original);
            }
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task FractionalServerIncrementPersistsExactValue()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });
            this.SetClient(client);
            Container container = client.GetContainer(this.database.Id, this.containerId);
            string id = Guid.NewGuid().ToString();
            await container.CreateItemAsync(new { id, pk = id, value = 1.5 }, new PartitionKey(id),
                cancellationToken: this.cancellationToken);
            ItemResponse<JObject> patched = await container.PatchItemAsync<JObject>(
                id, new PartitionKey(id), new[] { PatchOperation.Increment("/value", 1.0) },
                cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, patched.StatusCode);
            Assert.AreEqual(2.5, (double)patched.Resource["value"]);
            ItemResponse<JObject> read = await container.ReadItemAsync<JObject>(
                id, new PartitionKey(id), cancellationToken: this.cancellationToken);
            Assert.AreEqual(2.5, (double)read.Resource["value"]);
        }

        private static (BigInteger Numerator, BigInteger Denominator) Rational(string text)
        {
            string[] parts = text.ToLowerInvariant().Split('e');
            int exponent = parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
            string mantissa = parts[0];
            int decimalPoint = mantissa.IndexOf('.');
            int scale = decimalPoint < 0 ? 0 : mantissa.Length - decimalPoint - 1;
            BigInteger numerator = BigInteger.Parse(mantissa.Replace(".", string.Empty), CultureInfo.InvariantCulture);
            BigInteger denominator = BigInteger.Pow(10, scale);
            if (exponent > 0)
            {
                numerator *= BigInteger.Pow(10, exponent);
            }
            else if (exponent < 0)
            {
                denominator *= BigInteger.Pow(10, -exponent);
            }

            return (numerator, denominator);
        }
    }
}
