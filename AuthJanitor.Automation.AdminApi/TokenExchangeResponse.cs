// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Newtonsoft.Json;
using System;

namespace AuthJanitor.Automation.AdminApi
{
    public class TokenExchangeResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        public TimeSpan ExpiresInTimeSpan => TimeSpan.FromSeconds(ExpiresIn);

        [JsonProperty("expires_on")]
        public long ExpiresOn { get; set; }

        public DateTimeOffset ExpiresOnDateTime => DateTimeOffset.FromUnixTimeSeconds(ExpiresOn);

        [JsonProperty("ext_expires_in")]
        public int ExtExpiresIn { get; set; }

        public TimeSpan ExtExpiresInTimeSpan => TimeSpan.FromSeconds(ExtExpiresIn);

        [JsonProperty("not_before")]
        public long NotBefore { get; set; }

        public DateTimeOffset NotBeforeDateTime => DateTimeOffset.FromUnixTimeSeconds(NotBefore);

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("resource")]
        public string Resource { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }
    }
}
