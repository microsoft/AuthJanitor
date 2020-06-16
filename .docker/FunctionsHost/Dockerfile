FROM auth-janitor as build

ARG APP_ROOT
ARG ENVIRONMENT

WORKDIR /src/${APP_ROOT}
RUN dotnet publish --no-restore -c ${ENVIRONMENT} -o /app

# # #  --- Functions Runtime Image --- # # #
FROM mcr.microsoft.com/azure-functions/dotnet:3.0
ARG ENVIRONMENT

ENV Host--CORS=*
ENV FUNCTIONS_WORKER_RUNTIME=dotnet
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AZURE_FUNCTION_PROXY_DISABLE_LOCAL_CALL=True

# # #  --- Well Known Key and AccountName --- # # #
ENV AzureWebJobsStorage=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://storage:10000/devstoreaccount1;QueueEndpoint=http://storage:10001/devstoreaccount1

COPY --from=build /app /home/site/wwwroot