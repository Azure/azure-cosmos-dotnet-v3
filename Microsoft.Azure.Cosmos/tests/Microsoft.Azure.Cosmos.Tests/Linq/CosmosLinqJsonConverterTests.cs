//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using global::Azure.Core.Serialization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [TestClass]
    public class CosmosLinqJsonConverterTests
    {
        [TestMethod]
        public void DateTimeKindIsPreservedTest()
        {
            // Should work for UTC time
            DateTime utcDate = new DateTime(2022, 5, 26, 0, 0, 0, DateTimeKind.Utc);
            Expression<Func<TestDocument, bool>> expr = a => a.StartDate <= utcDate;
            string sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"StartDate\"] <= \"2022-05-26\")", sql);

            // Should work for local time
            DateTime localDate = new DateTime(2022, 5, 26, 0, 0, 0, DateTimeKind.Local);
            expr = a => a.StartDate <= localDate;
            sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"StartDate\"] <= \"2022-05-26\")", sql);
        }

        [TestMethod]
        public void EnumIsPreservedAsINTest()
        {
            // Arrange
            CosmosLinqSerializerOptions options = new()
            {
                //CustomCosmosSerializer = new TestCustomJsonSerializer()
            };

            // Act
            TestEnum[] values = new[] { TestEnum.One, TestEnum.Two };
            Expression<Func<TestEnumDocument, bool>> expr = a => values.Contains(a.Value);
            
            string sql = SqlTranslator.TranslateExpression(expr.Body, options);

            // Assert
            // Assert.AreEqual("(a[\"Value\"] IN (\"One\", \"Two\"))", sql); // <- TODO - Desired Behavior with CustomSerializer //mayapainter
            Assert.AreEqual("(a[\"Value\"] IN (0, 1))", sql); // <- Actual behavior, with ability to set custom serializor reverted
        }

        [TestMethod]
        public void EnumIsPreservedAsEQUALSTest()
        {
            // Arrange
            CosmosLinqSerializerOptions options = new()
            {
                //CustomCosmosSerializer = new TestCustomJsonSerializer()
            };

            // Act
            TestEnum statusValue = TestEnum.One;
            Expression<Func<TestEnumDocument, bool>> expr = a => a.Value == statusValue;

            string sql = SqlTranslator.TranslateExpression(expr.Body, options);

            // Assert
            // Assert.AreEqual("(a[\"Value\"] = \"One\")", sql); // <- THIS is the correct value, if we are able to use the custom serializer //mayapainter
            Assert.AreEqual("(a[\"Value\"] = 0)", sql); // <- THIS is the current mis-behavior of the SDK
        }

        [TestMethod]
        public void EnumIsPreservedAsEXPRESSIONTest()
        {
            // Arrange
            CosmosLinqSerializerOptions options = new()
            {
                //CustomCosmosSerializer = new TestCustomJsonSerializer()
            };

            // Act

            // Get status constant
            ConstantExpression status = Expression.Constant(TestEnum.One);

            // Get member access expression
            ParameterExpression arg = Expression.Parameter(typeof(TestEnumNewtonsoftDocument), "a");

            // Access the value property
            MemberExpression docValueExpression = Expression.MakeMemberAccess(
                arg,
                typeof(TestEnumNewtonsoftDocument).GetProperty(nameof(TestEnumNewtonsoftDocument.Value))!
            );

            // Create comparison expression
            BinaryExpression expression = Expression.Equal(
                docValueExpression,
                status
            );

            // Create lambda expression
            Expression<Func<TestEnumNewtonsoftDocument, bool>> lambda = 
                Expression.Lambda<Func<TestEnumNewtonsoftDocument, bool>>(expression, arg);

            string sql = SqlTranslator.TranslateExpression(lambda.Body, options);

            // Assert
            Assert.AreEqual("(a[\"Value\"] = \"One\")", sql);
        }

        enum TestEnum
        {
            One,
            Two,
            Three,
        }

        class TestEnumDocument
        {
            [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))] // TODO: Remove this once we have the ability to use custom serializer for LINQ queries
            public TestEnum Value { get; set; }
        }

        class TestEnumNewtonsoftDocument
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public TestEnum Value { get; set; }
        }

        class TestDocument
        {
            [JsonConverter(typeof(DateJsonConverter))]
            public DateTime StartDate { get; set; }
        }

        class DateJsonConverter : IsoDateTimeConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is DateTime dateTime)
                {
                    writer.WriteValue(dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
                else
                {
                    base.WriteJson(writer, value, serializer);
                }
            }
        }

        [TestMethod]
        public void TestNewtonsoftExtensionDataQuery()
        {
            Expression<Func<DocumentWithExtensionData, bool>> expr = a => (string)a.NewtonsoftExtensionData["foo"] == "bar";
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("(a[\"foo\"] = \"bar\")", sql);
        }

        [TestMethod]
        public void TestSystemTextJsonExtensionDataQuery()
        {
            Expression<Func<DocumentWithExtensionData, bool>> expr = a => ((object)a.NetExtensionData["foo"]) == "bar";
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            // TODO: This is a limitation in the translator. It should be able to handle STJ extension data, if a custom
            // JSON serializer is specified. //mayapainter - what is STJ?
            Assert.AreEqual("(a[\"NetExtensionData\"][\"foo\"] = \"bar\")", sql);
        }

        class DocumentWithExtensionData
        {
            [Newtonsoft.Json.JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> NewtonsoftExtensionData { get; set; }

            [System.Text.Json.Serialization.JsonExtensionData()]
            public Dictionary<string, System.Text.Json.JsonElement> NetExtensionData { get; set; }
        }

        /// <remarks>
        // See: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Usage/SystemTextJson/CosmosSystemTextJsonSerializer.cs
        /// </remarks>
        class TestCustomJsonSerializer : CosmosSerializer
        {
            private readonly JsonObjectSerializer systemTextJsonSerializer;

            public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = {
                    new System.Text.Json.Serialization.JsonStringEnumConverter(),
                }
            };

            public TestCustomJsonSerializer()
            {
                this.systemTextJsonSerializer = new JsonObjectSerializer(JsonOptions);
            }

            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                {
                    if (stream.CanSeek && stream.Length == 0)
                    {
                        return default;
                    }

                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        return (T)(object)stream;
                    }

                    return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream stream = new ();

                this.systemTextJsonSerializer.Serialize(stream, input, typeof(T), default);
                stream.Position = 0;
                return stream;
            }
        }
    }
}
