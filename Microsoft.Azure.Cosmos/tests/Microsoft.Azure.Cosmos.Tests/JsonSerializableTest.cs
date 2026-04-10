//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Tests <see cref="JsonSerializable"/> class.
    /// </summary>
    [TestClass]
    public class JsonSerializableTest
    {
        [JsonConverter(typeof(StringEnumConverter))]
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

        [JsonConverter(typeof(StringEnumConverter))]
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
                serializable = new SampleSerializable();
                serializable.LoadFrom(new JsonTextReader(new StringReader(json)));
                Assert.AreEqual(SampleEnum.Splitting, serializable.Field1);
                Assert.AreEqual(SampleEnum2.Splitting, serializable.Field2);
                Assert.AreEqual(SampleEnum3.Splitting, serializable.Field3);

                serializable = new SampleSerializable();
                serializable.LoadFrom(new JsonTextReader(new StringReader("{\"field1\":\"splitting\",\"field2\":\"Splitting\",\"field3\":\"Splitting\"}")));
                Assert.AreEqual(SampleEnum.Splitting, serializable.Field1);
                Assert.AreEqual(SampleEnum2.Splitting, serializable.Field2);
                Assert.AreEqual(SampleEnum3.Splitting, serializable.Field3);
            }
        }
    }
}