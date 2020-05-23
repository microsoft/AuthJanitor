// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AuthJanitor.Providers
{
    public class RekeyingAttemptLogger : ILogger
    {
        public bool IsSuccessfulAttempt => string.IsNullOrEmpty(OuterException);
        public string OuterException { get; set; }

        public DateTimeOffset AttemptStarted { get; set; }
        public DateTimeOffset AttemptFinished { get; set; }
        public string UserDisplayName { get; set; }
        public string UserEmail { get; set; }

        [JsonIgnore]
        public ILogger ChainedLogger { get; set; }
        public string LogString { get; set; }

        public RekeyingAttemptLogger() => AttemptStarted = DateTimeOffset.UtcNow;
        public RekeyingAttemptLogger(ILogger chainedLogger) : this()
        {
            ChainedLogger = chainedLogger;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            if (ChainedLogger != null)
                return ChainedLogger.BeginScope<TState>(state);
            else
                return new AggregatedStringLoggerScope<TState>()
                {
                    State = state
                };
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (ChainedLogger != null)
                return ChainedLogger.IsEnabled(logLevel);
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            LogString +=
                $"[{(DateTime.Now - AttemptStarted).TotalSeconds:00000.000}]" +
                $"[{logLevel}] " +
                formatter(state, exception) +
                Environment.NewLine;
            if (exception != null)
            {
                var exceptionString = exception.ToString() + Environment.NewLine;
                exceptionString = Regex.Replace(exceptionString, "Bearer [A-Za-z0-9\\-\\._~\\+\\/]+=*", "<<REDACTED BEARER TOKEN>>");
                LogString += exceptionString;
            }
            if (ChainedLogger != null)
                ChainedLogger.Log<TState>(logLevel, eventId, state, exception, formatter);
            AttemptFinished = DateTimeOffset.UtcNow;
        }
    }

    public class AggregatedStringLoggerScope<TState> : IDisposable
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
}