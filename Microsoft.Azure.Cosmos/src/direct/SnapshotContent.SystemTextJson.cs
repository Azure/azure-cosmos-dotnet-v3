//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    internal sealed partial class SnapshotContent
    {
        private T GetResourceIfDeserialized<T>(string body, JsonTypeInfo<T> typeInfo)
            where T : class
        {
            try
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(body);
                using (MemoryStream stream = new MemoryStream(byteArray))
                {
                    return SerializationHelper.LoadFrom<T>(stream, typeInfo);
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
