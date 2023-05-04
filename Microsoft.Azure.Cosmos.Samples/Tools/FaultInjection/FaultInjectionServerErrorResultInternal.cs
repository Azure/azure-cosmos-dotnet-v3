//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Documents;

    public class FaultInjectionServerErrorResultInternal
    {
        private readonly FaultInjectionServerErrorType serverErrorType;
        private readonly int times;
        private readonly TimeSpan delay;

        public FaultInjectionServerErrorResultInternal(
            FaultInjectionServerErrorType serverErrorType, 
            int times, 
            TimeSpan delay)
        {
            this.serverErrorType = serverErrorType;
            this.times = times;
            this.delay = delay;
        }

        public FaultInjectionServerErrorType GetServerErrorType()
        {
            return this.serverErrorType;
        }

        public int GetTimes()
        {
            return this.times;
        }

        public TimeSpan GetDelay()
        {
            return this.delay;
        }

        public bool IsApplicable(string ruleId, DocumentServiceRequest request)
        {
            return this.times == null || request.FaultInjectionRequestContext.getFaultInjectionRuleApplyCount(ruleId) < this.times;
        }

    }
}
