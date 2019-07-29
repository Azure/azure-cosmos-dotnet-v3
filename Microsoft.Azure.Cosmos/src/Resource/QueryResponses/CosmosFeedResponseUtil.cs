//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// This is a helper class that is used to get the query response collection.
    /// Each resource type has a different property to access the array.
    /// During JSON deserialization any one of the properties listed will be set.
    /// For example Databases which will then use the base property Data to actually
    /// store the collection. Then the response object will use the Data property to
    /// access the collection. This prevents having a class for each different property.
    /// </summary>
    internal sealed class CosmosFeedResponseUtil<T>
    {
        /// <summary>
        /// All the properties use this to store the collection.
        /// </summary>
        public Collection<T> Data { get; set; }

        public Collection<T> Attachments
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> DocumentCollections
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> Databases
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> Documents
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> Offers
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> Triggers
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> UserDefinedFunctions
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> UserDefinedTypes
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> StoredProcedures
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> Conflicts
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> Users
        {
            get => this.Data;
            set => this.Data = value;
        }

        public Collection<T> Permissions
        {
            get => this.Data;
            set => this.Data = value;
        }
    }
}