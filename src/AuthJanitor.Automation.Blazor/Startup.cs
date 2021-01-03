// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using AuthJanitor.Repository;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using McMaster.NETCore.Plugins;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public delegate Task<AccessTokenCredential> GetToken(TokenSources src, string parameter);

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMicrosoftIdentityWebApiAuthentication(Configuration)
                .EnableTokenAcquisitionToCallDownstreamApi()
                .AddInMemoryTokenCaches();
            services.AddMicrosoftIdentityWebAppAuthentication(Configuration)
                .EnableTokenAcquisitionToCallDownstreamApi()
                .AddInMemoryTokenCaches();

            services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            }).AddMicrosoftIdentityUI();

            services.AddScoped<ITokenCredentialProvider, BlazorTokenCredentialProvider>();
            
            // NOTE: In this mode, the RSA data is ephemeral and cleared at shutdown
            services.AddAuthJanitorDummyServices();
            services.AddAuthJanitorService((file) =>
                PluginLoader.CreateFromAssemblyFile(file, AuthJanitorService.ProviderSharedTypes)
                            .LoadDefaultAssembly(),
                AuthJanitorService.AdminServiceAgentIdentity);
            services.AddDbContext<AuthJanitorDbContext>(ServiceLifetime.Singleton);

            services.AddRazorPages();
            services.AddServerSideBlazor()
                    .AddMicrosoftIdentityConsentHandler();

            services.AddBlazorise()
                    .AddBootstrapProviders()
                    .AddFontAwesomeIcons();

            services.AddHttpClient("AuthJanitorHttpClient", o =>
                o.BaseAddress = new Uri("https://localhost:44396"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.ApplicationServices
              .UseBootstrapProviders()
              .UseFontAwesomeIcons()
              .TryInitializeDatabase();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}

