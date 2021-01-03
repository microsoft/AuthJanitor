// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor.Shared
{
    public abstract class EntityBasePage<TModel> : ComponentBase
        where TModel : new()
    {
        protected abstract string UrlBase { get; }
        protected abstract Func<TModel, Guid> GetId { get; }

        [Inject]
        protected IHttpClientFactory ClientFactory { get; set; }

        protected HttpClient Http => 
            ClientFactory.CreateClient("AuthJanitorHttpClient");

        public IEnumerable<TModel> ModelCollection { get; set; } = new List<TModel>();
        public TModel SelectedModel { get; set; } = new TModel();

        protected override Task OnInitializedAsync() => Load();

        protected async Task<IEnumerable<TModel>> Load()
        {
            ModelCollection = await Http.GetFromJsonAsync<IEnumerable<TModel>>(UrlBase);
            return ModelCollection;
        }

        protected Task Create() => Create(SelectedModel);
        protected Task Create(TModel model) =>
            Http.PostAsJsonAsync<TModel>(UrlBase, model);

        protected Task Update() => Update(SelectedModel);
        protected Task Update(TModel model) =>
            Http.PutAsJsonAsync<TModel>(UrlBase + $"/{GetId(model)}", model);

        protected Task Delete() => Delete(SelectedModel);
        protected Task Delete(TModel model) =>
            Http.DeleteAsync(UrlBase + $"/{GetId(model)}");

        protected Task Delete(Guid id) =>
            Http.DeleteAsync(UrlBase + $"/{id}");
    }
}
