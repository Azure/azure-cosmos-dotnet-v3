//-----------------------------------------------------------------------
// <copyright file="JsonTokenEqualityComparer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    internal sealed class JsonTokenEqualityComparer : IEqualityComparer<JToken>
    {
        public static readonly JsonTokenEqualityComparer Value = new JsonTokenEqualityComparer();

        public bool Equals(double double1, double double2)
        {
            if (double1 == double2)
            {
                return true;
            }

            // Backend uses old complier while managed uses new complier which has floating point differences
            if (Math.Abs(double1 - double2) <= 1E-7)
            {
                return true;
            }

            return false;
        }

        public bool Equals(string string1, string string2)
        {
            return string1.Equals(string2);
        }

        public bool Equals(bool bool1, bool bool2)
        {
            return bool1 == bool2;
        }

        public bool Equals(JArray jArray1, JArray jArray2)
        {
            if (jArray1.Count != jArray2.Count)
            {
                return false;
            }

            IEnumerable<Tuple<JToken, JToken>> pairwiseElements = jArray1
                .Zip(jArray2, (first, second) => new Tuple<JToken, JToken>(first, second));
            bool deepEquals = true;
            foreach (Tuple<JToken, JToken> pairwiseElement in pairwiseElements)
            {
                deepEquals &= this.Equals(pairwiseElement.Item1, pairwiseElement.Item2);
            }

            return deepEquals;
        }

        public bool Equals(JObject jObject1, JObject jObject2)
        {
            if (jObject1.Count != jObject2.Count)
            {
                return false;
            }

            bool deepEquals = true;
            foreach (KeyValuePair<string, JToken> kvp in jObject1)
            {
                string name = kvp.Key;
                JToken value1 = kvp.Value;

                if (jObject2.TryGetValue(name, out JToken value2))
                {
                    deepEquals &= this.Equals(value1, value2);
                }
                else
                {
                    return false;
                }
            }

            return deepEquals;
        }

        public bool Equals(JToken jToken1, JToken jToken2)
        {
            if (object.ReferenceEquals(jToken1, jToken2))
            {
                return true;
            }

            if (jToken1 == null || jToken2 == null)
            {
                return false;
            }

            JsonType type1 = JTokenTypeToJsonType(jToken1.Type);
            JsonType type2 = JTokenTypeToJsonType(jToken2.Type);

            // If the types don't match
            if (type1 != type2)
            {
                return false;
            }

            switch (type1)
            {

                case JsonType.Object:
                    return this.Equals((JObject)jToken1, (JObject)jToken2);
                case JsonType.Array:
                    return this.Equals((JArray)jToken1, (JArray)jToken2);
                case JsonType.Number:
                    return this.Equals((double)jToken1, (double)jToken2);
                case JsonType.String:
                    return this.Equals(jToken1.ToString(), jToken2.ToString());
                case JsonType.Boolean:
                    return this.Equals((bool)jToken1, (bool)jToken2);
                case JsonType.Null:
                    return true;
                default:
                    throw new InvalidEnumArgumentException(nameof(type1));
            }
        }

        public int GetHashCode(JToken obj)
        {
            return 0;
        }

        private enum JsonType
        {
            Number,
            String,
            Null,
            Array,
            Object,
            Boolean
        }

        private static JsonType JTokenTypeToJsonType(JTokenType type)
        {
            switch (type)
            {

                case JTokenType.Object:
                    return JsonType.Object;
                case JTokenType.Array:
                    return JsonType.Array;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return JsonType.Number;
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                case JTokenType.Date:
                case JTokenType.String:
                    return JsonType.String;
                case JTokenType.Boolean:
                    return JsonType.Boolean;
                case JTokenType.Null:
                    return JsonType.Null;
                case JTokenType.None:
                case JTokenType.Undefined:
                case JTokenType.Constructor:
                case JTokenType.Property:
                case JTokenType.Comment:
                case JTokenType.Raw:
                case JTokenType.Bytes:
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
