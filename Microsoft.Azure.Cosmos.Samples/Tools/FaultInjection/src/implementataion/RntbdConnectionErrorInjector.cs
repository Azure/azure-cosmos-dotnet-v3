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
            if (rule.GetConnectionType() == FaultInjectionConnectionType.Direct
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
                            addresses.ForEach(addressUri => allChannels.Where(channel => channel.GetServerUri().Equals(addressUri)).ToList().ForEach(channel =>
                            {
                                if (random.NextDouble() < rule.GetResult().GetThreshold())
                                {
                                    rule.ApplyRule();
                                    DefaultTrace.TraceInformation("FaultInjection: Injecting {0} connection error rule: {1}, for address {2}", 
                                        connectionErrorType, 
                                        rule.GetId(), 
                                        addressUri);
                                    channel.InjectFaultInjectionConnectionError(this.GetTransportException(connectionErrorType, channel));
                                }
                            }));

                            return Task.CompletedTask;
                        }

                        //Case 2: Inject connection error for all endpoins of one region when there is no specific physical address
                        List<Uri> regionEndpoints = rule.GetRegionEndpoints();
                        if (regionEndpoints != null && regionEndpoints.Count > 0)
                        {
                            regionEndpoints.ForEach(regionEndpoint => allChannels.Where(channel =>
                                this.ParseRntbdEndpointForNormalizedRegion(channel.GetServerUri())
                                    .Equals(this.ParseRntbdEndpointForNormalizedRegion(regionEndpoint))).ToList()
                                .ForEach(channel =>
                                {
                                    if (random.NextDouble() < rule.GetResult().GetThreshold())
                                    {
                                        rule.ApplyRule();
                                        DefaultTrace.TraceInformation("FaultInjection: Injecting {0} connection error rule: {1} for region {2}", 
                                            connectionErrorType, 
                                            rule.GetId(),
                                            regionEndpoint);
                                        channel.InjectFaultInjectionConnectionError(this.GetTransportException(connectionErrorType, channel));
                                    }
                                }));

                            return Task.CompletedTask;
                        }

                        //Case 3: Inject connection error for all endpoints of all regions when there is no specific physical address and region
                        allChannels.ForEach(channel =>
                        {
                            if (random.NextDouble() < rule.GetResult().GetThreshold())
                            {
                                rule.ApplyRule();
                                DefaultTrace.TraceInformation("FaultInjection: Injecting {0} connection error rule: {1}",
                                    connectionErrorType, 
                                    rule.GetId());
                                channel.InjectFaultInjectionConnectionError(this.GetTransportException(connectionErrorType, channel));
                            }
                        });

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

        private TransportException GetTransportException(FaultInjectionConnectionErrorType errorType, Channel channel)
        {
            return errorType switch
            {
                FaultInjectionConnectionErrorType.RecievedStreamClosed => new TransportException(
                                        errorCode: TransportErrorCode.ReceiveStreamClosed,
                                        innerException: null,
                                        activityId: Guid.Empty,
                                        requestUri: channel.GetServerUri(),
                                        sourceDescription: "FaultInjectionConnectionError",
                                        userPayload: false,
                                        payloadSent: true),
                FaultInjectionConnectionErrorType.RecieveFailed => new TransportException(
                                        errorCode: TransportErrorCode.ReceiveFailed,
                                        innerException: null,
                                        activityId: Guid.Empty,
                                        requestUri: channel.GetServerUri(),
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
