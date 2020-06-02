// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Text.Json.Serialization;

namespace AuthJanitor
{
    public class AccessTokenCredential
    {
        public string Username { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        public TimeSpan ExpiresInTimeSpan => TimeSpan.FromSeconds(ExpiresIn);

        [JsonPropertyName("expires_on")]
        public long ExpiresOn { get; set; }

        public DateTimeOffset ExpiresOnDateTime
        {
            get => DateTimeOffset.FromUnixTimeSeconds(ExpiresOn);
            set => ExpiresOn = value.ToUnixTimeSeconds();
        }

        [JsonPropertyName("ext_expires_in")]
        public int ExtExpiresIn { get; set; }

        public TimeSpan ExtExpiresInTimeSpan
        {
            get => TimeSpan.FromSeconds(ExtExpiresIn);
            set => ExtExpiresIn = (int)value.TotalSeconds;
        }

        [JsonPropertyName("not_before")]
        public long NotBefore { get; set; }

        public DateTimeOffset NotBeforeDateTime
        {
            get => DateTimeOffset.FromUnixTimeSeconds(NotBefore);
            set => NotBefore = value.ToUnixTimeSeconds();
        }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("resource")]
        public string Resource { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }
}
