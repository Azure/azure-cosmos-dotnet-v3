//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// <see cref="ChangeFeedPolicy"/> fluent definition.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class ChangeFeedPolicyDefinition
    {
        private readonly ContainerBuilder parent;
        private readonly Action<ChangeFeedPolicy> attachCallback;
        private TimeSpan changeFeedPolicyRetention;

        internal ChangeFeedPolicyDefinition(
            ContainerBuilder parent,
            Action<ChangeFeedPolicy> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Defines the path used to resolve LastWrtierWins resolution mode <see cref="ConflictResolutionPolicy"/>.
        /// </summary>
        /// <param name="retention"> Indicates for how long operation logs have to be retained. <see cref="ChangeFeedPolicy.FullFidelityRetention"/>.</param>
        /// <returns>An instance of the current <see cref="ChangeFeedPolicyDefinition"/>.</returns>
        public ChangeFeedPolicyDefinition WithFullFidelityRetention(TimeSpan retention)
        {
            this.changeFeedPolicyRetention = retention;

            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            ChangeFeedPolicy resolutionPolicy = new ChangeFeedPolicy
            {
                FullFidelityRetention = this.changeFeedPolicyRetention
            };

            this.attachCallback(resolutionPolicy);
            return this.parent;
        }
    }
}
