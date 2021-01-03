using AuthJanitor.Providers;
using AuthJanitor.SecureStorage;
using Microsoft.Identity.Web;
using System;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.Blazor
{
    public class BlazorTokenCredentialProvider : ITokenCredentialProvider
    {
        public const string AZURE_SCOPE = "https://management.core.windows.net/user_impersonation";
        private readonly ISecureStorage _secureStorage;
        private readonly ITokenAcquisition _tokenAcquisition;

        public BlazorTokenCredentialProvider(
            ISecureStorage secureStorage,
            ITokenAcquisition tokenAcquisition)
        {
            _secureStorage = secureStorage;
            _tokenAcquisition = tokenAcquisition;
        }

        public async Task<AccessTokenCredential> GetToken(TokenSources source, string parameters)
        {
            switch (source)
            {
                case TokenSources.Persisted:
                    var guid = Guid.Parse(parameters);
                    return await _secureStorage.Retrieve<AccessTokenCredential>(guid);
                case TokenSources.ServicePrincipal:
                    return AccessTokenCredential.CreateBearer(
                        await _tokenAcquisition.GetAccessTokenForAppAsync(AZURE_SCOPE));
                case TokenSources.OBO:
                    return AccessTokenCredential.CreateBearer(
                        await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { AZURE_SCOPE }));
            }
            return null;
        }
    }
}
