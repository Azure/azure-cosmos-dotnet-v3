//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents content properties tied to the Standard pricing tier for the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class OfferContentV2 : JsonSerializable
    {
#if !DOCDBCLIENT
        private CollectionThroughputInfo throughputInfo;
        private OfferMinimumThroughputParameters minimumThroughputParameters;
        private Collection<PhysicalPartitionThroughputInfo> physicalPartitionThroughputInfo;
#endif

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// The <see cref="OfferContentV2"/> class 
        /// represents content properties tied to the Standard pricing tier for the Azure Cosmos DB service.
        /// </remarks>
        public OfferContentV2() : this(0)
        {
        }

        /// <summary>
        /// Constructor accepting offer throughput.
        /// </summary>
        /// <remarks>
        /// The <see cref="OfferContentV2"/> class 
        /// represents content properties tied to the Standard pricing tier for the Azure Cosmos DB service.
        /// </remarks>
        public OfferContentV2(int offerThroughput)
        {
            this.OfferThroughput = offerThroughput;
            this.OfferIsRUPerMinuteThroughputEnabled = null;
        }

        /// <summary>
        /// Constructor accepting offer throughput, Request Units(RU)/Minute throughput is enabled or disabled
        /// and auto scale is enabled or disabled.
        /// </summary>
        /// <remarks>
        /// The <see cref="OfferContentV2"/> class 
        /// represents content properties tied to the Standard pricing tier for the Azure Cosmos DB service.
        /// </remarks>
        public OfferContentV2(int offerThroughput, bool? offerEnableRUPerMinuteThroughput)
        {
            this.OfferThroughput = offerThroughput;
            this.OfferIsRUPerMinuteThroughputEnabled = offerEnableRUPerMinuteThroughput;
        }

        /// <summary>
        /// internal constructor that takes offer throughput, RUPM is enabled/disabled and a reference offer content
        /// </summary>
        internal OfferContentV2(OfferContentV2 content,
                                int offerThroughput,
                                bool? offerEnableRUPerMinuteThroughput)
        {
            this.OfferThroughput = offerThroughput;
            this.OfferIsRUPerMinuteThroughputEnabled = offerEnableRUPerMinuteThroughput;

#if !DOCDBCLIENT
            // Copy autopilot GA settings.
            // Note that we don't copy auto scale V1 settings as it is not meant to be made public.
            if (content != null)
            {
                AutopilotSettings autopilotSettings = content.OfferAutopilotSettings;
                if (autopilotSettings != null)
                {
                    this.OfferAutopilotSettings = new AutopilotSettings(autopilotSettings);
                }
            }
#endif
        }

        /// <summary>
        /// internal constructor that takes offer throughput, RUPM is enabled/disabled, BgTaskMaxAllowedThroughputPercent  and a reference offer content
        /// </summary>
        internal OfferContentV2(OfferContentV2 content,
                                int offerThroughput,
                                bool? offerEnableRUPerMinuteThroughput,
                                double? bgTaskMaxAllowedThroughputPercent)
        {
            this.OfferThroughput = offerThroughput;
            this.OfferIsRUPerMinuteThroughputEnabled = offerEnableRUPerMinuteThroughput;

            if(bgTaskMaxAllowedThroughputPercent != null)
            {
                this.BackgroundTaskMaxAllowedThroughputPercent = bgTaskMaxAllowedThroughputPercent;
            }
            
#if !DOCDBCLIENT
            // Copy autopilot GA settings.
            // Note that we don't copy auto scale V1 settings as it is not meant to be made public.
            if (content != null)
            {
                AutopilotSettings autopilotSettings = content.OfferAutopilotSettings;
                if (autopilotSettings != null)
                {
                    this.OfferAutopilotSettings = new AutopilotSettings(autopilotSettings);
                }
            }
#endif
        }

#if !DOCDBCLIENT
        /// <summary>
        /// Constructor accepting autopilot settings.
        /// </summary>
        /// <param name="offerAutopilotSettings">offer autopilot settings</param>
        internal OfferContentV2(AutopilotSettings offerAutopilotSettings)
        {
            if (offerAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(offerAutopilotSettings);
            }
        }

        /// <summary>
        /// Constructor accepting autopilot settings.
        /// </summary>
        /// <param name="offerAutopilotSettings">offer autopilot settings</param>
        /// <param name="bgTaskMaxAllowedThroughputPercent">offer bg-task percentage settings</param>
        internal OfferContentV2(AutopilotSettings offerAutopilotSettings, double? bgTaskMaxAllowedThroughputPercent)
        { 
            if (offerAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(offerAutopilotSettings);
            }

            if(bgTaskMaxAllowedThroughputPercent != null)
            {
                this.BackgroundTaskMaxAllowedThroughputPercent = bgTaskMaxAllowedThroughputPercent;
            }
        }

        /// <summary>
        /// Internal constructor accepting offer throughput, autopilot settings and minimum throughput parameters
        /// </summary>
        internal OfferContentV2(int offerThroughput,
                                bool? offerEnableRUPerMinuteThroughput,
                                bool? offerIsAutoScaleV1Enabled,
                                AutopilotSettings autopilotSettings,
                                OfferMinimumThroughputParameters minThroughputParameters)
        {
            this.OfferThroughput = offerThroughput;
            this.OfferIsRUPerMinuteThroughputEnabled = offerEnableRUPerMinuteThroughput;
            this.OfferIsAutoScaleEnabled = offerIsAutoScaleV1Enabled;

            if (autopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(autopilotSettings);
            }

            if (minThroughputParameters != null)
            {
                this.OfferMinimumThroughputParameters = new OfferMinimumThroughputParameters(minThroughputParameters);
            }
        }

        /// <summary>
        /// Internal constructor accepting offer throughput, autopilot settings, minimum throughput parameters, bg task throughput percent
        /// </summary>
        internal OfferContentV2(int offerThroughput,
                                bool? offerEnableRUPerMinuteThroughput,
                                bool? offerIsAutoScaleV1Enabled,
                                AutopilotSettings autopilotSettings,
                                OfferMinimumThroughputParameters minThroughputParameters,
                                double? bgTaskMaxAllowedThroughputPercent)
        {
            this.OfferThroughput = offerThroughput;
            this.OfferIsRUPerMinuteThroughputEnabled = offerEnableRUPerMinuteThroughput;
            this.OfferIsAutoScaleEnabled = offerIsAutoScaleV1Enabled;

            if (autopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(autopilotSettings);
            }

            if (minThroughputParameters != null)
            {
                this.OfferMinimumThroughputParameters = new OfferMinimumThroughputParameters(minThroughputParameters);
            }

            if(bgTaskMaxAllowedThroughputPercent != null)
            {
                this.BackgroundTaskMaxAllowedThroughputPercent = bgTaskMaxAllowedThroughputPercent;
            }
        }
#endif

        /// <summary>
        /// Represents customizable throughput chosen by user for his collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferThroughput, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int OfferThroughput
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.OfferThroughput);
            }
            private set
            {
                base.SetValue(Constants.Properties.OfferThroughput, value);
            }
        }

        /// <summary>
        /// Represents customizable maximum allowed throughput budget in percentage chosen by user to run any
        /// background task(eg. PK Delete, Creating UniqueIndex policy) for the collection in the Azure Cosmos DB service.
        /// In the absence of any background task, the whole throughput is available for use by customer for their workload.
        /// But even in absence of user workload, user background task will not utilize over the allotted percentage of throughput.
        /// We will have default value of BackgroundTaskMaxAllowedThroughputPercent to be 10 percent if user has not explicitly set it.
        /// This helps the background tasks to not starve and at the same time impact on user's workload will be minimal.
        /// User can set the value in range (10,100].
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.BackgroundTaskMaxAllowedThroughputPercent, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal double? BackgroundTaskMaxAllowedThroughputPercent
        {
            get
            {
                return base.GetValue<double?>(Constants.Properties.BackgroundTaskMaxAllowedThroughputPercent);
            }
            private set
            {
                base.SetValue(Constants.Properties.BackgroundTaskMaxAllowedThroughputPercent, value);
            }
        }

        /// <summary>
        /// Represents Request Units(RU)/Minute throughput is enabled/disabled for collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferIsRUPerMinuteThroughputEnabled, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? OfferIsRUPerMinuteThroughputEnabled
        {
            get
            {
                return base.GetValue<bool?>(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled);
            }
            private set
            {
                base.SetValue(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled, value);
            }
        }

        /// <summary>
        /// Validates the property, by calling it, in case of any errors exception is thrown
        /// </summary>
        internal override void Validate()
        {
            base.GetValue<int>(Constants.Properties.OfferThroughput);
            base.GetValue<bool?>(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled);
#if !DOCDBCLIENT
            if(this.OfferAutopilotSettings != null)
            {
                this.OfferAutopilotSettings.Validate();
            }
#endif
            base.GetValue<double?>(Constants.Properties.BackgroundTaskMaxAllowedThroughputPercent);
        }

#if !DOCDBCLIENT
        /// <summary>
        /// Represents auto scale is enabled/disabled for collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferIsAutoScaleEnabled, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal bool? OfferIsAutoScaleEnabled
        {
            get
            {
                return base.GetValue<bool?>(Constants.Properties.OfferIsAutoScaleEnabled);
            }
            set
            {
                base.SetValue(Constants.Properties.OfferIsAutoScaleEnabled, value);
            }
        }

        /// <summary>
        /// Represents timestamp when offer was last replaced by user for collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferLastReplaceTimestamp, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal long? OfferLastReplaceTimestamp
        {
            get
            {
                return base.GetValue<long?>(Constants.Properties.OfferLastReplaceTimestamp);
            }
            set
            {
                base.SetValue(Constants.Properties.OfferLastReplaceTimestamp, value);
            }
        }

        /// <summary>
        /// Represents throughput information relating to the collection that this offer is associated with.
        /// This is an internal attribute populated only for purposes of post-split throughput adjustments.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.CollectionThroughputInfo, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal CollectionThroughputInfo CollectionThroughputInfo
        {
            get
            {
                if (this.throughputInfo == null)
                {
                    this.throughputInfo = base.GetValue<CollectionThroughputInfo>(Constants.Properties.CollectionThroughputInfo);

                    if (this.throughputInfo == null)
                    {
                        this.throughputInfo = new CollectionThroughputInfo();
                    }
                }

                return this.throughputInfo;
            }
        }

        /// <summary>
        /// Represents information relating to the collection/database that this offer is associated with.
        /// This is an internal attribute populated for min RU calculations.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferMinimumThroughputParameters, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal OfferMinimumThroughputParameters OfferMinimumThroughputParameters
        {
            get
            {
                if (this.minimumThroughputParameters == null)
                {
                    this.minimumThroughputParameters = base.GetObject<OfferMinimumThroughputParameters>(Constants.Properties.OfferMinimumThroughputParameters);

                    if (this.minimumThroughputParameters == null)
                    {
                        this.minimumThroughputParameters = new OfferMinimumThroughputParameters();
                    }
                }

                return this.minimumThroughputParameters;
            }
            set
            {
                this.minimumThroughputParameters = value;
                base.SetObject(Constants.Properties.OfferMinimumThroughputParameters, this.minimumThroughputParameters);
            }
        }

        /// <summary>
        /// Represents settings related to auto scale of a collection/database offer.
        /// <remark>
        /// Don't cache this property like we did for CollectionThroughputInfo and OffferMinimumThroughputParameters
        /// as it would result in not deserializing autoscale settings correctly.
        /// </remark>
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AutopilotSettings, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal AutopilotSettings OfferAutopilotSettings
        {
            get
            {
                return base.GetObject<AutopilotSettings>(Constants.Properties.AutopilotSettings); ;
            }
            set
            {
                base.SetObject(Constants.Properties.AutopilotSettings, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.PhysicalPartitionThroughputInfo, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal Collection<PhysicalPartitionThroughputInfo> PhysicalPartitionThroughputInfo
        {
            get
            {
                if (this.physicalPartitionThroughputInfo == null)
                {
                    this.physicalPartitionThroughputInfo = base.GetObjectCollection<PhysicalPartitionThroughputInfo>(Constants.Properties.PhysicalPartitionThroughputInfo);
                }

                return this.physicalPartitionThroughputInfo;
            }
        }

        internal override void OnSave()
        {
            base.OnSave();

            if (this.throughputInfo != null)
            {
                this.throughputInfo.OnSave();
                this.SetObject(Constants.Properties.CollectionThroughputInfo, this.throughputInfo);
            }

            if (this.minimumThroughputParameters != null)
            {
                this.minimumThroughputParameters.OnSave();
                this.SetObject(Constants.Properties.OfferMinimumThroughputParameters, this.minimumThroughputParameters);
            }

            if (this.physicalPartitionThroughputInfo != null)
            {
                this.SetObjectCollection(Constants.Properties.PhysicalPartitionThroughputInfo, this.physicalPartitionThroughputInfo);
            }
        }
#endif
        }
}
