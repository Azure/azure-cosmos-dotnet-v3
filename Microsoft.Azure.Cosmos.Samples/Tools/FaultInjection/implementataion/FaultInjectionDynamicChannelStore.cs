namespace Microsoft.Azure.Cosmos.FaultInjection.implementataion
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Azure.Documents;

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
            this.channelDictonary.TryRemove(connectionCorrelationId, out Channel _);
        }

        public IEnumerable<Channel> GetAllChannels()
        {
            return this.channelDictonary.Values.AsEnumerable();
        }
    }
}
