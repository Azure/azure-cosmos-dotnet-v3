//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    internal class NewtonsoftCosmosLinqSerializer : ICosmosLinqSerializer
    {
        private readonly CosmosPropertyNamingPolicy PropertyNamingPolicy;

        public NewtonsoftCosmosLinqSerializer(CosmosPropertyNamingPolicy propertyNamingPolicy)
        {
            this.PropertyNamingPolicy = propertyNamingPolicy;
        }

        public bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType)
        {
            CustomAttributeData memberAttribute = memberExpression.Member.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute));
            CustomAttributeData typeAttribute = memberType.GetsCustomAttributes().FirstOrDefault(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute));

            return memberAttribute != null || typeAttribute != null;
        }

        public string Serialize(object value, MemberExpression memberExpression, Type memberType)
        {
            CustomAttributeData memberAttribute = memberExpression.Member.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute));
            CustomAttributeData typeAttribute = memberType.GetsCustomAttributes().FirstOrDefault(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute));
            CustomAttributeData converterAttribute = memberAttribute ?? typeAttribute;

            Debug.Assert(converterAttribute.ConstructorArguments.Count > 0, $"{nameof(DefaultCosmosLinqSerializer)} Assert!", "At least one constructor argument exists.");
            Type converterType = (Type)converterAttribute.ConstructorArguments[0].Value;

            string serializedValue = converterType.GetConstructor(Type.EmptyTypes) != null
                ? JsonConvert.SerializeObject(value, (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(converterType))
                : JsonConvert.SerializeObject(value);

            return serializedValue;
        }

        public string SerializeScalarExpression(ConstantExpression inputExpression)
        {
            if (this.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase)
            {
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                };
                //serializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter()); //todo: need to find way to identify StringEnumCOnverter decoration

                return JsonConvert.SerializeObject(inputExpression.Value, serializerSettings);
            }

            return JsonConvert.SerializeObject(inputExpression.Value);
        }

        public string SerializeMemberName(MemberInfo memberInfo)
        {
            string memberName = memberInfo.Name;

            // Check if Newtonsoft JsonExtensionDataAttribute is present on the member, if so, return empty member name.
            Newtonsoft.Json.JsonExtensionDataAttribute jsonExtensionDataAttribute = memberInfo.GetCustomAttribute<Newtonsoft.Json.JsonExtensionDataAttribute>(true);
            if (jsonExtensionDataAttribute != null && jsonExtensionDataAttribute.ReadData)
            {
                return null;
            }

            JsonPropertyAttribute jsonPropertyAttribute = memberInfo.GetCustomAttribute<JsonPropertyAttribute>(true);
            if (jsonPropertyAttribute != null && !string.IsNullOrEmpty(jsonPropertyAttribute.PropertyName))
            {
                memberName = jsonPropertyAttribute.PropertyName;
            }

            memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(this.PropertyNamingPolicy, memberName);

            return memberName;
        }
    }
}
