//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json.Linq;

    internal static class DictionaryExtensions
    {
        /// <summary>
        /// Compare two dictionaries and return true if they have same pair of key-values
        /// </summary>
        internal static bool EqualsTo(this IDictionary<string, JToken> dict1, IDictionary<string, JToken> dict2)
        {
            if (dict1 == null && dict2 == null)
            {
                return true;
            }

            if (dict1 == null || dict2 == null || dict1.Count != dict2.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, JToken> pair in dict1)
            {
                if (!dict2.TryGetValue(pair.Key, out JToken value) || !JToken.DeepEquals(value, pair.Value))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool EqualsTo<U, T>(this IDictionary<U, T> dict1, IDictionary<U, T> dict2)
        {
            if (dict1 == null && dict2 == null)
            {
                return true;
            }

            if (dict1 == null || dict2 == null || dict1.Count != dict2.Count)
            {
                return false;
            }

            foreach (KeyValuePair<U, T> pair in dict1)
            {
                if (!dict2.TryGetValue(pair.Key, out T value) || !pair.Value.Equals(value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
