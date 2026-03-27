//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the embedding source settings for the vector embedding policy.
    /// </summary>
    internal sealed class EmbeddingSource : JsonSerializable
    {
        public EmbeddingSource() 
        {
        }

        /// <summary>
        /// Gets or sets the source path of the embedding source.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SourcePath)]
        public string SourcePath
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.SourcePath);
            }
            set
            {
                base.SetValue(Constants.Properties.SourcePath, value);
            }
        }

        /// <summary>
        /// Gets or sets the auth type <see cref="Cosmos.AuthType"/> of the embedding source.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AuthType)]
        public AuthenticationType AuthType
        {
            get
            {
                AuthenticationType result = default;
                string strValue = base.GetValue<string>(Constants.Properties.AuthType);
                if (!string.IsNullOrEmpty(strValue))
                {
                    Enum.TryParse(strValue, true, out result);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.AuthType, value);
            }
        }

        /// <summary>
        /// Gets or sets the deployment name of the embedding source.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.DeploymentName)]
        public string DeploymentName
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.DeploymentName);
            }
            set
            {
                base.SetValue(Constants.Properties.DeploymentName, value);
            }
        }

        /// <summary>
        /// Gets or sets the endpoint of the embedding source.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Endpoint)]
        public string Endpoint
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Endpoint);
            }
            set
            {
                base.SetValue(Constants.Properties.Endpoint, value);
            }
        }

        /// <summary>
        /// Gets or sets the model name of the embedding source.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ModelName)]
        public string ModelName
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ModelName);
            }
            set
            {
                base.SetValue(Constants.Properties.ModelName, value);
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is EmbeddingSource other))
            {
                return false;
            }

            return this.SourcePath == other.SourcePath &&
                   this.AuthType == other.AuthType &&
                   this.DeploymentName == other.DeploymentName &&
                   this.Endpoint == other.Endpoint &&
                   this.ModelName == other.ModelName;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 1265339359;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.SourcePath);
            hashCode = (hashCode * -1521134295) + this.AuthType.GetHashCode();
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.DeploymentName);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Endpoint);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.ModelName);
            return hashCode;
        }

        internal override void Validate()
        {
            base.Validate();
            Helpers.ValidateEnumProperties<AuthenticationType>(this.AuthType);
        }

        /// <summary>
        /// Defines the type of Authentication while calling embedding provider api in the Azure Cosmos DB's Embedding Generator service.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum AuthenticationType : short
        {
            /// <summary>
            /// Represent unknown authentication.
            /// </summary>
            [EnumMember(Value = "Unknown")]
            Unknown,

            /// <summary>
            /// Represent Entra authentication type.
            /// </summary>
            [EnumMember(Value = "Entra")]
            Entra,

            /// <summary>
            /// Represent Key based authentication type.
            /// </summary>
            [EnumMember(Value = "ApiKey")]
            ApiKey,
        }
    }
}
