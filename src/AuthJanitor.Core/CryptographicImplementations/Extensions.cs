// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Cryptography;

namespace AuthJanitor.CryptographicImplementations
{
    public static class Extensions
    {
        public static DefaultCryptographicImplementationConfiguration EmbedEphemeralRSAKey(
            this DefaultCryptographicImplementationConfiguration config)
        {
            var rsa = RSA.Create();
            config.PublicKey = rsa.ExportRSAPublicKey();
            config.PrivateKey = rsa.ExportRSAPrivateKey();
            return config;
        }

        public static void AddAJDefaultCryptographicImplementation<TOptions>(this IServiceCollection serviceCollection, Action<DefaultCryptographicImplementationConfiguration> configureOptions)
        {
            serviceCollection.Configure<DefaultCryptographicImplementationConfiguration>(configureOptions);
            serviceCollection.AddSingleton<ICryptographicImplementation, DefaultCryptographicImplementation>();
        }
    }
}
