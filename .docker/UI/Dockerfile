FROM auth-janitor as build

ARG APP_ROOT
ARG ENVIRONMENT

WORKDIR /src/${APP_ROOT}
RUN dotnet publish --no-restore -c ${ENVIRONMENT} -o /app

FROM nginx:1.19.0

COPY --from=build /app/wwwroot /var/www/
COPY nginx.conf /etc/nginx/nginx.conf