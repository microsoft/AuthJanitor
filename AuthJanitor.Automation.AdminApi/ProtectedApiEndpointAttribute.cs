// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Automation.AdminApi
{
#pragma warning disable CS0618 // Type or member is obsolete
    public class ProtectedApiEndpointAttribute : FunctionInvocationFilterAttribute
#pragma warning restore CS0618 // Type or member is obsolete
    {
        private const string HEADER_NAME = "AuthJanitor";
        private const string HEADER_VALUE = "administrator";

#pragma warning disable CS0618 // Type or member is obsolete
        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            var request = (executingContext.Arguments.First(a => a.Value is HttpRequest)).Value as HttpRequest;

            if (!request.Headers.ContainsKey(HEADER_NAME) ||
                request.Headers[HEADER_NAME].First() != HEADER_VALUE)
            {
                executingContext.Logger.LogCritical("Client attempted to access an API function without appropriate headers!");
                throw new ValidationException("Invalid request!");
            }
            return base.OnExecutingAsync(executingContext, cancellationToken);
        }
    }
}
