// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AuthJanitor.Integrations.EventSinks.SendGrid
{
    public static class Extensions
    {
        public static void AddAJSendGridEventSink<TOptions>(this IServiceCollection serviceCollection, Action<SendGridEventSinkConfiguration> configureOptions)
        {
            serviceCollection.Configure<SendGridEventSinkConfiguration>(configureOptions);
            serviceCollection.AddSingleton<IEventSink, SendGridEventSink>();
        }
    }
}
