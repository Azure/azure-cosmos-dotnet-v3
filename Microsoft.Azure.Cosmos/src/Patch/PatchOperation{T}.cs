//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary>
    /// Defines PatchOperation with a value parameter.
    /// </summary>
    /// <typeparam name="T">Data type of value provided for PatchOperation.</typeparam>
    internal abstract class PatchOperation<T> : PatchOperation
    {
        /// <summary>
        /// Value parameter.
        /// </summary>
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Value)]
        public abstract T Value { get; }
    }
}
