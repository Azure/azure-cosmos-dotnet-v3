// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;

    internal readonly struct DiagnosticScope : IDisposable
    {
        private readonly DiagnosticActivity activity;

        private readonly string name;

        private readonly DiagnosticListener source;

        internal DiagnosticScope(string name, DiagnosticListener source)
        {
            this.name = name;
            this.source = source;
            this.activity = this.source.IsEnabled() ? new DiagnosticActivity(this.name) : null;
            this.activity?.SetW3CFormat();
        }

        public bool IsEnabled => this.activity != null;

        public void AddAttribute(string name, string value)
        {
            this.activity?.AddTag(name, value);
        }

        public void AddAttribute<T>(string name, T value)
        {
            if (this.activity != null && value != null)
            {
                this.AddAttribute(name, value.ToString());
            }
        }

        public void AddAttribute<T>(string name, T value, Func<T, string> format)
        {
            if (this.activity != null)
            {
                this.AddAttribute(name, format(value));
            }
        }

        public void AddLink(string id)
        {
            if (this.activity != null)
            {
                Activity linkedActivity = new Activity("LinkedActivity");
                linkedActivity.SetW3CFormat();
                linkedActivity.SetParentId(id);

                this.activity.AddLink(linkedActivity);
            }
        }

        public void Start()
        {
            if (this.activity != null && this.source.IsEnabled(this.name))
            {
                this.source.StartActivity(this.activity, this.activity);
            }
        }

        public void Dispose()
        {
            if (this.activity == null)
            {
                return;
            }

            if (this.source != null)
            {
                this.source.StopActivity(this.activity, null);
            }
            else
            {
                this.activity?.Stop();
            }
        }

        public void Failed(Exception e)
        {
            if (this.activity == null)
            {
                return;
            }

            this.source?.Write(this.activity.OperationName + ".Exception", e);

        }

        private class DiagnosticActivity : Activity
        {
            private List<Activity> links;

            public IEnumerable<Activity> Links => (IEnumerable<Activity>)this.links ?? Array.Empty<Activity>();

            public DiagnosticActivity(string operationName)
                : base(operationName)
            {
            }

            public void AddLink(Activity activity)
            {
                this.links ??= new List<Activity>();
                this.links.Add(activity);
            }
        }
    }

    /// <summary>
    /// HACK HACK HACK. Some runtime environments like Azure.Functions downgrade System.Diagnostic.DiagnosticSource package version causing method not found exceptions in customer apps
    /// This type is a temporary workaround to avoid the issue.
    /// </summary>
    internal static class ActivityExtensions
    {
        private static readonly MethodInfo setIdFormatMethod = typeof(Activity).GetMethod("SetIdFormat");
        private static readonly MethodInfo getIdFormatMethod = typeof(Activity).GetProperty("IdFormat")?.GetMethod;
        private static readonly MethodInfo getTraceStateStringMethod = typeof(Activity).GetProperty("TraceStateString")?.GetMethod;

        public static bool SetW3CFormat(this Activity activity)
        {
            if (setIdFormatMethod == null) return false;

            setIdFormatMethod.Invoke(activity, new object[] { 2 /* ActivityIdFormat.W3C */});

            return true;
        }

        public static bool IsW3CFormat(this Activity activity)
        {
            if (getIdFormatMethod == null) return false;

            object result = getIdFormatMethod.Invoke(activity, Array.Empty<object>());

            return (int)result == 2 /* ActivityIdFormat.W3C */;
        }

        public static bool TryGetTraceState(this Activity activity, out string traceState)
        {
            traceState = null;

            if (getTraceStateStringMethod == null) return false;

            traceState = getTraceStateStringMethod.Invoke(activity, Array.Empty<object>()) as string;

            return true;
        }
    }
}