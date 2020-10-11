// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;

namespace AuthJanitor.Providers.Capabilities
{
    public class ProviderResourceCandidate
    {
        /// <summary>
        /// Anything which could be used by an application to connect to the rekeyable object
        /// </summary>
        public HashSet<string> RekeyableObjectIdentifiers { get; set; }
        public IRekeyableObjectProvider RekeyableObjectProvider { get; set; }

        /// <summary>
        /// Hashes of secrets to be compared to those used by an application (hash the application's current secrets and match)
        /// </summary>
        public HashSet<string> RekeyableObjectSecretHashes { get; set; }

        public IDictionary<string, IApplicationLifecycleProvider> ApplicationLifecycleProviders { get; set; } = new Dictionary<string, IApplicationLifecycleProvider>();

        public bool HasSecretHash(string secretHash) =>
            RekeyableObjectSecretHashes.Contains(secretHash);
        public bool HasIdentifier(string identifier) =>
            RekeyableObjectIdentifiers.Contains(identifier);
    }
}
