namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Azure.Documents.Rntbd;

    internal class FaultInjectionDynamicChannelStore
    {
        private readonly ConcurrentDictionary<Guid, Channel> channelDictonary;

        public FaultInjectionDynamicChannelStore() 
        {
            this.channelDictonary = new ConcurrentDictionary<Guid, Channel>();
        }

        public void AddChannel(Guid connectionCorrelationId, Channel channel)
        {
            this.channelDictonary.TryAdd(connectionCorrelationId, channel);
        }

        public void RemoveChannel(Guid connectionCorrelationId)
        {
            this.channelDictonary.TryRemove(connectionCorrelationId, out _);
        }

        public List<Channel> GetAllChannels()
        {
            return this.channelDictonary.Values.ToList();
        }

        public List<Guid> GetAllChannelIds()
        {
            return this.channelDictonary.Keys.ToList();
        }
    }
}
