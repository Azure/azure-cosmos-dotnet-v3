//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json.Linq;

    internal static class AppUtils
    {
        /// <summary>
        /// Compare two dictonaries and return true if they have same pair of key-values
        /// </summary>
        internal static bool CompareDictionary(IDictionary<string, JToken> dict1, IDictionary<string, JToken> dict2)
        {
            if (dict1 == null && dict2 == null)
            {
                return true;
            }
            else if (dict1 != null && dict2 != null)
            {
                bool isEqual = false;
                if (dict1.Count == dict2.Count)
                {
                    isEqual = true;
                    foreach (KeyValuePair<string, JToken> pair in dict1)
                    {
                        if (dict2.TryGetValue(pair.Key, out JToken value))
                        {
                            // Require value be equal.
                            if (!value.ToString().Equals(pair.Value.ToString()))
                            {
                                isEqual = false;
                                break;
                            }
                        }
                        else
                        {
                            // Require key be present.
                            isEqual = false;
                            break;
                        }
                    }
                }
                return isEqual;
            }

            return false;
        }
    }
}
