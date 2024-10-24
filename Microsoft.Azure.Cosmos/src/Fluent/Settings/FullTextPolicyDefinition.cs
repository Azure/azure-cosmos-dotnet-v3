//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// <see cref="FullTextPolicyDefinition"/> fluent definition.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class FullTextPolicyDefinition
    {
        private readonly ContainerBuilder parent;
        private readonly Action<FullTextPolicy> attachCallback;
        private readonly string defaultLanguage;
        private readonly Collection<FullTextPath> fullTextPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTextPolicyDefinition"/> class.
        /// </summary>
        /// <param name="parent">The original instance of <see cref="ContainerBuilder"/>.</param>
        /// <param name="defaultLanguage">A string indicating the default language for the inexing policy definition.</param>
        /// <param name="fullTextPaths">List of fullTextPaths to include in the policy definition.</param>
        /// <param name="attachCallback">A callback delegate to be used at a later point of time.</param>
        public FullTextPolicyDefinition(
            ContainerBuilder parent,
            string defaultLanguage,
            Collection<FullTextPath> fullTextPaths,
            Action<FullTextPolicy> attachCallback)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.attachCallback = attachCallback ?? throw new ArgumentNullException(nameof(attachCallback));
            this.fullTextPaths = fullTextPaths;
            this.defaultLanguage = defaultLanguage;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            FullTextPolicy fullTextPolicy = new ()
            {
                DefaultLanguage = this.defaultLanguage,
                FullTextPaths = this.fullTextPaths
            };

            this.attachCallback(fullTextPolicy);
            return this.parent;
        }
    }
}
