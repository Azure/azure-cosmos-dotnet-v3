//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    internal sealed class CompositeIndexFluentDefinitionCore : CompositeIndexFluentDefinition
    {
        private readonly Collection<CompositePath> compositePaths = new Collection<CompositePath>();
        private readonly IndexingPolicyFluentDefinition parent;
        private readonly Action<Collection<CompositePath>> attachCallback;

        public CompositeIndexFluentDefinitionCore(
            IndexingPolicyFluentDefinition parent,
            Action<Collection<CompositePath>> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        public override CompositeIndexFluentDefinition Path(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.compositePaths.Add(new CompositePath() { Path = path });
            return this;
        }

        public override CompositeIndexFluentDefinition Path(
            string path, 
            CompositePathSortOrder sortOrder)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.compositePaths.Add(new CompositePath() { Path = path, Order = sortOrder });
            return this;
        }

        public override IndexingPolicyFluentDefinition Attach()
        {
            this.attachCallback(this.compositePaths);
            return this.parent;
        }
    }
}
