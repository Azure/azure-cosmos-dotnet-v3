//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// <see cref="ConflictResolutionPolicy"/> fluent definition.
    /// </summary>
    public class ConflictResolutionDefinition
    {
        private readonly ContainerBuilder parent;
        private readonly Action<ConflictResolutionPolicy> attachCallback;
        private string conflictResolutionPath;
        private string conflictResolutionProcedure;

        internal ConflictResolutionDefinition(
            ContainerBuilder parent,
            Action<ConflictResolutionPolicy> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Defines the path used to resolve LastWrtierWins resolution mode <see cref="ConflictResolutionPolicy"/>.
        /// </summary>
        /// <param name="conflictResolutionPath"> sets the path which is present in each item in the Azure Cosmos DB service for last writer wins conflict-resolution. <see cref="ConflictResolutionPolicy.ResolutionPath"/>.</param>
        /// <returns>An instance of the current <see cref="UniqueKeyDefinition"/>.</returns>
        public ConflictResolutionDefinition WithLastWriterWinsResolution(string conflictResolutionPath)
        {
            if (string.IsNullOrEmpty(conflictResolutionPath))
            {
                throw new ArgumentNullException(nameof(conflictResolutionPath));
            }

            this.conflictResolutionPath = conflictResolutionPath;

            return this;
        }

        /// <summary>
        /// Defines the stored procedure to be used as custom conflict resolution mode <see cref="ConflictResolutionPolicy"/>.
        /// </summary>
        /// <param name="conflictResolutionProcedure"> sets the stored procedure to be used for conflict-resolution. <see cref="ConflictResolutionPolicy.ResolutionProcedure"/>.</param>
        /// <returns>An instance of the current <see cref="UniqueKeyDefinition"/>.</returns>
        public ConflictResolutionDefinition WithCustomStoredProcedureResolution(string conflictResolutionProcedure)
        {
            if (string.IsNullOrEmpty(conflictResolutionProcedure))
            {
                throw new ArgumentNullException(nameof(conflictResolutionProcedure));
            }

            this.conflictResolutionProcedure = conflictResolutionProcedure;

            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            ConflictResolutionPolicy resolutionPolicy = new ConflictResolutionPolicy();
            if (this.conflictResolutionPath != null)
            {
                resolutionPolicy.Mode = ConflictResolutionMode.LastWriterWins;
                resolutionPolicy.ResolutionPath = this.conflictResolutionPath;
            }

            if (this.conflictResolutionProcedure != null)
            {
                resolutionPolicy.Mode = ConflictResolutionMode.Custom;
                resolutionPolicy.ResolutionProcedure = this.conflictResolutionProcedure;
            }

            this.attachCallback(resolutionPolicy);
            return this.parent;
        }
    }
}
