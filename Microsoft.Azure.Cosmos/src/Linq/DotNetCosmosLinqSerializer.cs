//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class DotNetCosmosLinqSerializer : ICosmosLinqSerializer
    {
        private readonly CosmosSerializer CustomCosmosSerializer;

        private readonly CosmosPropertyNamingPolicy PropertyNamingPolicy;

        public DotNetCosmosLinqSerializer(CosmosSerializer customCosmosSerializer, CosmosPropertyNamingPolicy propertyNamingPolicy)
        {
            this.CustomCosmosSerializer = customCosmosSerializer ?? new CosmosJsonDotNetSerializer();
            this.PropertyNamingPolicy = propertyNamingPolicy;
        }

        public bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType)
        {
            CustomAttributeData converterAttribute = memberExpression.Member.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(System.Text.Json.Serialization.JsonConverterAttribute));
            return converterAttribute != null;
        }

        public string Serialize(object value, MemberExpression memberExpression, Type memberType)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();

            CustomAttributeData converterAttribute = memberExpression.Member.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(System.Text.Json.Serialization.JsonConverterAttribute));

            Debug.Assert(converterAttribute.ConstructorArguments.Count > 0, $"{nameof(DefaultCosmosLinqSerializer)} Assert!", "No constructor arguments exist");
            Type converterType = (Type)converterAttribute.ConstructorArguments[0].Value;

            if (converterType == typeof(JsonStringEnumConverter))
            {
                options.Converters.Add(new JsonStringEnumConverter());
            }

            return System.Text.Json.JsonSerializer.Serialize(value, options);
        }

        public string SerializeScalarExpression(ConstantExpression inputExpression)
        {
            StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);

            using (Stream stream = this.CustomCosmosSerializer.ToStream(inputExpression.Value))
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    string propertyValue = streamReader.ReadToEnd();
                    writer.Write(propertyValue);
                    return writer.ToString();
                }
            }
        }

        public string SerializeMemberName(MemberInfo memberInfo)
        {
            JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);

            string memberName = jsonPropertyNameAttribute != null && !string.IsNullOrEmpty(jsonPropertyNameAttribute.Name)
                ? jsonPropertyNameAttribute.Name
                : memberInfo.Name;

            memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(this.PropertyNamingPolicy, memberName);

            return memberName;
        }
    }
}
