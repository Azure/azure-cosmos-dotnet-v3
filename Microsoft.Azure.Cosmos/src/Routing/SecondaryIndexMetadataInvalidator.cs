//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System;
    using System.Threading;

    internal sealed class SecondaryIndexMetadataInvalidator
    {
        private ISecondaryIndexMetadataInvalidationTarget target;

        public bool HasTarget => Volatile.Read(ref this.target) != null;

        public void Register(ISecondaryIndexMetadataInvalidationTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Volatile.Write(ref this.target, target);
        }

        public void Invalidate(string sourceCollectionRid)
        {
            if (sourceCollectionRid != null)
            {
                Volatile.Read(ref this.target)?.Invalidate(sourceCollectionRid);
            }
        }
    }
}
