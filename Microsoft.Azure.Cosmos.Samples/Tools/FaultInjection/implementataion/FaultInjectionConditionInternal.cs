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

    internal class FaultInjectionConditionInternal
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

        //Used for connection delay
        public bool IsApplicable(string ruleId, Guid activityId, string callUri, DocumentServiceRequest request)
        {
            foreach (IFaultInjectionConditionValidator validator in this.validators)
            {
                if (validator.GetType() == typeof(RegionEndpointValidator))
                {
                    RegionEndpointValidator regionEndpointValidator = (RegionEndpointValidator)validator;
                    if (!regionEndpointValidator.IsApplicable(ruleId, activityId, request))
                    {
                        return false;
                    }
                }
                else if (validator.GetType() == typeof(OperationTypeValidator))
                {
                    OperationTypeValidator operationTypeValidator = (OperationTypeValidator)validator;
                    if (!operationTypeValidator.IsApplicable(ruleId, activityId, request))
                    {
                        return false;
                    }
                }
                else if (validator.GetType() == typeof(ContainerValidator))
                {
                    ContainerValidator containerValidator = (ContainerValidator)validator;
                    if (!containerValidator.IsApplicable(ruleId, activityId, request))
                    {
                        return false;
                    }
                }
                else if (validator.GetType() == typeof(AddressValidator))
                {
                    AddressValidator addressValidator = (AddressValidator)validator;
                    if (!addressValidator.IsApplicable(ruleId, activityId, callUri, request))
                    {
                        return false;
                    }
                }
                else
                {
                    throw new ArgumentException($"Unknown validator type {validator.GetType()}");
                }
            }

            return true;
        }

        private interface IFaultInjectionConditionValidator
        {
            public bool IsApplicable(string ruleId, ChannelCallArguments args);
        }

        private class RegionEndpointValidator : IFaultInjectionConditionValidator
        {
            private readonly List<Uri> regionEndpoints;

            public RegionEndpointValidator(List<Uri> regionEndpoints)
            {
                this.regionEndpoints = regionEndpoints;
            }
            public bool IsApplicable(string ruleId, ChannelCallArguments args)
            {
                bool isApplicable = this.regionEndpoints.Contains(args.FaultInjectionRequestContext.GetLocationEndpointToRoute());
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.CommonArguments.ActivityId,
                        String.Format(
                            "{0} [RegionEndpoint mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            this.regionEndpoints.Select(i => i.ToString()).ToList(),
                            args.FaultInjectionRequestContext.GetLocationEndpointToRoute()));
                }

                return isApplicable;
            }

            //Used for Connection Delay
            public bool IsApplicable(string ruleId, Guid activityId, DocumentServiceRequest request)
            {
                bool isApplicable = this.regionEndpoints.Contains(request.FaultInjectionRequestContext.GetLocationEndpointToRoute());
                if (!isApplicable)
                {
                    request.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        activityId,
                        String.Format(
                            "{0} [RegionEndpoint mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            this.regionEndpoints.Select(i => i.ToString()).ToList(),
                            request.FaultInjectionRequestContext.GetLocationEndpointToRoute()));
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
            public bool IsApplicable(string ruleId, ChannelCallArguments args)
            {
                bool isApplicable = args.OperationType == this.operationType;
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.CommonArguments.ActivityId,
                        String.Format(
                            "{0} [OperationType mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            this.operationType,
                            args.OperationType));
                }

                return isApplicable;
            }

            //Used for Connection Delay
            public bool IsApplicable(string ruleId, Guid activityId, DocumentServiceRequest request)
            {
                bool isApplicable = request.OperationType == this.operationType;
                if (!isApplicable)
                {
                    request.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        activityId,
                        String.Format(
                            "{0} [OperationType mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            this.operationType,
                            request.OperationType));
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
            public bool IsApplicable(string ruleId, ChannelCallArguments args)
            {
                bool isApplicable = String.Equals(this.containerResourceId, args.ResolvedCollectionRid);
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.CommonArguments.ActivityId,
                        String.Format(
                            "{0} [ContainerRid mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            this.containerResourceId,
                            args.ResolvedCollectionRid));
                }

                return isApplicable;
            }

            //Used for Connection Delay
            public bool IsApplicable(string ruleId, Guid activityId, DocumentServiceRequest request)
            {
                bool isApplicable = String.Equals(this.containerResourceId, request.RequestContext.ResolvedCollectionRid);
                if (!isApplicable)
                {
                    request.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        activityId,
                        String.Format(
                            "{0} [ContainerRid mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            this.containerResourceId,
                            request.RequestContext.ResolvedCollectionRid));
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
            public bool IsApplicable(string ruleId, ChannelCallArguments args)
            {
                bool isApplicable = this.addresses.Exists(uri => args.PreparedCall.Uri.ToString().StartsWith(uri.ToString()));
                if (!isApplicable)
                {
                    args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        args.CommonArguments.ActivityId,
                        String.Format(
                            "{0} [Addresses mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            string.Join(",", this.addresses.Select(i => i.ToString()).ToList()),
                            args.PreparedCall.Uri.ToString()));
                }

                return isApplicable;
            }

            //Used for Connection Delay
            public bool IsApplicable(string ruleId, Guid activityId, string callUri, DocumentServiceRequest request)
            {
                bool isApplicable = this.addresses.Exists(uri => callUri.StartsWith(uri.ToString()));
                if (!isApplicable)
                {
                    request.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                        activityId,
                        String.Format(
                            "{0} [Addresses mistmatch: Excpected {1}, Actual {2}]",
                            ruleId,
                            string.Join(",", this.addresses.Select(i => i.ToString()).ToList()),
                            callUri));
                }

                return isApplicable;
            }
        }
    }
}
