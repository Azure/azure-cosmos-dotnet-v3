//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Serialization;
    using SourceProperty = System.String;
    using TargetProperty = System.String;

    /// <summary>
    /// Used by JsonSerializer to resolve a JsonContract for <see cref="ChangeFeedItemChanges{T}"/> that also contains <see cref="ChangeFeedMetadata"/>.
    /// </summary>
    public class CosmosItemChangesContractResolver : DefaultContractResolver
    {
        private readonly Dictionary<TargetProperty, SourceProperty> PropertyMappings = new Dictionary<TargetProperty, SourceProperty>
            {
                { nameof(ChangeFeedMetadata.CurrentLogSequenceNumber), "lsn" },
                { nameof(ChangeFeedMetadata.PreviousLogSequenceNumber), "previousImageLSN" },
                { nameof(ChangeFeedMetadata.ConflictResolutionTimestamp), "crts" },
            };

        /// <summary>
        /// Resolves the name of the property.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns><see cref="string"/></returns>
        protected override string ResolvePropertyName(string propertyName)
        {
            bool resolved = this.PropertyMappings.TryGetValue(propertyName, out TargetProperty resolvedName);
            return resolved ? resolvedName : base.ResolvePropertyName(propertyName);
        }

        /// <summary>
        /// Determines which contract type is created for the given type.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns><see cref="JsonContract"/></returns>
        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract contract = base.CreateContract(objectType);

            if (objectType == typeof(DateTime))
            {
                contract.Converter = new UnixDateTimeConverter();
            }

            return contract;
        }
    }
}
