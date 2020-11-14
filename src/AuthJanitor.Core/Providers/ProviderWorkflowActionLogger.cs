// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;

namespace AuthJanitor.Providers
{
    public class ProviderWorkflowActionLogger<TCategory> : ProviderWorkflowActionLogger, ILogger<TCategory> { }
    public class ProviderWorkflowActionLogger : ILogger
    {
        public class ProviderWorkflowActionScope<TState> : IDisposable
        {
            public TState State { get; set; }

            #region IDisposable Support
            private bool disposedValue = false;
            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        if (State is IDisposable)
                            ((IDisposable)State).Dispose();
                        State = default;
                    }
                    disposedValue = true;
                }
            }
            public void Dispose() => Dispose(true);
            #endregion
        }

        public DateTime LoggerContextStarted { get; private set; } = DateTime.Now;
        public string LogString { get; set; }

        public IDisposable BeginScope<TState>(TState state) =>
            new ProviderWorkflowActionScope<TState>() { State = state };

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (string.IsNullOrEmpty(LogString)) LoggerContextStarted = DateTime.Now;
            LogString +=
                $"[{(DateTime.Now - LoggerContextStarted).TotalSeconds:00000.00}]" +
                $"[{logLevel}] " +
                formatter(state, exception) +
                Environment.NewLine;
            if (exception != null)
            {
                var exceptionString = exception.ToString() + Environment.NewLine;
                exceptionString = Regex.Replace(exceptionString, "Bearer [A-Za-z0-9\\-\\._~\\+\\/]+=*", "<<REDACTED BEARER TOKEN>>");
                LogString += exceptionString;
            }
        }
    }
}
