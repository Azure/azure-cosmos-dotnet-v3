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
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    internal class DataContractCosmosLinqSerializer : ICosmosLinqSerializer
    {
        private readonly CosmosLinqSerializerOptions LinqSerializerOptions;

        public DataContractCosmosLinqSerializer(CosmosLinqSerializerOptions linqSerializerOptions)
        {
            this.LinqSerializerOptions = linqSerializerOptions;
        }

        public bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType)
        {
            CustomAttributeData converterAttribute = memberExpression.Member.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute));
            return converterAttribute != null;
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
            if (this.LinqSerializerOptions != null && this.LinqSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase)
            {
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                return JsonConvert.SerializeObject(inputExpression.Value, serializerSettings);
            }

            return JsonConvert.SerializeObject(inputExpression.Value);
        }

        public string SerializeMemberName(MemberInfo memberInfo)
        {
            string memberName = null;

            // Check if Newtonsoft JsonExtensionDataAttribute is present on the member, if so, return empty member name.
            Newtonsoft.Json.JsonExtensionDataAttribute jsonExtensionDataAttribute = memberInfo.GetCustomAttribute<Newtonsoft.Json.JsonExtensionDataAttribute>(true);
            if (jsonExtensionDataAttribute != null && jsonExtensionDataAttribute.ReadData)
            {
                return null;
            }

            DataContractAttribute dataContractAttribute = memberInfo.DeclaringType.GetCustomAttribute<DataContractAttribute>(true);
            if (dataContractAttribute != null)
            {
                DataMemberAttribute dataMemberAttribute = memberInfo.GetCustomAttribute<DataMemberAttribute>(true);
                if (dataMemberAttribute != null && !string.IsNullOrEmpty(dataMemberAttribute.Name))
                {
                    memberName = dataMemberAttribute.Name;
                }
            }

            memberName ??= memberInfo.Name;

            if (this.LinqSerializerOptions != null)
            {
                memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(this.LinqSerializerOptions, memberName);
            }

            return memberName;
        }
    }
}
