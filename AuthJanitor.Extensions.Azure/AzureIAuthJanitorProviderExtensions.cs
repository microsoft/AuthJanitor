// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System.Threading.Tasks;

namespace AuthJanitor.Extensions.Azure
{
    public static class AzureIAuthJanitorProviderExtensions
    {
        public static async Task<Microsoft.Azure.Management.Fluent.IAzure> GetAzure(this IAuthJanitorProvider provider)
        {
            return await Microsoft.Azure.Management.Fluent.Azure
                .Configure()
                .Authenticate(provider.Credential.CreateAzureCredentials())
                .WithDefaultSubscriptionAsync();
        }
    }
}
