﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal class InAccountRestoreParameters : JsonSerializable
    {
        public InAccountRestoreParameters()
        {
        }

        /// <summary>
        /// Gets or sets the <see cref="InstanceId"/> after triggering the InAccount Restore as part of RestoreParameters
        /// </summary>
        /// <value>
        /// A valid value should have unique InstanceId in restore parameters.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.InstanceId)]
        public string InstanceId
        {
            get { return base.GetValue<string>(Constants.Properties.InstanceId); }
            set { base.SetValue(Constants.Properties.InstanceId, value); }
        }

        /// <summary>
        /// Gets or sets the <see cref="RestoreTimestampInUtc"/> for triggering the InAccount Restore as part of RestoreParameters
        /// </summary>
        /// <value>
        /// A valid value should have a DateTime Value which represents the restore time
        /// </value>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.RestoreTimestampInUtc)]
        public DateTime RestoreTimestampInUtc
        {
            get { return base.GetValue<DateTime>(Constants.Properties.RestoreTimestampInUtc); }
            set { base.SetValue(Constants.Properties.RestoreTimestampInUtc, value); }
        }

        /// <summary>
        /// Gets or sets the <see cref="RestoreSource"/> for triggering the InAccount Restore as part of RestoreParameters
        /// </summary>
        /// <value>
        /// A valid value should have the RestoreSource url with the restorableDatabaseAccount instance id
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.RestoreSource)]
        public string RestoreSource
        {
            get { return base.GetValue<string>(Constants.Properties.RestoreSource); }
            set { base.SetValue(Constants.Properties.RestoreSource, value); }
        }

        /// <summary>
        /// Gets or sets the <see cref="SourceBackupLocation"/> for triggering the InAccount Restore as part of RestoreParameters
        /// </summary>
        /// <value>
        /// A valid value should have location info about the source backup
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.SourceBackupLocation)]
        public string SourceBackupLocation
        {
            get { return base.GetValue<string>(Constants.Properties.SourceBackupLocation); }
            set { base.SetValue(Constants.Properties.SourceBackupLocation, value); }
        }

        /// <summary>
        /// Gets or sets the <see cref="RestoreWithTtlDisabled"/> for triggering the InAccount Restore as part of RestoreParameters
        /// </summary>
        /// <value>
        /// A valid value should be true or false
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.RestoreWithTtlDisabled)]
        public bool RestoreWithTtlDisabled
        {
            get { return base.GetValue<bool>(Constants.Properties.RestoreWithTtlDisabled); }
            set { base.SetValue(Constants.Properties.RestoreWithTtlDisabled, value); }
        }

        internal override void Validate()
        {
            base.Validate();

            if (this.RestoreTimestampInUtc == default)
            {
                throw new BadRequestException($"{Constants.Properties.RestoreTimestampInUtc} is a required input for in account restore request");
            }

            if (string.IsNullOrEmpty(this.RestoreSource))
            {
                throw new BadRequestException($"{Constants.Properties.RestoreSource} is a required input for in account restore request");
            }
        }
    }
}