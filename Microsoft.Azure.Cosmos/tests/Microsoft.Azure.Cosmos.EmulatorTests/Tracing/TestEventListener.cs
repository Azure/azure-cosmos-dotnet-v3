//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tests;
    
    internal class TestEventListener : EventListener
    {
        private string eventName;
        
        public ConcurrentBag<string> CollectedEvents { set; get; } = new();

        internal TestEventListener(string eventName)
        {
            this.eventName = eventName;
        }
        
        /// <summary>
        /// EventListener Override
        /// </summary>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource != null && eventSource.Name.Equals(this.eventName))
            {
                this.EnableEvents(eventSource, EventLevel.Informational); // Enable information level events
            }
        }

        /// <summary>
        /// EventListener Override
        /// </summary>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<EVENT>")
                   .Append("<EVENT-NAME>").Append(eventData.EventName).Append("</EVENT-NAME>")
                   .Append("<EVENT-TEXT>Ideally, this should contain request diagnostics but request diagnostics is " +
                   "subject to change with each request as it contains few unique id. " +
                   "So just putting this tag with this static text to make sure event is getting generated" +
                   " for each test.</EVENT-TEXT>")
                   .Append("</EVENT>");
            this.CollectedEvents.Add(builder.ToString());
        }

        public override void Dispose()
        {
            base.Dispose();

            this.eventName = null;
        }
    }
}
