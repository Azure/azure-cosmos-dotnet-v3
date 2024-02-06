//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal class RntbdConnectionErrorInjector
    {
        private readonly FaultInjectionRuleStore ruleStore;
        private readonly FaultInjectionDynamicChannelStore channelStore;
        private readonly RegionNameMapper regionNameMapper;
        private readonly Dictionary<string, string> regionSpecialCases;

        public RntbdConnectionErrorInjector(FaultInjectionRuleStore ruleStore, FaultInjectionDynamicChannelStore channelStore)
        {
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
            this.channelStore = channelStore ?? throw new ArgumentNullException(nameof(channelStore));
            this.regionNameMapper = new RegionNameMapper();
            // Some regions have different names in the RNTBD endpoint and in the location endpoint
            // this dictionary maps the RNTBD endpoint region name to the location endpoint region name
            this.regionSpecialCases = new Dictionary<string, string>
            {
                { "westus1", "westus" },
                { "eastus1", "eastus" },
                { "chinanorth1", "chinanorth" },
                { "chinaeast1", "chinaeast" },
                { "australiacentral1", "australiacentral" },
            };
        }

        public bool Accept(IFaultInjectionRuleInternal rule)
        {
            if ((rule.GetConnectionType() == FaultInjectionConnectionType.Direct 
                || rule.GetConnectionType() == FaultInjectionConnectionType.All)
                && (rule.GetType() == typeof(FaultInjectionConnectionErrorRule)))
            {
                this.InjectConnectionErrorTask((FaultInjectionConnectionErrorRule)rule);
                return true;
            }
            return false;
        }

        public Task InjectConnectionErrorTask(FaultInjectionConnectionErrorRule rule)
        {
            TimeSpan delay = rule.GetResult().GetTimespan();

            return Task.Delay(delay).ContinueWith(
                t =>
                {
                    //check to see if rule is valid
                    if (this.IsEffectiveRule(rule))
                    {
                        List<Channel> allChannels = this.channelStore.GetAllChannels();
                        Random random = new Random();
                        FaultInjectionConnectionErrorType connectionErrorType = rule.GetResult().GetConnectionErrorType();
                        //Case 1: Inject connection error for specific physical address
                        List<Uri> addresses = rule.GetAddresses();
                        if (addresses != null && addresses.Count > 0)
                        {
                            foreach (Uri addressUri in addresses)
                            {
                                foreach (Channel channel in allChannels)
                                {
                                    Uri serverUri;
                                    try
                                    {
                                        serverUri = channel.GetServerUri();
                                    }
                                    catch (Exception)
                                    {
                                        //Channel is alread disposed, there can sometimes be lag from when the rule is applied and when the channel is disposed
                                        //and marked unhealthy
                                        continue;
                                    }

                                    if (serverUri.Equals(addressUri))
                                    {
                                        rule.ApplyRule();
                                        if (random.NextDouble() < rule.GetResult().GetThresholdPercentage())
                                        {
                                            rule.ApplyRule();
                                            DefaultTrace.TraceInformation("FaultInjection: Injecting {0} connection error rule: {1}, for address {2}",
                                                connectionErrorType,
                                                rule.GetId(),
                                                addressUri);
                                            channel.InjectFaultInjectionConnectionError(this.GetTransportException(connectionErrorType, serverUri));
                                        }
                                    }
                                }
                            }

                            return Task.CompletedTask;
                        }

                        //Case 2: Inject connection error for all endpoints of one region when there is no specific physical address
                        List<Uri> regionEndpoints = rule.GetRegionEndpoints();
                        if (regionEndpoints != null && regionEndpoints.Count > 0)
                        {

                            foreach (Uri regionEndpoint in regionEndpoints)
                            {
                                foreach (Channel channel in allChannels)
                                {
                                    Uri serverUri;
                                    try
                                    {
                                        serverUri = channel.GetServerUri();
                                    }
                                    catch (Exception)
                                    {
                                        //Channel is alread disposed, there can sometimes be lag from when the rule is applied and when the channel is disposed
                                        //and marked unhealthy
                                        continue;
                                    }

                                    if(this.ParseRntbdEndpointForNormalizedRegion(serverUri).Equals(this.ParseRntbdEndpointForNormalizedRegion(regionEndpoint)))
                                    {
                                        if (random.NextDouble() < rule.GetResult().GetThresholdPercentage())
                                        {
                                            rule.ApplyRule();
                                            DefaultTrace.TraceInformation("FaultInjection: Injecting {0} connection error rule: {1} for region {2}",
                                                connectionErrorType,
                                                rule.GetId(),
                                                regionEndpoint);
                                            channel.InjectFaultInjectionConnectionError(this.GetTransportException(connectionErrorType, serverUri));
                                        }
                                    }
                                }
                            }

                            return Task.CompletedTask;
                        }

                        //Case 3: Inject connection error for all endpoints of all regions when there is no specific physical address and region
                        foreach (Channel channel in allChannels)
                        {
                            Uri serverUri;
                            try
                            {
                                serverUri = channel.GetServerUri();
                            }
                            catch (Exception)
                            {
                                //Channel is alread disposed, there can sometimes be lag from when the rule is applied and when the channel is disposed
                                //and marked unhealthy
                                continue;
                            }
                            
                            if (random.NextDouble() < rule.GetResult().GetThresholdPercentage())
                            {
                                rule.ApplyRule();
                                DefaultTrace.TraceInformation("FaultInjection: Injecting {0} connection error rule: {1}",
                                    connectionErrorType,
                                    rule.GetId());
                                channel.InjectFaultInjectionConnectionError(this.GetTransportException(connectionErrorType, serverUri));
                            }
                        }

                        return Task.CompletedTask;
                    }

                    return Task.CompletedTask;
                }).ContinueWith(
                t =>
                {
                    //repeats rule injection if rule is still valid
                    if (this.IsEffectiveRule(rule))
                    {
                        this.InjectConnectionErrorTask(rule);
                    }
                    else
                    {
                        //removes rule from rule store one rule is no longer valid
                        this.ruleStore.RemoveRule(rule);
                    }
                });
        }

        private TransportException GetTransportException(FaultInjectionConnectionErrorType errorType, Uri serverUri)
        {
            return errorType switch
            {
                FaultInjectionConnectionErrorType.ReceiveStreamClosed => new TransportException(
                                        errorCode: TransportErrorCode.ReceiveStreamClosed,
                                        innerException: null,
                                        activityId: Guid.Empty,
                                        requestUri: serverUri,
                                        sourceDescription: "FaultInjectionConnectionError",
                                        userPayload: false,
                                        payloadSent: true),
                FaultInjectionConnectionErrorType.ReceiveFailed => new TransportException(
                                        errorCode: TransportErrorCode.ReceiveFailed,
                                        innerException: null,
                                        activityId: Guid.Empty,
                                        requestUri: serverUri,
                                        sourceDescription: "FaultInjectionConnectionError",
                                        userPayload: false,
                                        payloadSent: true),
                _ => throw new ArgumentException("Invalid connection error type"),
            };
        }

        private bool IsEffectiveRule(FaultInjectionConnectionErrorRule rule)
        {
            return this.ruleStore.ContainsRule(rule) && rule.IsValid();
        }

        private string ParseRntbdEndpointForNormalizedRegion(Uri endpoint)
        {
            string region = endpoint.ToString().Split(new char[] { '-' })[3];
            region = this.regionNameMapper.GetCosmosDBRegionName(
                this.regionSpecialCases.ContainsKey(region) 
                ? this.regionSpecialCases[region]
                : region);
            return region;
        }

        private string ParseLocationEndpointForNormalizedRegion(Uri endpoint)
        {
            string region = endpoint.ToString().Split(new char[] { '-' }).Last();
            region = region[0..region.IndexOf('.')];
            region = this.regionNameMapper.GetCosmosDBRegionName(region);
            return region;
        }
    }
}
