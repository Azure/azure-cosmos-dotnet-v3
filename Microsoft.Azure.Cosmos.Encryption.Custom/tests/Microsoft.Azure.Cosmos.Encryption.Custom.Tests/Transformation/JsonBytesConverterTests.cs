#if NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.SystemTextJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonBytesConverterTests
    {
        [TestMethod]
        public void Write_Results_IdenticalToNewtonsoft()
        {
            byte[] bytes = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

            JsonBytes jsonBytes = new (bytes, 5, 5);

            using MemoryStream ms = new ();
            using Utf8JsonWriter writer = new (ms);

            JsonBytesConverter jsonConverter = new ();
            jsonConverter.Write(writer, jsonBytes, JsonSerializerOptions.Default);
            
            writer.Flush();
            ms.Flush();
            ms.Position = 0;
            StreamReader sr = new(ms);
            string systemTextResult = sr.ReadToEnd();

            byte[] newtonsoftBytes = bytes.AsSpan(5, 5).ToArray();
            string newtonsoftResult = Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftBytes);

            Assert.AreEqual(systemTextResult, newtonsoftResult);
        }
    }
}
#endif