//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Microsoft.Azure.Documents;

    public class CosmosScriptSettings
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosScriptSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosScriptSettings()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosScriptSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="Body">The body of the script</param>
        /// <param name="Type">The CosmosScriptType of the script, which is either StoredProcedure, UDF, PreTrigger, PostTrigger</param>
        public CosmosScriptSettings(string Id, string Body, CosmosScriptType Type)
        {
            this.Id = Id;
            this.Body = Body;
            this.Type = Type;
        }

        /// <summary>
        /// Gets or sets the Id of the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Id associated with the resource.</value>
        /// <remarks>
        /// <para>
        /// Every resource within an Azure Cosmos DB database account needs to have a unique identifier. 
        /// Unlike <see cref="Resource.ResourceId"/>, which is set internally, this Id is settable by the user and is not immutable.
        /// </para>
        /// <para>
        /// When working with document resources, they too have this settable Id property. 
        /// If an Id is not supplied by the user the SDK will automatically generate a new GUID and assign its value to this property before
        /// persisting the document in the database. 
        /// You can override this auto Id generation by setting the disableAutomaticIdGeneration parameter on the <see cref="Microsoft.Azure.Documents.Client.DocumentClient"/> instance to true.
        /// This will prevent the SDK from generating new Ids. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the body of the script for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the trigger.</value>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public string Body { get; set; }

        [JsonIgnore]
        public CosmosScriptType? Type { get; set; }

        /// <summary>
        /// Get or set the type of the trigger for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the trigger.</value>
        /// <seealso cref="TriggerType"/>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.TriggerType)]
        internal TriggerType TriggerType { get; set; }

        /// <summary>
        /// Gets or sets the operation the trigger is associated with for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The operation the trigger is associated with.</value>
        /// <seealso cref="TriggerOperation"/>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.TriggerOperation)]
        internal TriggerOperation TriggerOperation { get; set; }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.ETag)]
        public virtual string ETag { get; protected internal set; }

        public bool ShouldSerializeTriggerType()
        {
            if (Type.Equals(CosmosScriptType.PreTrigger) || Type.Equals(CosmosScriptType.PostTrigger))
            {
                if (Type.Equals(CosmosScriptType.PreTrigger))
                {
                    this.TriggerType = TriggerType.Pre;
                }
                if (Type.Equals(CosmosScriptType.PostTrigger))
                {
                    this.TriggerType = TriggerType.Post;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ShouldSerializeTriggerOperation()
        {
            if (Type.Equals(CosmosScriptType.PreTrigger) || Type.Equals(CosmosScriptType.PostTrigger))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
