//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Newtonsoft.Json;

    internal class DotNetCosmosLinqSerializer : ICosmosLinqSerializer
    {
        private readonly CosmosLinqSerializerOptions LinqSerializerOptions;

        public DotNetCosmosLinqSerializer(CosmosLinqSerializerOptions linqSerializerOptions)
        {
            this.LinqSerializerOptions = linqSerializerOptions;
        }

        public bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType)
        {
            CustomAttributeData converterAttribute = memberExpression.Member.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(System.Text.Json.Serialization.JsonConverterAttribute));
            return converterAttribute != null;
        }

        public string Serialize(object value, MemberExpression memberExpression, Type memberType)
        {
            //check this
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            return System.Text.Json.JsonSerializer.Serialize(value, options);
        }

        public string SerializeScalarExpression(ConstantExpression inputExpression)
        {
            if (this.LinqSerializerOptions.CustomCosmosSerializer != null)
            {
                StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);

                using (Stream stream = this.LinqSerializerOptions.CustomCosmosSerializer.ToStream(inputExpression.Value))
                {
                    using (StreamReader streamReader = new StreamReader(stream))
                    {
                        string propertyValue = streamReader.ReadToEnd();
                        writer.Write(propertyValue);
                        return writer.ToString();
                    }
                }
            }
            
            return JsonConvert.SerializeObject(inputExpression.Value); //todo: fix this, throw error if custom serializer is null?
        }

        public string SerializeMemberName(MemberInfo memberInfo)
        {
            JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);

            string memberName = jsonPropertyNameAttribute != null && !string.IsNullOrEmpty(jsonPropertyNameAttribute.Name)
                ? jsonPropertyNameAttribute.Name
                : memberInfo.Name;

            if (this.LinqSerializerOptions != null)
            {
                memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(this.LinqSerializerOptions, memberName);
            }

            return memberName;
        }
    }
}
