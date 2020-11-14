// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers.Capabilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Azure.Workflows
{
    public abstract class TwoKeyAzureRekeyableObjectProvider<TConfiguration, TResource, TKeyring, TKeyType, TSdkKeyType> : 
        AzureAuthJanitorProvider<TConfiguration, TResource>,
        ICanRunSanityTests,
        ICanGenerateTemporarySecretValue,
        ICanRekey,
        ICanCleanup
        where TConfiguration : TwoKeyAzureAuthJanitorProviderConfiguration<TKeyType>
        where TKeyType : struct, Enum
    {
        protected TwoKeyAzureRekeyableObjectProvider(ILogger logger) : base(logger) { }

        protected abstract string Service { get; }
        protected abstract TSdkKeyType Translate(TKeyType keyType);

        protected abstract Task<TKeyring> RetrieveCurrentKeyring(TResource resource, TSdkKeyType keyType);
        protected abstract Task<TKeyring> RotateKeyringValue(TResource resource, TSdkKeyType keyType);

        protected abstract RegeneratedSecret CreateSecretFromKeyring(TKeyring keyring, TKeyType keyType);

        public async Task Test()
        {
            var resource = await GetResourceAsync();
            if (resource == null) throw new Exception("Could not retrieve resource");

            var currentKeyringTask = RetrieveCurrentKeyring(resource, Translate(Configuration.KeyType)).ContinueWith(t => { 
                if (t.Result == null) { throw new Exception($"Could not retrieve keyring for selected key type {Configuration.KeyType}"); } 
            });
            
            var otherKeyringTask = RetrieveCurrentKeyring(resource, Translate(GetOtherPairedKey(Configuration.KeyType))).ContinueWith(t => {
                if (t.Result == null) { throw new Exception($"Could not retrieve keyring for opposite paired key type {GetOtherPairedKey(Configuration.KeyType)}"); }
            });

            await Task.WhenAll(currentKeyringTask, otherKeyringTask);
        }

        public async Task<RegeneratedSecret> GenerateTemporarySecretValue()
        {
            Logger.LogInformation("Getting temporary secret to use during rekeying from other ({OtherKeyType}) key...", GetOtherPairedKey(Configuration.KeyType));
            var resource = await GetResourceAsync();
            var keyring = await RetrieveCurrentKeyring(resource, Translate(GetOtherPairedKey(Configuration.KeyType)));
            Logger.LogInformation("Successfully retrieved temporary secret to use during rekeying from other ({OtherKeyType}) key...", GetOtherPairedKey(Configuration.KeyType));

            var regeneratedSecret = CreateSecretFromKeyring(keyring, GetOtherPairedKey(Configuration.KeyType));
            regeneratedSecret.Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30);
            regeneratedSecret.UserHint = Configuration.UserHint;
            return regeneratedSecret;
        }

        public async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Regenerating key type {KeyType}", Configuration.KeyType);
            var resource = await GetResourceAsync();
            var keyring = await RotateKeyringValue(resource, Translate(Configuration.KeyType));
            if (keyring == null) keyring = await RetrieveCurrentKeyring(resource, Translate(Configuration.KeyType));
            Logger.LogInformation("Successfully rekeyed key type {KeyType}", Configuration.KeyType);

            var regeneratedSecret = CreateSecretFromKeyring(keyring, Configuration.KeyType);
            regeneratedSecret.Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30);
            regeneratedSecret.UserHint = Configuration.UserHint;
            return regeneratedSecret;
        }

        public async Task Cleanup()
        {
            if (!Configuration.SkipScramblingOtherKey)
            {
                Logger.LogInformation("Scrambling opposite key kind {OtherKeyType}", GetOtherPairedKey(Configuration.KeyType));
                await RotateKeyringValue(await GetResourceAsync(), Translate(GetOtherPairedKey(Configuration.KeyType)));
            }
            else
                Logger.LogInformation("Skipping scrambling opposite key kind {OtherKeyKind}", GetOtherPairedKey(Configuration.KeyType));
        }

        public override string GetDescription() =>
            $"Regenerates the {Configuration.KeyType} key for a {Service} instance " +
            $"called '{Configuration.ResourceName}' (Resource Group '{Configuration.ResourceGroup}'). " +
            $"The {GetOtherPairedKey(Configuration.KeyType)} key is used as a temporary " +
            $"key while rekeying is taking place. The {GetOtherPairedKey(Configuration.KeyType)} " +
            $"key will {(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            var issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) key of this type is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey the service and maintain a high degree of security."
                });
            }
            return issues;
        }

        protected TKeyType GetOtherPairedKey(TKeyType keyType)
        {
            var pairs = new Dictionary<TKeyType, string>();
            var memberInfos = typeof(TKeyType).GetMembers();
            foreach (var memberInfo in memberInfos)
            {
                var pairAttr = memberInfo.GetCustomAttribute<PairedKeyAttribute>();
                if (pairAttr == null)
                    continue;
                pairs[Enum.Parse<TKeyType>(memberInfo.Name)] = pairAttr.PairName;
            }

            if (!pairs.ContainsKey(keyType) ||
                 pairs.Count(p => p.Value == pairs[keyType]) < 2)
                throw new Exception("Key configuration is not valid; pair is not present!");

            return pairs
                    .Where(p => !p.Key.Equals(keyType))
                    .First(p => p.Value == pairs[keyType])
                    .Key;
        }
    }
}
