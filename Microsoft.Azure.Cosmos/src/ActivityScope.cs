//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;

    internal sealed class ActivityScope : IDisposable
    {
        private readonly Guid ambientActivityId;

        public ActivityScope(Guid activityId)
        {
            this.ambientActivityId = Trace.CorrelationManager.ActivityId;
            Trace.CorrelationManager.ActivityId = activityId;
        }

        public void Dispose()
        {
            Trace.CorrelationManager.ActivityId = this.ambientActivityId;
        }

        public static ActivityScope CreateIfDefaultActivityId()
        {
            if (Trace.CorrelationManager.ActivityId == Guid.Empty)
            {
                return new ActivityScope(Guid.NewGuid());
            }

            return null;
        }
    }
}
