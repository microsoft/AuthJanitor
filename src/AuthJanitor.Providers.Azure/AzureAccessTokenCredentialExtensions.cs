// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Azure.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;
using System;

namespace AuthJanitor.Providers.Azure
{
    public static class AzureAccessTokenCredentialExtensions
    {
        public static AzureCredentials CreateAzureCredentials(this AccessTokenCredential accessTokenCredential) =>
            new AzureCredentials(
                new TokenCredentials(accessTokenCredential.AccessToken, accessTokenCredential.TokenType),
                new TokenCredentials(accessTokenCredential.AccessToken, accessTokenCredential.TokenType),
                Environment.GetEnvironmentVariable("TENANT_ID", EnvironmentVariableTarget.Process),
                AzureEnvironment.AzureGlobalCloud);

        public static TokenCredential CreateTokenCredential(this AccessTokenCredential accessTokenCredential) =>
            new ExistingTokenCredential(accessTokenCredential.AccessToken, accessTokenCredential.ExpiresOnDateTime);
    }
}