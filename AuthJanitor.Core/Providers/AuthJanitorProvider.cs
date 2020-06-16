// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthJanitor.Providers
{
    public interface IAuthJanitorProvider
    {
        /// <summary>
        /// Serialized ProviderConfiguration
        /// </summary>
        string SerializedConfiguration { get; set; }

        /// <summary>
        /// Access Token credential to use when executing Provider actions
        /// </summary>
        AccessTokenCredential Credential { get; set; }

        /// <summary>
        /// Test if the current credentials can execute an Extension 
        /// </summary>
        /// <returns></returns>
        Task Test();

        /// <summary>
        /// Get a text description of the action which is taken by the Extension
        /// </summary>
        /// <returns></returns>
        string GetDescription();

        /// <summary>
        /// Get a list of risky items for the Extension based on the Secret validity period
        /// </summary>
        /// <param name="requestedValidPeriod">Requested period of validity</param>
        /// <returns></returns>
        IList<RiskyConfigurationItem> GetRisks(TimeSpan requestedValidPeriod);

        /// <summary>
        /// Get a list of risky items for the Extension independent of Secret validity period
        /// </summary>
        /// <returns></returns>
        IList<RiskyConfigurationItem> GetRisks();

        /// <summary>
        /// Get the Provider's metadata
        /// </summary>
        ProviderAttribute ProviderMetadata => GetType().GetCustomAttribute<ProviderAttribute>();
    }

    /// <summary>
    /// Describes an AuthJanitor Provider which either rekeys a service (RekeyableService) or manages an application lifecycle (ApplicationLifecycle)
    /// </summary>
    public abstract class AuthJanitorProvider<TConfiguration> : IAuthJanitorProvider where TConfiguration : AuthJanitorProviderConfiguration
    {
        private TConfiguration _cachedConfigurationInstance;

        /// <summary>
        /// Provider Configuration
        /// </summary>
        public TConfiguration Configuration
        {
            get => _cachedConfigurationInstance ??= JsonSerializer.Deserialize<TConfiguration>(SerializedConfiguration);
        }

        /// <summary>
        /// Serialized ProviderConfiguration
        /// </summary>
        public string SerializedConfiguration { get; set; }

        /// <summary>
        /// Access Token credential to use when executing Provider actions
        /// </summary>
        public AccessTokenCredential Credential { get; set; }

        /// <summary>
        /// Test if the current credentials can execute an Extension 
        /// </summary>
        /// <returns></returns>
        public virtual Task Test()
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Get a text description of the action which is taken by the Provider
        /// </summary>
        /// <returns></returns>
        public abstract string GetDescription();

        /// <summary>
        /// Get a list of risky items for the Provider based on Secret validity period
        /// </summary>
        /// <param name="requestedValidPeriod">Requested period of validity</param>
        /// <returns></returns>
        public virtual IList<RiskyConfigurationItem> GetRisks(TimeSpan requestedValidPeriod) => GetRisks();

        /// <summary>
        /// Get a list of risky items for the Provider independent of Secret validity period
        /// </summary>
        /// <returns></returns>
        public virtual IList<RiskyConfigurationItem> GetRisks() => new List<RiskyConfigurationItem>();
    }
}
