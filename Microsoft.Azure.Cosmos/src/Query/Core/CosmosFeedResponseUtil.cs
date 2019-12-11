//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core
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
        private Collection<T> Attachments
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> DocumentCollections
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> Databases
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> Documents
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> Offers
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> Triggers
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> UserDefinedFunctions
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> UserDefinedTypes
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> StoredProcedures
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> Conflicts
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> Users
        {
            get => this.Data;
            set => this.Data = value;
        }

        [JsonProperty]
        private Collection<T> Permissions
        {
            get => this.Data;
            set => this.Data = value;
        }
    }
}