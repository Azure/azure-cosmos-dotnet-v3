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
    using System.Reflection;
    using System.Text.Json.Serialization;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [TestClass]
    public class CosmosLinqJsonConverterTests
    {
        private readonly CosmosLinqSerializerOptions defaultPublicOptions = new()
        {
            LinqSerializerType = CosmosLinqSerializerType.Default
        };

        private readonly CosmosLinqSerializerOptions customPublicOptions = new()
        {
            LinqSerializerType = CosmosLinqSerializerType.CustomCosmosSerializer
        };

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
            CosmosLinqSerializerOptionsInternal options = CosmosLinqSerializerOptionsInternal.Create(this.customPublicOptions, new TestCustomJsonSerializer());
            CosmosLinqSerializerOptionsInternal defaultOptions = CosmosLinqSerializerOptionsInternal.Create(this.defaultPublicOptions, new TestCustomJsonSerializer());

            TestEnum[] values = new[] { TestEnum.One, TestEnum.Two };

            Expression<Func<TestEnumDocument, bool>> expr = a => values.Contains(a.Value);
            string sql = SqlTranslator.TranslateExpression(expr.Body, options);

            Expression<Func<TestEnumNewtonsoftDocument, bool>> exprNewtonsoft = a => values.Contains(a.Value);
            string sqlDefault = SqlTranslator.TranslateExpression(exprNewtonsoft.Body, defaultOptions);

            Assert.AreEqual("(a[\"Value\"] IN (\"One\", \"Two\"))", sql);
            Assert.AreEqual("(a[\"Value\"] IN (0, 1))", sqlDefault);
        }

        [TestMethod]
        public void EnumIsPreservedAsEQUALSTest()
        {
            CosmosLinqSerializerOptionsInternal options = CosmosLinqSerializerOptionsInternal.Create(this.customPublicOptions, new TestCustomJsonSerializer());
            CosmosLinqSerializerOptionsInternal defaultOptions = CosmosLinqSerializerOptionsInternal.Create(this.defaultPublicOptions, new TestCustomJsonSerializer());

            TestEnum statusValue = TestEnum.One;

            Expression<Func<TestEnumDocument, bool>> expr = a => a.Value == statusValue;
            string sql = SqlTranslator.TranslateExpression(expr.Body, options);

            Expression<Func<TestEnumNewtonsoftDocument, bool>> exprDefault = a => a.Value == statusValue;
            string sqlNewtonsoft = SqlTranslator.TranslateExpression(exprDefault.Body, defaultOptions);

            Assert.AreEqual("(a[\"Value\"] = \"One\")", sql);
            Assert.AreEqual("(a[\"Value\"] = \"One\")", sqlNewtonsoft);
        }

        [TestMethod]
        public void EnumIsPreservedAsEXPRESSIONTest()
        {
            CosmosLinqSerializerOptionsInternal options = CosmosLinqSerializerOptionsInternal.Create(this.customPublicOptions, new TestCustomJsonSerializer());
            CosmosLinqSerializerOptionsInternal defaultOptions = CosmosLinqSerializerOptionsInternal.Create(this.defaultPublicOptions, new TestCustomJsonSerializer());

            // Get status constant
            ConstantExpression status = Expression.Constant(TestEnum.One);

            // Get member access expression
            ParameterExpression arg = Expression.Parameter(typeof(TestEnumDocument), "a");
            ParameterExpression argNewtonsoft = Expression.Parameter(typeof(TestEnumNewtonsoftDocument), "a");

            // Access the value property
            MemberExpression docValueExpression = Expression.MakeMemberAccess(
                arg,
                typeof(TestEnumDocument).GetProperty(nameof(TestEnumDocument.Value))!
            );
            MemberExpression docValueExpressionDefault = Expression.MakeMemberAccess(
                argNewtonsoft,
                typeof(TestEnumNewtonsoftDocument).GetProperty(nameof(TestEnumNewtonsoftDocument.Value))!
            );

            // Create comparison expression
            BinaryExpression expression = Expression.Equal(
                docValueExpression,
                status
            );
            BinaryExpression expressionDefault = Expression.Equal(
                docValueExpressionDefault,
                status
            );

            // Create lambda expression
            Expression<Func<TestEnumDocument, bool>> lambda =
                Expression.Lambda<Func<TestEnumDocument, bool>>(expression, arg);
            string sql = SqlTranslator.TranslateExpression(lambda.Body, options);

            Expression<Func<TestEnumNewtonsoftDocument, bool>> lambdaNewtonsoft =
                Expression.Lambda<Func<TestEnumNewtonsoftDocument, bool>>(expressionDefault, argNewtonsoft);
            string sqlDefault = SqlTranslator.TranslateExpression(lambdaNewtonsoft.Body, defaultOptions);

            Assert.AreEqual("(a[\"Value\"] = \"One\")", sql);
            Assert.AreEqual("(a[\"Value\"] = \"One\")", sqlDefault);
        }

        enum TestEnum
        {
            One,
            Two,
            Three,
        }

        class TestEnumDocument
        {
            public TestEnum Value { get; set; }
        }

        class TestEnumNewtonsoftDocument
        {
            [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
            public TestEnum Value { get; set; }
        }

        class TestDocument
        {
            [Newtonsoft.Json.JsonConverter(typeof(DateJsonConverter))]
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
            CosmosLinqSerializerOptionsInternal defaultOptions = CosmosLinqSerializerOptionsInternal.Create(this.defaultPublicOptions, null);

            Expression<Func<DocumentWithExtensionData, bool>> expr = a => (string)a.NewtonsoftExtensionData["foo"] == "bar";
            string sql = SqlTranslator.TranslateExpression(expr.Body, defaultOptions);

            Assert.AreEqual("(a[\"foo\"] = \"bar\")", sql);
        }

        [TestMethod]
        public void TestSystemTextJsonExtensionDataQuery()
        {
            CosmosLinqSerializerOptionsInternal dotNetOptions = CosmosLinqSerializerOptionsInternal.Create(this.customPublicOptions, new TestCustomJsonSerializer());

            Expression<Func<DocumentWithExtensionData, bool>> expr = a => ((object)a.NetExtensionData["foo"]) == "bar";
            string sql = SqlTranslator.TranslateExpression(expr.Body, dotNetOptions);

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
        class TestCustomJsonSerializer : CosmosSerializer, ICosmosLinqSerializer
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
                MemoryStream stream = new();

                this.systemTextJsonSerializer.Serialize(stream, input, input.GetType(), default);
                stream.Position = 0;
                return stream;
            }

            public string SerializeMemberName(MemberInfo memberInfo)
            {
                JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);

                string memberName = jsonPropertyNameAttribute != null && !string.IsNullOrEmpty(jsonPropertyNameAttribute.Name)
                    ? jsonPropertyNameAttribute.Name
                    : memberInfo.Name;

                return memberName;
            }
        }
    }
}
