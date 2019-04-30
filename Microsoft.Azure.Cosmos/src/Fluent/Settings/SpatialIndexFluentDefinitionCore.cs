//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
using System;

namespace Microsoft.Azure.Cosmos.Fluent
{
    internal sealed class SpatialIndexFluentDefinitionCore : SpatialIndexFluentDefinition
    {
        private readonly SpatialSpec spatialSpec = new SpatialSpec();
        private readonly IndexingPolicyFluentDefinition parent;
        private readonly Action<SpatialSpec> attachCallback;

        public SpatialIndexFluentDefinitionCore(
            IndexingPolicyFluentDefinition parent,
            Action<SpatialSpec> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
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
            this.attachCallback(this.spatialSpec);
            return this.parent;
        }
    }
}
