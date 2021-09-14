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
            else if (dict1 != null && dict2 != null && dict1.Count == dict2.Count)
            {
                foreach (KeyValuePair<string, JToken> pair in dict1)
                {
                    if (dict2.TryGetValue(pair.Key, out JToken value))
                    {
                        // Require value be equal.
                        if (!JToken.DeepEquals(value, pair.Value))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // if key is not there
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
