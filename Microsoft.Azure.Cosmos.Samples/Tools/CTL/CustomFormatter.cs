//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Logging;
    using System.IO;
    using System;
    using Microsoft.Extensions.Logging.Console;
    using Microsoft.Extensions.Options;

    public sealed class CustomFormatter : ConsoleFormatter, IDisposable
    {
        private readonly IDisposable? _optionsReloadToken;
        private CustomOptions _formatterOptions;

        public CustomFormatter(IOptionsMonitor<CustomOptions> options)
            // Case insensitive
            : base("customName")
        {
            (this._optionsReloadToken, this._formatterOptions) =
                (options.OnChange(this.ReloadLoggerOptions), options.CurrentValue);
        }

        private void ReloadLoggerOptions(CustomOptions options)
        {
            this._formatterOptions = options;
        }

        public override void Write<TState>(
            in LogEntry<TState> logEntry,
            IExternalScopeProvider? scopeProvider,
            TextWriter textWriter)
        {
            string? message =
                logEntry.Formatter?.Invoke(
                    logEntry.State, logEntry.Exception);

            if (message is null)
            {
                return;
            }

            this.CustomLogicGoesHere(textWriter);
            textWriter.WriteLine(message);
        }

        private void CustomLogicGoesHere(TextWriter textWriter)
        {
            textWriter.Write(this._formatterOptions.CustomPrefix);
        }

        public void Dispose()
        {
            this._optionsReloadToken?.Dispose();
        }
    }
}