//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Internal class created for overriding ToString method for List of generic type T.
    /// </summary>
    internal sealed class JsonSerializableList<T> : List<T>
    {
        public JsonSerializableList(IEnumerable<T> list)
            : base(list)
        {
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static List<T> LoadFrom(string serialized)
        {
            if (serialized == null)
            {
                throw new ArgumentNullException("serialized");
            }

            JArray array = JArray.Parse(serialized);
            return array.ToObject<List<T>>();
        }
    }
}
