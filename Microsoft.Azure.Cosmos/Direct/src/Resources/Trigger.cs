//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a trigger in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks> 
    /// Azure Cosmos DB supports pre and post triggers written in JavaScript to be executed on creates, updates and deletes. 
    /// For additional details, refer to the server-side JavaScript API documentation.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Trigger : Resource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Trigger"/> class for the Azure Cosmos DB service.
        /// </summary>
        public Trigger()
        {
        }

        /// <summary>
        /// Gets or sets the body of the trigger for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the trigger.</value>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public string Body
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Body);
            }
            set
            {
                base.SetValue(Constants.Properties.Body, value);
            }
        }

        /// <summary>
        /// Get or set the type of the trigger for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the trigger.</value>
        /// <seealso cref="TriggerType"/>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.TriggerType)]
        public TriggerType TriggerType
        {
            get
            {
                return base.GetValue<TriggerType>(Constants.Properties.TriggerType, TriggerType.Pre);
            }
            set
            {
                base.SetValue(Constants.Properties.TriggerType, value.ToString());
            }
        }

        /// <summary>
        /// Gets or sets the operation the trigger is associated with for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The operation the trigger is associated with.</value>
        /// <seealso cref="TriggerOperation"/>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.TriggerOperation)]
        public TriggerOperation TriggerOperation
        {
            get
            {
                return base.GetValue<TriggerOperation>(Constants.Properties.TriggerOperation, TriggerOperation.All);
            }
            set
            {
                base.SetValue(Constants.Properties.TriggerOperation, value.ToString());
            }
        } 
    }
}
