//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.ObjectModel;

    internal sealed class CompositeIndexFluentDefinitionCore : CompositeIndexFluentDefinition
    {
        private readonly Collection<CompositePath> compositePaths = new Collection<CompositePath>();
        private readonly IndexingPolicyFluentDefinitionCore parent;

        public CompositeIndexFluentDefinitionCore(IndexingPolicyFluentDefinitionCore parent)
        {
            this.parent = parent;
        }

        public override CompositeIndexFluentDefinition Path(string path)
        {
            this.compositePaths.Add(new CompositePath() { Path = path });
            return this;
        }

        public override CompositeIndexFluentDefinition Path(
            string path, 
            CompositePathSortOrder sortOrder)
        {
            this.compositePaths.Add(new CompositePath() { Path = path, Order = sortOrder });
            return this;
        }

        public override IndexingPolicyFluentDefinition Attach()
        {
            this.parent.WithCompositePaths(this.compositePaths);
            return this.parent;
        }
    }
}
