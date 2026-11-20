//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System;

    internal sealed class SecondaryIndexMetadataInvalidator
    {
        private volatile ISecondaryIndexMetadataInvalidationTarget target;

        public bool HasTarget => this.target != null;

        public void Register(ISecondaryIndexMetadataInvalidationTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            this.target = target;
        }

        public void Invalidate(string sourceCollectionRid)
        {
            if (sourceCollectionRid == null)
            {
                throw new ArgumentNullException(nameof(sourceCollectionRid));
            }

            this.target?.Invalidate(sourceCollectionRid);
        }
    }
}
