using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor
{
    public class TokenAbstractionService
    {
        private const string AZURE_SCOPE = "https://management.core.windows.net/user_impersonation";
        private const string GRAPH_SCOPE = "https://graph.windows.net/.default";
        private readonly ITokenAcquisition _tokenAcquisition;

        public TokenAbstractionService(
            ITokenAcquisition tokenAcquisition)
        {
            _tokenAcquisition = tokenAcquisition;
        }

        public async Task<AccessTokenCredential> GetAzureAsUser() =>
            AccessTokenCredential.CreateBearer(
                await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { AZURE_SCOPE }));

        public async Task<AccessTokenCredential> GetAzureAsApp() =>
            AccessTokenCredential.CreateBearer(
                await _tokenAcquisition.GetAccessTokenForAppAsync(AZURE_SCOPE));

        public async Task<AccessTokenCredential> GetGraphAsUser() =>
            AccessTokenCredential.CreateBearer(
                await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { GRAPH_SCOPE }));

        public async Task<AccessTokenCredential> GetGraphAsApp() =>
            AccessTokenCredential.CreateBearer(
                await _tokenAcquisition.GetAccessTokenForAppAsync(GRAPH_SCOPE));
    }
}
