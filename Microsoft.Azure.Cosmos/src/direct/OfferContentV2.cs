//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.ObjectModel;

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
        private ThroughputDistributionPolicyType? throughputDistributionPolicy;
        private Collection<ThroughputBucket> throughputBuckets;
        private int? offerTargetThroughput;
        private int? partitionCount;
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
        internal OfferContentV2(
            OfferContentV2 content,
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
                this.ThroughputDistributionPolicy = content.ThroughputDistributionPolicy;

                Collection<ThroughputBucket> throughputBuckets = content.ThroughputBuckets;
                if (throughputBuckets != null)
                {
                    this.ThroughputBuckets = throughputBuckets;
                }

                int? offerTargetThroughput = content.OfferTargetThroughput;
                if (offerTargetThroughput.HasValue)
                {
                    this.OfferTargetThroughput = offerTargetThroughput.Value;
                }

                int? partitionCount = content.PartitionCount;
                if (partitionCount.HasValue)
                {
                    this.PartitionCount = partitionCount.Value;
                }
            }
#endif
        }

        /// <summary>
        /// internal constructor that takes offer throughput, RUPM is enabled/disabled, BgTaskMaxAllowedThroughputPercent  and a reference offer content
        /// </summary>
        internal OfferContentV2(
            OfferContentV2 content,
            int offerThroughput,
            bool? offerEnableRUPerMinuteThroughput,
            double? bgTaskMaxAllowedThroughputPercent)
        {
            this.OfferThroughput = offerThroughput;
            this.OfferIsRUPerMinuteThroughputEnabled = offerEnableRUPerMinuteThroughput;

            if (bgTaskMaxAllowedThroughputPercent != null)
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
                this.ThroughputDistributionPolicy = content.ThroughputDistributionPolicy;

                Collection<ThroughputBucket> throughputBuckets = content.ThroughputBuckets;
                if (throughputBuckets != null)
                {
                    this.ThroughputBuckets = throughputBuckets;
                }

                int? offerTargetThroughput = content.OfferTargetThroughput;
                if (offerTargetThroughput.HasValue)
                {
                    this.OfferTargetThroughput = offerTargetThroughput.Value;
                }

                int? partitionCount = content.PartitionCount;
                if (partitionCount.HasValue)
                {
                    this.PartitionCount = partitionCount.Value;
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
        /// Constructor accepting autopilot settings and a reference offer content
        /// </summary>
        /// <param name="offerAutopilotSettings">offer autopilot settings</param>
        internal OfferContentV2(OfferContentV2 content, AutopilotSettings offerAutopilotSettings)
        {
            if (content != null)
            {
                Collection<ThroughputBucket> throughputBuckets = content.ThroughputBuckets;
                if (throughputBuckets != null)
                {
                    this.ThroughputBuckets = throughputBuckets;
                }
            }

            if (offerAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(offerAutopilotSettings);
            }
        }

        /// <summary>
        /// Constructor accepting autopilot settings, throughput buckets and a reference offer content
        /// </summary>
        /// <param name="offerAutopilotSettings">offer autopilot settings</param>
        /// <param name="throughputBuckets">offer autopilot settings</param>
        internal OfferContentV2(OfferContentV2 content, AutopilotSettings offerAutopilotSettings, Collection<ThroughputBucket> throughputBuckets)
        {
            if (offerAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(offerAutopilotSettings);
            }

            if (throughputBuckets != null)
            {
                this.ThroughputBuckets = throughputBuckets;
            }
        }

        /// <summary>
        /// Constructor accepting autopilot settings and throughput buckets.
        /// </summary>
        /// <param name="offerAutopilotSettings">offer autopilot settings</param>
        /// <param name="throughputBuckets">throughput buckets</param>
        internal OfferContentV2(
            AutopilotSettings offerAutopilotSettings,
            Collection<ThroughputBucket> throughputBuckets)
        {
            if (offerAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(offerAutopilotSettings);
            }

            if(throughputBuckets != null)
            {
                this.ThroughputBuckets = throughputBuckets;
            }
        }

        /// <summary>
        /// Constructor accepting autopilot settings, bgTaskMaxAllowedThroughputPercent, throughput distribution policy and throughput buckets
        /// </summary>
        /// <param name="offerAutopilotSettings">offer autopilot settings</param>
        /// <param name="bgTaskMaxAllowedThroughputPercent">offer bg-task percentage settings</param>
        /// <param name="throughputDistributionPolicy">throughput distribution policy</param>
        /// <param name="throughputBuckets">throughput buckets</param>
        internal OfferContentV2(
            AutopilotSettings offerAutopilotSettings,
            double? bgTaskMaxAllowedThroughputPercent,
            ThroughputDistributionPolicyType? throughputDistributionPolicy,
            Collection<ThroughputBucket> throughputBuckets)
        {
            if (offerAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(offerAutopilotSettings);
            }

            if (bgTaskMaxAllowedThroughputPercent != null)
            {
                this.BackgroundTaskMaxAllowedThroughputPercent = bgTaskMaxAllowedThroughputPercent;
            }

            if (throughputDistributionPolicy != null)
            {
                this.ThroughputDistributionPolicy = throughputDistributionPolicy;
            }

            if (throughputBuckets != null)
            {
                this.ThroughputBuckets = throughputBuckets;
            }
        }

        /// <summary>
        /// Internal constructor accepting offer throughput, autopilot settings, minimum throughput parameters,
        /// throughput distribution policy and throughput buckets
        /// </summary>
        internal OfferContentV2(
            OfferContentV2 contentV2,
            bool? offerEnableRUPerMinuteThroughput,
            bool? offerIsAutoScaleV1Enabled,
            AutopilotSettings autopilotSettings,
            OfferMinimumThroughputParameters minThroughputParameters,
            ThroughputDistributionPolicyType? throughputDistributionPolicy,
            Collection<ThroughputBucket> throughputBuckets)
        {
            this.OfferThroughput = contentV2.OfferThroughput;
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

            if (throughputDistributionPolicy != null)
            {
                this.ThroughputDistributionPolicy = throughputDistributionPolicy;
            }

            if (throughputBuckets != null)
            {
                this.ThroughputBuckets = throughputBuckets;
            }

            int? offerTargetThroughput = contentV2.OfferTargetThroughput;
            if (offerTargetThroughput.HasValue)
            {
                this.OfferTargetThroughput = offerTargetThroughput.Value;
            }

            int? partitionCount = contentV2.PartitionCount;
            if (partitionCount.HasValue)
            {
                this.PartitionCount = partitionCount.Value;
            }
        }

        /// <summary>
        /// Copy constructor with optional overrides for all properties except OfferThroughput.
        /// </summary>
        internal OfferContentV2(
            OfferContentV2 source,
            bool? offerIsRUPerMinuteThroughputEnabled = null,
            bool? offerIsAutoScaleEnabled = null,
            AutopilotSettings offerAutopilotSettings = null,
            OfferMinimumThroughputParameters offerMinimumThroughputParameters = null,
            double? backgroundTaskMaxAllowedThroughputPercent = null,
            ThroughputDistributionPolicyType? throughputDistributionPolicy = null,
            Collection<ThroughputBucket> throughputBuckets = null,
            Collection<PhysicalPartitionThroughputInfo> physicalPartitionThroughputInfo = null,
            int? offerTargetThroughput = null,
            int? partitionCount = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // Throughput is always copied from source
            this.OfferThroughput = source.OfferThroughput;

            this.OfferIsRUPerMinuteThroughputEnabled = offerIsRUPerMinuteThroughputEnabled ?? source.OfferIsRUPerMinuteThroughputEnabled;
            this.OfferIsAutoScaleEnabled = offerIsAutoScaleEnabled ?? source.OfferIsAutoScaleEnabled;

            if (offerAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(offerAutopilotSettings);
            }
            else if (source.OfferAutopilotSettings != null)
            {
                this.OfferAutopilotSettings = new AutopilotSettings(source.OfferAutopilotSettings);
            }

            this.OfferMinimumThroughputParameters = offerMinimumThroughputParameters != null ? new OfferMinimumThroughputParameters(offerMinimumThroughputParameters) : (source.OfferMinimumThroughputParameters != null ? new OfferMinimumThroughputParameters(source.OfferMinimumThroughputParameters) : null);
            this.BackgroundTaskMaxAllowedThroughputPercent = backgroundTaskMaxAllowedThroughputPercent ?? source.BackgroundTaskMaxAllowedThroughputPercent;
            this.ThroughputDistributionPolicy = throughputDistributionPolicy ?? source.ThroughputDistributionPolicy;
            this.ThroughputBuckets = throughputBuckets ?? source.ThroughputBuckets;
            this.PhysicalPartitionThroughputInfo = physicalPartitionThroughputInfo ?? source.PhysicalPartitionThroughputInfo;
            this.OfferTargetThroughput = offerTargetThroughput ?? source.OfferTargetThroughput;
            this.PartitionCount = partitionCount ?? source.PartitionCount;
        }

        /// <summary>
        /// Internal constructor accepting offer throughput, autopilot settings, minimum throughput parameters, bg task throughput percent,
        /// throughput distribution policy, throughput buckets, target throughput, and partition count.
        /// </summary>
        internal OfferContentV2(
            int offerThroughput,
            bool? offerEnableRUPerMinuteThroughput,
            bool? offerIsAutoScaleV1Enabled,
            AutopilotSettings autopilotSettings,
            OfferMinimumThroughputParameters minThroughputParameters,
            double? bgTaskMaxAllowedThroughputPercent,
            ThroughputDistributionPolicyType? throughputDistributionPolicy,
            Collection<ThroughputBucket> throughputBuckets,
            int? offerTargetThroughput = null,
            int? partitionCount = null)
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

            if (bgTaskMaxAllowedThroughputPercent != null)
            {
                this.BackgroundTaskMaxAllowedThroughputPercent = bgTaskMaxAllowedThroughputPercent;
            }

            if (throughputDistributionPolicy != null)
            {
                this.ThroughputDistributionPolicy = throughputDistributionPolicy;
            }

            if (throughputBuckets != null)
            {
                this.ThroughputBuckets = throughputBuckets;
            }

            if (offerTargetThroughput.HasValue)
            {
                this.OfferTargetThroughput = offerTargetThroughput.Value;
            }

            if (partitionCount.HasValue)
            {
                this.PartitionCount = partitionCount.Value;
            }
        }

        internal OfferContentV2(
            OfferContentV2 content,
            Collection<PhysicalPartitionThroughputInfo> physicalPartitionThroughputInfo)
        {
            this.PhysicalPartitionThroughputInfo = physicalPartitionThroughputInfo;

            // Copy autopilot GA settings.
            // Note that we don't copy auto scale V1 settings as it is not meant to be made public.
            if (content != null)
            {
                AutopilotSettings autopilotSettings = content.OfferAutopilotSettings;
                if (autopilotSettings != null)
                {
                    this.OfferAutopilotSettings = new AutopilotSettings(autopilotSettings);
                }

                Collection<ThroughputBucket> throughputBuckets = content.ThroughputBuckets;
                if (throughputBuckets != null)
                {
                    this.ThroughputBuckets = throughputBuckets;
                }

                int? offerTargetThroughput = content.OfferTargetThroughput;
                if (offerTargetThroughput.HasValue)
                {
                    this.OfferTargetThroughput = offerTargetThroughput.Value;
                }
                
                int? partitionCount = content.PartitionCount;
                if (partitionCount.HasValue)
                {
                    this.PartitionCount = partitionCount.Value;
                }

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
            if (this.OfferAutopilotSettings != null)
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
        /// Represents customizable throughput distribution policy
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ThroughputDistributionPolicy, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal ThroughputDistributionPolicyType? ThroughputDistributionPolicy
        {
            get
            {
                if (this.throughputDistributionPolicy == null)
                {
                    this.throughputDistributionPolicy = base.GetValue<ThroughputDistributionPolicyType?>(Constants.Properties.ThroughputDistributionPolicy);
                }

                return this.throughputDistributionPolicy;
            }
            set
            {
                this.throughputDistributionPolicy = value;
                base.SetValue(Constants.Properties.ThroughputDistributionPolicy, this.throughputDistributionPolicy);
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
        /// Don't cache this property like we did for CollectionThroughputInfo and OfferMinimumThroughputParameters
        /// as it would result in not deserializing autoscale settings correctly.
        /// </remark>
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AutopilotSettings, DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
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
            set
            {
                this.physicalPartitionThroughputInfo = value;
                base.SetObjectCollection<PhysicalPartitionThroughputInfo>(Constants.Properties.PhysicalPartitionThroughputInfo, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.ThroughputBuckets, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal Collection<ThroughputBucket> ThroughputBuckets
        {
            get
            {
                if (this.throughputBuckets == null)
                {
                    this.throughputBuckets = base.GetObjectCollection<ThroughputBucket>(Constants.Properties.ThroughputBuckets);
                }

                return this.throughputBuckets;
            }
            set
            {
                this.throughputBuckets = value;
                base.SetObjectCollection<ThroughputBucket>(Constants.Properties.ThroughputBuckets, value);
            }
        }

        /// <summary>
        /// Represents target max throughput offer should operate at once offer is no longer in pending state.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferTargetThroughput, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal int? OfferTargetThroughput
        {
            get
            {
                if (this.offerTargetThroughput == null)
                {
                    this.offerTargetThroughput = base.GetValue<int?>(Constants.Properties.OfferTargetThroughput);
                }

                return this.offerTargetThroughput;
            }
            private set
            {
                this.offerTargetThroughput = value;
                base.SetValue(Constants.Properties.OfferTargetThroughput, this.offerTargetThroughput);
            }
        }

        /// <summary>
        /// Represents partition count.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.PartitionCount, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal int? PartitionCount
        {
            get
            {
                if (this.partitionCount == null)
                {
                    this.partitionCount = base.GetValue<int?>(Constants.Properties.PartitionCount);
                }

                return this.partitionCount;
            }
            private set
            {
                this.partitionCount = value;
                base.SetValue(Constants.Properties.PartitionCount, this.partitionCount);
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
