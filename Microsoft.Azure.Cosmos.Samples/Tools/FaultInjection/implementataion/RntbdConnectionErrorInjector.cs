namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;

    internal class RntbdConnectionErrorInjector
    {
        private readonly FaultInjectionRuleStore ruleStore;
        private readonly ChannelDictionary channelDictionary;

        public RntbdConnectionErrorInjector(FaultInjectionRuleStore ruleStore, ChannelDictionary channelDictionary)
        {
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
            this.channelDictionary = channelDictionary ?? throw new ArgumentNullException(nameof(channelDictionary));
        }

        public bool Accept(IFaultInjectionRuleInternal rule)
        {
            if (rule.GetConnectionType() == FaultInjectionConnectionType.DIRECT_MODE
                && (rule.GetType() == typeof(FaultInjectionConnectionErrorRule)))
            {
                this.InjectConnectionErrorTask((FaultInjectionConnectionErrorRule)rule).Start();
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
                        List<Channel> allChannels = this.channelDictionary.GetAllChannels();
                        Random random = new Random()
                        FaultInjectionConnectionErrorType connectionErrorType = rule.GetResult().GetConnectionErrorType();
                        //Case 1: Inject connection error for specific physical address
                        List<Uri> addresses = rule.GetAddresses();
                        if (addresses != null && addresses.Count > 0)
                        {
                            addresses.ForEach(addressUri => allChannels.Where(channel => channel.GetServerUri().Equals(addressUri)).ToList().ForEach(channel =>
                            {
                                if (random.NextDouble() < rule.GetResult().GetThreshold())
                                {
                                    DefaultTrace.TraceInformation("FaultInjection: Injecting connection error for address {0}", addressUri);
                                    channel.InjectFaultInjectionConnectionError(rule.GetId(), connectionErrorType, this.GetTransportException(connectionErrorType, channel));
                                }
                            }));

                            return Task.CompletedTask;
                        }

                        //Case 2: Inject connection error for all endpoins of one region when there is no specific physical address
                        List<Uri> regionEndpoints = rule.GetRegionEndpoints();
                        if (regionEndpoints != null && regionEndpoints.Count > 0)
                        {
                            regionEndpoints.ForEach(regionEndpoint => allChannels.Where(channel =>
                                channel.GetServerUri().DnsSafeHost.Equals(regionEndpoint.DnsSafeHost)).ToList().ForEach(channel =>
                                {
                                    if (random.NextDouble() < rule.GetResult().GetThreshold())
                                    {
                                        DefaultTrace.TraceInformation("FaultInjection: Injecting connection error for region {0}", regionEndpoint);
                                        channel.InjectFaultInjectionConnectionError(rule.GetId(), connectionErrorType, this.GetTransportException(connectionErrorType, channel));
                                    }
                                }));

                            return Task.CompletedTask;
                        }

                        //Case 3: Inject connection error for all endpoints of all regions when there is no specific physical address and region
                        allChannels.ForEach(channel =>
                        {
                            if (random.NextDouble() < rule.GetResult().GetThreshold())
                            {
                                DefaultTrace.TraceInformation("FaultInjection: Injecting connection error");
                                channel.InjectFaultInjectionConnectionError(rule.GetId(), connectionErrorType, this.GetTransportException(connectionErrorType, channel));
                            }
                        });

                        return Task.CompletedTask;
                    }

                    return Task.CompletedTask;
                });
        }

        private TransportException GetTransportException(FaultInjectionConnectionErrorType errorType, Channel channel)
        {
            switch (errorType)
            {
                case FaultInjectionConnectionErrorType.RECIEVED_STREAM_CLOSED:
                    return new TransportException(
                        errorCode: TransportErrorCode.ReceiveStreamClosed,
                        innerException: null,
                        activityId: Guid.Empty,
                        requestUri: channel.GetServerUri(),
                        sourceDescription: "FaultInjectionConnectionError",
                        userPayload: false,
                        payloadSent: true);
                case FaultInjectionConnectionErrorType.RECIEVE_FAILED:
                    return new TransportException(
                        errorCode: TransportErrorCode.ReceiveFailed,
                        innerException: null,
                        activityId: Guid.Empty,
                        requestUri: channel.GetServerUri(),
                        sourceDescription: "FaultInjectionConnectionError",
                        userPayload: false,
                        payloadSent: true);
                default:
                    throw new ArgumentException("Invalid connection error type");
            }
        }

        private bool IsEffectiveRule(FaultInjectionConnectionErrorRule rule)
        {
            return this.ruleStore.ContainsRule(rule) && rule.IsValid();
        }
    }
}
