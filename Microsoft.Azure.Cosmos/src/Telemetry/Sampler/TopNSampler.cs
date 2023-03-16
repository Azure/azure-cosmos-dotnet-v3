//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

    /// <summary>
    /// Sampler to select top N unique records and return true/false on the basis of elements already selected.
    /// </summary>
    internal sealed class TopNSampler : ISampler<RequestInfo>
    {
        private readonly int NumberOfRecords;
        private readonly ISet<RequestInfo> TempStorage;
        
        public TopNSampler(int numberofRecords)
        {
            this.NumberOfRecords = numberofRecords;
            
            this.TempStorage = new HashSet<RequestInfo>();
        }
        
        public bool ShouldSample(RequestInfo requestInfo)
        {
            if (requestInfo == null)
            {
                return false;
            }
            
            if (this.TempStorage.Count < this.NumberOfRecords)
            {
                return this.TempStorage.Add(requestInfo);
            }
            else
            {
                bool isAdded = this.TempStorage.Add(requestInfo);
                if (isAdded)
                {
                    this.TempStorage.Remove(requestInfo);
                }
                return !isAdded;
            }
        }
    }
}
