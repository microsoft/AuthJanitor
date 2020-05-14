// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Automation.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Shared
{
    public class AuthJanitorHttpClient : HttpClient
    {
        public const string HEADER_NAME = "AuthJanitor";
        public const string HEADER_VALUE = "administrator";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = false,
            IgnoreNullValues = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly Dictionary<Type, string> ApiFormatStrings = new Dictionary<Type, string>()
        {
            { typeof(DashboardMetricsViewModel), "dashboard" },
            { typeof(ManagedSecretViewModel), "managedSecrets" },
            { typeof(RekeyingTaskViewModel), "tasks" },
            { typeof(ResourceViewModel), "resources" },
            { typeof(LoadedProviderViewModel), "providers" },
            { typeof(ProviderConfigurationViewModel), "providers" }
        };

        public AuthJanitorHttpClient() : base()
        {
            SerializerOptions.Converters.Add(new TimeSpanConverter());
            if (!DefaultRequestHeaders.Contains(HEADER_NAME))
                DefaultRequestHeaders.Add(HEADER_NAME, HEADER_VALUE);
        }

        public AuthJanitorHttpClient(HttpClient client) : this()
        {
            BaseAddress = new Uri($"{client.BaseAddress}../api");
            Console.WriteLine($"Creating AuthJanitor REST API Client with BaseAddress {BaseAddress}");
            foreach (var item in client.DefaultRequestHeaders)
                DefaultRequestHeaders.Add(item.Key, item.Value);
            MaxResponseContentBufferSize = client.MaxResponseContentBufferSize;
            Timeout = client.Timeout;
        }

        public Task<T> AJGet<T>() where T : IAuthJanitorViewModel => this
            .AssertRequestIsSane<T>()
            .GetAsync($"{BaseAddress}/{ApiFormatStrings[typeof(T)]}")
            .ContinueWith(t => GetFromContentPayload<T>(t.Result))
            .Unwrap();

        public Task<T> AJGet<T>(string name) where T : IAuthJanitorViewModel => this
            .AssertRequestIsSane<T>()
            .GetAsync($"{BaseAddress}/{ApiFormatStrings[typeof(T)]}/{name}")
            .ContinueWith(t => GetFromContentPayload<T>(t.Result))
            .Unwrap();

        public Task<T> AJGet<T>(Guid objectId) where T : IAuthJanitorViewModel => this
            .AssertRequestIsSane<T>()
            .GetAsync($"{BaseAddress}/{ApiFormatStrings[typeof(T)]}/{objectId}")
            .ContinueWith(t => GetFromContentPayload<T>(t.Result))
            .Unwrap();

        public Task<IEnumerable<T>> AJList<T>() where T : IAuthJanitorViewModel => this
            .AssertRequestIsSane<T>()
            .GetAsync($"{BaseAddress}/{ApiFormatStrings[typeof(T)]}")
            .ContinueWith(t => GetFromContentPayload<IEnumerable<T>>(t.Result))
            .Unwrap();

        public Task<T> AJCreate<T>(T obj) where T : IAuthJanitorViewModel => this
            .AssertRequestIsSane<T>()
            .PostAsync($"{BaseAddress}/{ApiFormatStrings[typeof(T)]}", new StringContent(Serialize(obj)))
            .ContinueWith(t => GetFromContentPayload<T>(t.Result))
            .Unwrap();

        public Task<T> AJUpdate<T>(Guid objectId, T obj) where T : IAuthJanitorViewModel => this
            .AssertRequestIsSane<T>()
            .PostAsync($"{BaseAddress}/{ApiFormatStrings[typeof(T)]}/{objectId}", new StringContent(Serialize(obj)))
            .ContinueWith(t => GetFromContentPayload<T>(t.Result))
            .Unwrap();

        public Task AJDelete<T>(Guid objectId) where T : IAuthJanitorViewModel => this
            .AssertRequestIsSane<T>()
            .DeleteAsync($"{BaseAddress}/{ApiFormatStrings[typeof(T)]}/{objectId}")
            .ContinueWith(t => t.Result.EnsureSuccessStatusCode());

        private async Task<T> GetFromContentPayload<T>(HttpResponseMessage message) =>
            message.Content.Headers.ContentLength == 0 ? default :
            Deserialize<T>(await message.Content.ReadAsStringAsync());

        private AuthJanitorHttpClient AssertRequestIsSane<T>()
        {
            if (!ApiFormatStrings.ContainsKey(typeof(T)))
                throw new Exception("Unsupported data abstraction!");
            return this;
        }

        private string Serialize<T>(T obj) => JsonSerializer.Serialize(obj);
        private T Deserialize<T>(string str) => JsonSerializer.Deserialize<T>(str);
    }
}
