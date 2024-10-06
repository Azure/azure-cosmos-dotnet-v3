//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    internal static class TestCommon
    {
        internal static byte[] GenerateRandomByteArray()
        {
            Random random = new();
            byte[] b = new byte[10];
            random.NextBytes(b);
            return b;
        }

        internal static byte[] EncryptData(byte[] plainText)
        {
            return plainText.Select(b => (byte)(b + 1)).ToArray();
        }

        internal static int EncryptData(byte[] plainText, int inputOffset, int inputLength, byte[] output, int outputOffset)
        {
            byte[] cipherText = EncryptData(plainText.AsSpan(inputOffset, inputLength).ToArray());
            Buffer.BlockCopy(cipherText, 0, output, outputOffset, cipherText.Length);

            return cipherText.Length;
        }

        internal static byte[] DecryptData(byte[] cipherText)
        {
            return cipherText.Select(b => (byte)(b - 1)).ToArray();
        }

        internal static int DecryptData(byte[] cipherText, int inputOffset, int inputLength, byte[] output, int outputOffset)
        {
            byte[] plainText = DecryptData(cipherText.AsSpan(inputOffset, inputLength).ToArray());
            Buffer.BlockCopy(plainText, 0, output, outputOffset, plainText.Length);
            return plainText.Length;
        }

        internal static Stream ToStream<T>(T input)
        {
            string s = JsonConvert.SerializeObject(input);
            return new MemoryStream(Encoding.UTF8.GetBytes(s));
        }

        internal static T FromStream<T>(Stream stream)
        {
            using (StreamReader sr = new(stream))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = new();
                return serializer.Deserialize<T>(reader);
            }
        }

        internal class TestDoc
        {
            public static List<string> PathsToEncrypt { get; } = new List<string>() { "/SensitiveStr", "/SensitiveInt", "/SensitiveArr", "/SensitiveDict" };

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string SensitiveStr { get; set; }

            public int SensitiveInt { get; set; }

            public string[] SensitiveArr { get; set; }

            public Dictionary<string, string> SensitiveDict { get; set; }

            public TestDoc()
            {
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.SensitiveInt == doc.SensitiveInt
                       && this.SensitiveStr == doc.SensitiveStr
                       && this.SensitiveArr?.Equals(doc.SensitiveArr) == true
                       && this.SensitiveDict?.Equals(doc.SensitiveDict) == true;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.NonSensitive);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.SensitiveStr);
                hashCode = (hashCode * -1521134295) + EqualityComparer<int>.Default.GetHashCode(this.SensitiveInt);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string[]>.Default.GetHashCode(this.SensitiveArr);
                hashCode = (hashCode * -1521134295) + EqualityComparer<Dictionary<string, string>>.Default.GetHashCode(this.SensitiveDict);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    SensitiveStr = Guid.NewGuid().ToString(),
                    SensitiveInt = new Random().Next(),
                    SensitiveArr = new string[]
                    {
                        Guid.NewGuid().ToString(),
                        Guid.NewGuid().ToString(),
                    },
                    SensitiveDict = new Dictionary<string, string>
                    {
                        { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                        { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                        { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                        { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                        { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                    }
                };
            }

            public Stream ToStream()
            {
                return TestCommon.ToStream(this);
            }
        }
    }
}
