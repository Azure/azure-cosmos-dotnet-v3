//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json.Linq;

    internal static class DictionaryExtensions
    {
        internal static bool EqualsTo(this IDictionary<string, long> dict1, IDictionary<string, long> dict2)
        {
            if (dict1 == null && dict2 == null)
            {
                return true;
            }

            if (dict1 == null || dict2 == null || dict1.Count != dict2.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, long> pair in dict1)
            {
                if (!dict2.TryGetValue(pair.Key, out long value) || value != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
