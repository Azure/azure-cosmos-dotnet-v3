//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;

    /// <summary>
    /// This is a helper class that is used to get the query response collection.
    /// Each resource type has a different property to access the array.
    /// During JSON deserialization any one of the properties listed will be set.
    /// For example Databases which will then use the base property Data to actually
    /// store the collection. Then the response object will use the Data property to
    /// access the collection. This prevents having a class for each different property.
    /// </summary>
    public sealed class CosmosFeedResponseUtil<T>
    {
        /// <summary>
        /// All the properties use this to store the collection.
        /// </summary>
        public Collection<T> Data { get; private set; }

        internal Type InnerType { get; } = typeof(T);

        [JsonProperty]
        internal Collection<T> Attachments
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> DocumentCollections
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> Databases
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> Documents
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> Offers
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> Triggers
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> UserDefinedFunctions
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> UserDefinedTypes
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> StoredProcedures
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> Conflicts
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> Users
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> Permissions
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        internal Collection<T> ClientEncryptionKeys
        {
            get => this.Data;
            set => this.Data = value;
        }
    }
}