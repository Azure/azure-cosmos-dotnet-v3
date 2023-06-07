//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.FaultInjection;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;

    public class FaultInjectionConditionInternal
    {
        private readonly string containerResourceId;

        private readonly List<IFaultInjectionConditionValidator> validators;

        private OperationType operationType;
        private List<Uri> regionEndpoints;
        private List<Uri> physicalAddresses;
        public FaultInjectionConditionInternal(string containerResourceId)
        {
            this.containerResourceId = containerResourceId;
            this.validators = new List<IFaultInjectionConditionValidator>
            {
                new ContainerValidator(this.containerResourceId)
            };
        }

        public OperationType GetOperationType()
        {
            return this.operationType;
        }

        public void SetOperationType(OperationType operationType)
        {
            this.operationType = operationType;
        }

        public void SetRegionEndpoints(List<Uri> regionEndpoints)
        {
            this.regionEndpoints = regionEndpoints;
            if (this.regionEndpoints != null)
            {
                this.validators.Add(new RegionEndpointValidator(this.regionEndpoints));
            }
        }

        public List<Uri> GetRegionEndpoints()
        {
            return this.regionEndpoints;
        }

        public List<Uri> GetPhysicalAddresses()
        {
            return this.physicalAddresses;
        }

        public void SetAddresses(List<Uri> physicalAddresses)
        {
            this.physicalAddresses = physicalAddresses;
            if (this.physicalAddresses != null && physicalAddresses.Count > 0)
            {
                this.validators.Add(new AddressValidator(this.physicalAddresses));
            }
        }

        public bool IsApplicable(string ruleId, ChannelCallArguments args)
        {
            foreach (IFaultInjectionConditionValidator validator in this.validators)
            {
                if (!validator.IsApplicable(ruleId, args))
                {
                    return false;
                }
            }

            return true;
        }
        private interface IFaultInjectionConditionValidator
        {
            public bool IsApplicable(string ruleID, ChannelCallArguments args);
        }

        private class RegionEndpointValidator : IFaultInjectionConditionValidator
        {
            private readonly List<Uri> regionEndpoints;

            public RegionEndpointValidator(List<Uri> regionEndpoints)
            {
                this.regionEndpoints = regionEndpoints;
            }
            public bool IsApplicable(string ruleID, ChannelCallArguments args)
            {
                bool isApplicable = this.regionEndpoints.Contains(args.FaultInjectionRequestContext.GetLocationEndpointToRoute());
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.PreparedCall.RequestId,
                        String.Format(
                            "{0} [RegionEndpoint mistmatch: Excpected {1}, Actual {2}]",
                            ruleID,
                            this.regionEndpoints.Select(i => i.ToString()).ToList(),
                            args.FaultInjectionRequestContext.GetLocationEndpointToRoute()));
                }

                return isApplicable;
            }
        }

        private class OperationTypeValidator : IFaultInjectionConditionValidator
        {
            private readonly OperationType operationType;

            public OperationTypeValidator(OperationType operationType)
            {
                this.operationType = operationType;
            }
            public bool IsApplicable(string ruleID, ChannelCallArguments args)
            {
                bool isApplicable = args.OperationType == this.operationType;
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.PreparedCall.RequestId,
                        String.Format(
                            "{0} [OperationType mistmatch: Excpected {1}, Actual {2}]",
                            ruleID,
                            this.operationType,
                            args.OperationType));
                }

                return isApplicable;
            }
        }

        private class ContainerValidator : IFaultInjectionConditionValidator
        {
            private readonly string containerResourceId;

            public ContainerValidator(string containerResourceId)
            {
                this.containerResourceId = containerResourceId;
            }
            public bool IsApplicable(string ruleID, ChannelCallArguments args)
            {
                bool isApplicable = String.Equals(this.containerResourceId, args.ResolvedCollectionRid);
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.PreparedCall.RequestId,
                        String.Format(
                            "{0} [ContainerRid mistmatch: Excpected {1}, Actual {2}]",
                            ruleID,
                            this.containerResourceId,
                            args.ResolvedCollectionRid));
                }

                return isApplicable;
            }
        }

        private class AddressValidator : IFaultInjectionConditionValidator
        {
            private readonly List<Uri> addresses;

            public AddressValidator(List<Uri> addresses)
            {
                this.addresses = addresses;
            }
            public bool IsApplicable(string ruleID, ChannelCallArguments args)
            {
                bool isApplicable = this.addresses.Exists(uri => args.PreparedCall.Uri.ToString().StartsWith(uri.ToString()));
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.PreparedCall.RequestId,
                        String.Format(
                            "{0} [Addresses mistmatch: Excpected {1}, Actual {2}]",
                            ruleID,
                            string.Join(",", this.addresses.Select(i => i.ToString()).ToList()),
                            args.PreparedCall.Uri.ToString()));
                }

                return isApplicable;
            }
        }
    }
}
