//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
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
        public SqlScalarExpression ApplyCustomConverters(MemberExpression memberExpression, Type memberType, SqlLiteralScalarExpression sqlLiteralScalarExpression)
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
            CustomAttributeData memberAttribute = memberExpression.Member.CustomAttributes.Where(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute)).FirstOrDefault();
            CustomAttributeData typeAttribute = memberType.GetsCustomAttributes().Where(ca => ca.AttributeType == typeof(Newtonsoft.Json.JsonConverterAttribute)).FirstOrDefault();

            CustomAttributeData converterAttribute = memberAttribute ?? typeAttribute;
            if (converterAttribute != null)
            {
                Debug.Assert(converterAttribute.ConstructorArguments.Count > 0);

                Type converterType = (Type)converterAttribute.ConstructorArguments[0].Value;

                object value = default(object);
                // Enum
                if (memberType.IsEnum())
                {
                    Number64 number64 = ((SqlNumberLiteral)sqlLiteralScalarExpression.Literal).Value;
                    if (number64.IsDouble)
                    {
                        value = Enum.ToObject(memberType, Number64.ToDouble(number64));
                    }
                    else
                    {
                        value = Enum.ToObject(memberType, Number64.ToLong(number64));
                    }

                }
                // DateTime
                else if (memberType == typeof(DateTime))
                {
                    SqlStringLiteral serializedDateTime = (SqlStringLiteral)sqlLiteralScalarExpression.Literal;
                    value = DateTime.Parse(serializedDateTime.Value, provider: null, DateTimeStyles.RoundtripKind);
                }

                if (value != default(object))
                {
                    string serializedValue;

                    if (converterType.GetConstructor(Type.EmptyTypes) != null)
                    {
                        serializedValue = JsonConvert.SerializeObject(value, (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(converterType));
                    }
                    else
                    {
                        serializedValue = JsonConvert.SerializeObject(value);
                    }

                    return CosmosElement.Parse(serializedValue).Accept(CosmosElementToSqlScalarExpressionVisitor.Singleton);
                }
            }

            return sqlLiteralScalarExpression;
        }

        public SqlScalarExpression ConvertToSqlScalarExpression(ConstantExpression inputExpression, IDictionary<object, string> parameters)
        {
            return CosmosElement.Parse(JsonConvert.SerializeObject(inputExpression.Value)).Accept(CosmosElementToSqlScalarExpressionVisitor.Singleton);
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
