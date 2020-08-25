//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the conflict resolution policy configuration for specifying how to resolve conflicts 
    /// in case writes from different regions result in conflicts on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <example>
    /// A collection with custom conflict resolution with no user-registered stored procedure.
    /// <![CDATA[
    /// var collectionSpec = new DocumentCollection
    /// {
    ///     Id = "Multi-master collection",
    ///     ConflictResolutionPolicy policy = new ConflictResolutionPolicy
    ///     {
    ///         Mode = ConflictResolutionMode.Custom
    ///     }
    /// };
    /// DocumentCollection collection = await client.CreateDocumentCollectionAsync(databaseLink, collectionSpec });
    /// ]]>
    /// </example>
    /// <example>
    /// A collection with custom conflict resolution with a user-registered stored procedure.
    /// <![CDATA[
    /// var collectionSpec = new DocumentCollection
    /// {
    ///     Id = "Multi-master collection",
    ///     ConflictResolutionPolicy policy = new ConflictResolutionPolicy
    ///     {
    ///         Mode = ConflictResolutionMode.Custom,
    ///         ConflictResolutionProcedure = "conflictResolutionSprocName"
    ///     }
    /// };
    /// DocumentCollection collection = await client.CreateDocumentCollectionAsync(databaseLink, collectionSpec });
    /// ]]>
    /// </example>
    /// <example>
    /// A collection with last writer wins conflict resolution, based on a path in the conflicting documents.
    /// <![CDATA[
    /// var collectionSpec = new DocumentCollection
    /// {
    ///     Id = "Multi-master collection",
    ///     ConflictResolutionPolicy policy = new ConflictResolutionPolicy
    ///     {
    ///         Mode = ConflictResolutionMode.LastWriterWins,
    ///         ConflictResolutionPath = "/path/for/conflict/resolution"
    ///     }
    /// };
    /// DocumentCollection collection = await client.CreateDocumentCollectionAsync(databaseLink, collectionSpec });
    /// ]]>
    /// </example>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class ConflictResolutionPolicy : JsonSerializable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConflictResolutionPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        public ConflictResolutionPolicy()
        {
            // defaults
            this.Mode = ConflictResolutionMode.LastWriterWins;
        }

        /// <summary>
        /// Gets or sets the <see cref="ConflictResolutionMode"/> in the Azure Cosmos DB service. By default it is <see cref="ConflictResolutionMode.LastWriterWins"/>.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="ConflictResolutionMode"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Mode)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ConflictResolutionMode Mode
        {
            get
            {
                ConflictResolutionMode result = ConflictResolutionMode.LastWriterWins;
                string strValue = base.GetValue<string>(Constants.Properties.Mode);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (ConflictResolutionMode)Enum.Parse(typeof(ConflictResolutionMode), strValue, true);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.Mode, value.ToString());
            }
        }

        /// <summary>
        /// Gets or sets the path which is present in each document in the Azure Cosmos DB service for last writer wins conflict-resolution.
        /// This path must be present in each document and must be an integer value.
        /// In case of a conflict occuring on a document, the document with the higher integer value in the specified path will be picked.
        /// If the path is unspecified, by default the <see cref="Resource.Timestamp"/> path will be used.
        /// </summary>
        /// <remarks>
        /// This value should only be set when using <see cref="ConflictResolutionMode.LastWriterWins"/>
        /// </remarks>
        /// <value>
        /// <![CDATA[The path to check values for last-writer wins conflict resolution. That path is a rooted path of the property in the document, such as "/name/first".]]>
        /// </value>
        /// <example>
        /// <![CDATA[
        /// conflictResolutionPolicy.ConflictResolutionPath = "/name/first";
        /// ]]>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.ConflictResolutionPath)]
        public string ConflictResolutionPath
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ConflictResolutionPath);
            }
            set
            {
                base.SetValue(Constants.Properties.ConflictResolutionPath, value);
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="StoredProcedure"/> which is used for conflict resolution in the Azure Cosmos DB service.
        /// This stored procedure may be created after the <see cref="DocumentCollection"/> is created and can be changed as required. 
        /// </summary>
        /// <remarks>
        /// 1. This value should only be set when using <see cref="ConflictResolutionMode.Custom"/>
        /// 2. In case the stored procedure fails or throws an exception, the conflict resolution will default to registering conflicts in the conflicts feed"/>.
        /// 3. The user can provide the stored procedure <see cref="Resource.Id"/> or <see cref="Resource.ResourceId"/>.
        /// </remarks>
        /// <value>
        /// <![CDATA[The stored procedure to perform conflict resolution.]]>
        /// </value>
        /// <example>
        /// <![CDATA[
        /// conflictResolutionPolicy.ConflictResolutionProcedure = "/name/first";
        /// ]]>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.ConflictResolutionProcedure)]
        public string ConflictResolutionProcedure
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ConflictResolutionProcedure);
            }
            set
            {
                base.SetValue(Constants.Properties.ConflictResolutionProcedure, value);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            Helpers.ValidateEnumProperties<ConflictResolutionMode>(this.Mode);
            base.GetValue<string>(Constants.Properties.ConflictResolutionPath);
            base.GetValue<string>(Constants.Properties.ConflictResolutionProcedure);
        }
    }
}