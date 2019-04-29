//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
using System;

namespace Microsoft.Azure.Cosmos.Fluent
{
    internal sealed class SpatialIndexFluentDefinitionCore : SpatialIndexFluentDefinition
    {
        private readonly SpatialSpec spatialSpec = new SpatialSpec();
        private readonly IndexingPolicyFluentDefinitionCore parent;

        public SpatialIndexFluentDefinitionCore(IndexingPolicyFluentDefinitionCore parent)
        {
            this.parent = parent;
        }

        public override SpatialIndexFluentDefinition WithPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.spatialSpec.Path = path;
            return this;
        }

        public override SpatialIndexFluentDefinition WithPath(
            string path, 
            params SpatialType[] spatialTypes)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (spatialTypes == null)
            {
                throw new ArgumentNullException(nameof(spatialTypes));
            }


            this.spatialSpec.Path = path;

            foreach (SpatialType spatialType in spatialTypes)
            {
                this.spatialSpec.SpatialTypes.Add(spatialType);
            }

            return this;
        }

        public override IndexingPolicyFluentDefinition Attach()
        {
            this.parent.WithSpatialIndex(this.spatialSpec);
            return this.parent;
        }
    }
}
