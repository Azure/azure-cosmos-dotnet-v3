//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="JsonSerializable"/> class.
    /// </summary>
    [TestClass]
    public class JsonSerializableTest
    {
        [JsonConverter(typeof(JsonStringEnumConverter<SampleEnum>))]
        internal enum SampleEnum
        {
            Invalid,

            [EnumMember(Value = "online")]
            Online,

            [EnumMember(Value = "splitting")]
            Splitting,

            [EnumMember(Value = "offline")]
            Offline
        }

        [JsonConverter(typeof(JsonStringEnumConverter<SampleEnum2>))]
        internal enum SampleEnum2
        {
            Invalid,
            Online,
            Splitting,
            Offline
        }

        internal enum SampleEnum3
        {
            Invalid,
            Online,
            Splitting,
            Offline
        }

        private class SampleSerializable : JsonSerializable
        {
            public SampleEnum Field1
            {
                get => base.GetValue<SampleEnum>("field1");
                set => base.SetValue("field1", value);
            }

            public SampleEnum2 Field2
            {
                get => base.GetValue<SampleEnum2>("field2");
                set => base.SetValue("field2", value);
            }

            public SampleEnum3 Field3
            {
                get => base.GetValue<SampleEnum3>("field3");
                set => base.SetValue("field3", value);
            }
        }

        /// <summary>
        /// Tests serialization deserialization of enums with EnumMember attribute.
        /// </summary>
        [TestMethod]
        public void TestEnumSerializeDeserialize()
        {
            SampleSerializable serializable = new SampleSerializable
            {
                Field1 = SampleEnum.Splitting,
                Field2 = SampleEnum2.Splitting,
                Field3 = SampleEnum3.Splitting
            };

            using (MemoryStream ms = new MemoryStream())
            {
                serializable.SaveTo(ms);

                ms.Position = 0;

                string json = new StreamReader(ms).ReadToEnd();

                Assert.AreEqual("{\"field1\":\"splitting\",\"field2\":\"Splitting\",\"field3\":2}", json);

                // Deserialize using System.Text.Json
                ms.Position = 0;
                SampleSerializable deserialized = JsonSerializable.LoadFrom<SampleSerializable>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)), null);

                Assert.AreEqual(SampleEnum.Splitting, deserialized.Field1);
                Assert.AreEqual(SampleEnum2.Splitting, deserialized.Field2);
                Assert.AreEqual(SampleEnum3.Splitting, deserialized.Field3);

                // Test with string value for field3
                string jsonWithStringField3 = "{\"field1\":\"splitting\",\"field2\":\"Splitting\",\"field3\":\"Splitting\"}";
                SampleSerializable deserialized2 = JsonSerializable.LoadFrom<SampleSerializable>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonWithStringField3)), null);

                Assert.AreEqual(SampleEnum.Splitting, deserialized2.Field1);
                Assert.AreEqual(SampleEnum2.Splitting, deserialized2.Field2);
                Assert.AreEqual(SampleEnum3.Splitting, deserialized2.Field3);
            }
        }
    }
}