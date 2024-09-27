namespace Sample.Listeners
{
    using System.Diagnostics.Tracing;
    using System.Diagnostics;
    using System.Collections.Concurrent;

    /// <summary>
    /// This listener can cover following aspects:
    /// 1. Write its own monitoring library with the custom implementation of aggregation or whatever you want to do with this data.
    /// 2. Support an APM tool which is not open telemetry compliant.
    /// </summary>
    /// <remarks>It is a simple sample. Anybody can get as creative as they want to make it better in terms of usability and performance.</remarks>
    internal class CustomDiagnosticAndEventListener :
        EventListener, // Override Event Listener to capture Event source events
        IObserver<KeyValuePair<string, object?>>, // Override IObserver to capture Activity events
        IObserver<DiagnosticListener>,
        IDisposable
    {
        private readonly string diagnosticSourceName;
        private readonly string eventSourceName;

        private ConcurrentBag<IDisposable>? Subscriptions = new();
        private ConcurrentBag<Activity> Activities { get; } = new();

        public CustomDiagnosticAndEventListener(string diagnosticSourceName, string eventSourceName)
        {
            this.diagnosticSourceName = diagnosticSourceName;
            this.eventSourceName = eventSourceName;

            DiagnosticListener.AllListeners.Subscribe(this);
        }

        /// <summary>
        /// IObserver Override
        /// </summary>
        public void OnCompleted() {
            Console.WriteLine("OnCompleted");
        }

        /// <summary>
        /// IObserver Override
        /// </summary>
        public void OnError(Exception error) {
            Console.WriteLine($"OnError : {error}");
        }

        /// <summary>
        /// IObserver Override
        /// </summary>
        public void OnNext(KeyValuePair<string, object?> value)
        {
            lock (this.Activities)
            {
                // Check for disposal
                if (this.Subscriptions == null) return;

                string startSuffix = ".Start";
                string stopSuffix = ".Stop";
                string exceptionSuffix = ".Exception";

                if (Activity.Current == null)
                {
                    return;
                }

                if (value.Key.EndsWith(startSuffix))
                {
                    this.Activities.Add(Activity.Current);
                }
                else if (value.Key.EndsWith(stopSuffix) || value.Key.EndsWith(exceptionSuffix))
                {
                    foreach (Activity activity in this.Activities)
                    {
                        if (activity.Id == Activity.Current.Id)
                        {
                            Console.WriteLine($"    Activity Name: {activity.DisplayName}");
                            Console.WriteLine($"    Activity Operation Name: {activity.OperationName}");
                            foreach (KeyValuePair<string, string?> actualTag in activity.Tags)
                            {
                                Console.WriteLine($"    {actualTag.Key} ==> {actualTag.Value}");
                            }
                            Console.WriteLine();
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// IObserver Override
        /// </summary>
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == this.diagnosticSourceName && this.Subscriptions != null)
            {
                Console.WriteLine($"CustomDiagnosticAndEventListener : OnNext : {value.Name}");
                lock (this.Activities)
                {
                    this.Subscriptions?.Add(value.Subscribe(this));
                }
            }
        }

        /// <summary>
        /// EventListener Override
        /// </summary>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource != null && eventSource.Name.Equals(this.eventSourceName))
            {
                Console.WriteLine($"CustomDiagnosticAndEventListener : OnEventSourceCreated : {eventSource.Name}");
                this.EnableEvents(eventSource, EventLevel.Informational); // Enable information level events
            }
        }

        /// <summary>
        /// EventListener Override
        /// </summary>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Console.WriteLine($"    Event Name: {eventData.EventName}");
            Console.WriteLine($"    Event Level: {eventData.Level}");
            if(eventData.Payload != null)
            {
                int counter = 0;
                foreach (object? payload in eventData.Payload)
                {
                    Console.WriteLine($"    Event Payload {counter++}: {payload}");
                }
            }
            else
            {
                Console.WriteLine($"    Event Payload: NULL");
            }
            Console.WriteLine();
        }

        public override void Dispose()
        {
            Console.WriteLine("CustomDiagnosticAndEventListener : Dispose");
            base.Dispose();

            if (this.Subscriptions == null)
            {
                return;
            }

            ConcurrentBag<IDisposable> subscriptions;
            lock (this.Activities)
            {
                subscriptions = this.Subscriptions;
                this.Subscriptions = null;
            }

            foreach (IDisposable subscription in subscriptions)
            {
                subscription.Dispose(); // Dispose of DiagnosticListener subscription
            }

            foreach (Activity activity in this.Activities)
            {
                activity.Dispose(); // Dispose of Activity
            }
        }
    }
}
