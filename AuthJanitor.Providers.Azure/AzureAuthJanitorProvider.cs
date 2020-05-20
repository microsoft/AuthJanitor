// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Azure.Core;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Rest;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Azure
{
    public abstract class AzureAuthJanitorProvider<TConfiguration, TResource> : AuthJanitorProvider<TConfiguration>
        where TConfiguration : AzureAuthJanitorProviderConfiguration
    {
        protected async Task<TResource> GetResourceAsync() =>
            await GetResourceCollection(await GetAzureAsync())
                    .GetByResourceGroupAsync(Configuration.ResourceGroup, Configuration.ResourceName);

        protected abstract ISupportsGettingByResourceGroup<TResource> GetResourceCollection(IAzure azure);

        protected async Task<IAzure> GetAzureAsync()
        {
            if (string.IsNullOrEmpty(Configuration.SubscriptionId))
                return await Microsoft.Azure.Management.Fluent.Azure
                    .Configure()
                    .Authenticate(CreateAzureCredentials())
                    .WithDefaultSubscriptionAsync();
            else
                // BUG: Fluent does not have "WithSubscriptionAsync"
                return await Task.FromResult(Microsoft.Azure.Management.Fluent.Azure
                    .Configure()
                    .Authenticate(CreateAzureCredentials())
                    .WithSubscription(Configuration.SubscriptionId));
        }

        protected AzureCredentials CreateAzureCredentials() =>
            new AzureCredentials(
                new TokenCredentials(Credential.AccessToken, Credential.TokenType),
                new TokenCredentials(Credential.AccessToken, Credential.TokenType),
                Environment.GetEnvironmentVariable("TENANT_ID", EnvironmentVariableTarget.Process),
                AzureEnvironment.AzureGlobalCloud);

        protected TokenCredential CreateTokenCredential() =>
            new ExistingTokenCredential(Credential.AccessToken, Credential.ExpiresOnDateTime);
    }
}
