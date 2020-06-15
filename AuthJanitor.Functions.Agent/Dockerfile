FROM mcr.microsoft.com/azure-functions/dotnet:3.0
ARG ENVIRONMENT=
ARG SENDGRID_API_KEY=
ENV Host--CORS=*
ENV FUNCTIONS_WORKER_RUNTIME=dotnet
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AZURE_FUNCTION_PROXY_DISABLE_LOCAL_CALL=True
ENV AzureWebJobsStorage=http://localhost
ENV SENDGRID_API_KEY=$SENDGRID_API_KEY
COPY /bin/$ENVIRONMENT/publish /home/site/wwwroot