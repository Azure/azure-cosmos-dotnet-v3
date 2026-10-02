//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the embedding source settings for the vector embedding policy.
    /// </summary>
    internal sealed class EmbeddingSource : JsonSerializable
    {
        private Collection<string> sourcePaths;

        public EmbeddingSource() 
        {
        }

        /// <summary>
        /// Gets or sets the source paths of the embedding source.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SourcePaths)]
        public Collection<string> SourcePaths
        {
            get
            {
                if (this.sourcePaths == null)
                {
                    this.sourcePaths = base.GetValue<Collection<string>>(Constants.Properties.SourcePaths);
                    if (this.sourcePaths == null)
                    {
                        this.sourcePaths = new Collection<string>();
                    }
                }

                return this.sourcePaths;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "SourcePaths"));
                }

                this.sourcePaths = value;
                base.SetValue(Constants.Properties.SourcePaths, this.sourcePaths);
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

            return ((this.SourcePaths == null && other.SourcePaths == null) || 
                    (this.SourcePaths != null && other.SourcePaths != null && Enumerable.SequenceEqual(this.SourcePaths, other.SourcePaths))) &&
                   this.AuthType == other.AuthType &&
                   this.DeploymentName == other.DeploymentName &&
                   this.Endpoint == other.Endpoint &&
                   this.ModelName == other.ModelName;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 1265339359;

            foreach (string sourcePath in this.SourcePaths)
            {
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(sourcePath);
            }

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
