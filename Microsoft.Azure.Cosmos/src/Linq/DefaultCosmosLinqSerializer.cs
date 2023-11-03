//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal class DefaultCosmosLinqSerializer : ICosmosLinqSerializer
    {
        public CustomAttributeData GetConverterAttribute(MemberExpression memberExpression, Type memberType)
        {
            // There are two ways to specify a custom attribute
            // 1- by specifying the JsonConverterAttribute on a Class/Enum
            //      [JsonConverter(typeof(StringEnumConverter))]
            //      Enum MyEnum
            //      {
            //           ...
            //      }
            //
            // 2- by specifying the JsonConverterAttribute on a property
            //      class MyClass
            //      {
            //           [JsonConverter(typeof(StringEnumConverter))]
            //           public MyEnum MyEnum;
            //      }
            //
            // Newtonsoft gives high precedence to the attribute specified
            // on a property over on a type (class/enum)
            // so we check both attributes and apply the same precedence rules
            // JsonConverterAttribute doesn't allow duplicates so it's safe to
            // use FirstOrDefault()
            CustomAttributeData memberAttribute = memberExpression.Member.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute));
            CustomAttributeData typeAttribute = memberType.GetsCustomAttributes().FirstOrDefault(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute));
            return memberAttribute ?? typeAttribute;
        }

        public string SerializeWithConverter(object value, Type converterType)
        {
            string serializedValue = converterType.GetConstructor(Type.EmptyTypes) != null
                ? JsonConvert.SerializeObject(value, (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(converterType))
                : JsonConvert.SerializeObject(value);

            return serializedValue;
        }

        public string SerializeScalarExpression(ConstantExpression inputExpression)
        {
            return JsonConvert.SerializeObject(inputExpression.Value);
        }

        public string GetMemberName(MemberInfo memberInfo, CosmosLinqSerializerOptions linqSerializerOptions = null)
        {
            string memberName = null;

            // Check if Newtonsoft JsonExtensionDataAttribute is present on the member, if so, return empty member name.
            Newtonsoft.Json.JsonExtensionDataAttribute jsonExtensionDataAttribute = memberInfo.GetCustomAttribute<Newtonsoft.Json.JsonExtensionDataAttribute>(true);
            if (jsonExtensionDataAttribute != null && jsonExtensionDataAttribute.ReadData)
            {
                return null;
            }

            // Json.Net honors JsonPropertyAttribute more than DataMemberAttribute
            // So we check for JsonPropertyAttribute first.
            JsonPropertyAttribute jsonPropertyAttribute = memberInfo.GetCustomAttribute<JsonPropertyAttribute>(true);
            if (jsonPropertyAttribute != null && !string.IsNullOrEmpty(jsonPropertyAttribute.PropertyName))
            {
                memberName = jsonPropertyAttribute.PropertyName;
            }
            else
            {
                DataContractAttribute dataContractAttribute = memberInfo.DeclaringType.GetCustomAttribute<DataContractAttribute>(true);
                if (dataContractAttribute != null)
                {
                    DataMemberAttribute dataMemberAttribute = memberInfo.GetCustomAttribute<DataMemberAttribute>(true);
                    if (dataMemberAttribute != null && !string.IsNullOrEmpty(dataMemberAttribute.Name))
                    {
                        memberName = dataMemberAttribute.Name;
                    }
                }
            }

            if (memberName == null)
            {
                memberName = memberInfo.Name;
            }

            if (linqSerializerOptions != null)
            {
                memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(linqSerializerOptions, memberName);
            }

            return memberName;
        }
    }
}
