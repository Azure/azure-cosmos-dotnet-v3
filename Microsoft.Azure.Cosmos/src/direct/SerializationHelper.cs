//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    /// <summary>
    /// Represents the base class for Azure Cosmos DB database objects and provides methods for serializing and deserializing from JSON.
    /// </summary>
    internal abstract partial class SerializationHelper //We introduce this type so that we dont have to expose the Newtonsoft type publically.
    {
        internal static T LoadFrom<T>(Stream stream, JsonTypeInfo<T> typeInfo)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            return JsonSerializer.Deserialize<T>(stream, typeInfo);
        }
    }
}