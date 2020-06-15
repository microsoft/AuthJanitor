ENVIRONMENT?=Release
STORAGE_WEB_URL?=http://localhost:8080
SENDGRID_API_KEY?=my.sendgrid.api.key
CLIENT_ID?=authjanitor-admin-application-client-id
CLIENT_SECRET?=authjanitor-admin-application-client-secret
TENANT_ID?=authjanitor-admin-application-tenant-id

usage:
	@echo "USAGE aj-admin-api | aj-admin-ui | aj-agent | all-services | run | stop"

aj-admin-api: PROJECT_NAME = AuthJanitor.Automation.AdminApi
aj-admin-api: admin_api_build_publish service_admin_api

aj-admin-ui: PROJECT_NAME = AuthJanitor.Automation.AdminUi
aj-admin-ui: admin_ui_build_publish service_admin_ui

aj-agent: PROJECT_NAME = AuthJanitor.Automation.Agent
aj-agent: agent_build_publish service_agent

all-services: aj-admin-api aj-admin-ui aj-agent

admin_api_build_publish: dotnet_build dotnet_publish
admin_ui_build_publish: dotnet_build dotnet_publish
agent_build_publish: dotnet_build dotnet_publish

#################

define update_service_name		
	$(eval SERVICE_NAME=$(shell echo "$(1)" | tr '[:upper:]' '[:lower:]'))
endef

dotnet_build:
	dotnet build ./${PROJECT_NAME} -c ${ENVIRONMENT}
	dotnet test

dotnet_publish:
	dotnet publish -c ${ENVIRONMENT} ./${PROJECT_NAME} -o ./${PROJECT_NAME}/bin/${ENVIRONMENT}/publish

service_admin_api:
	$(call update_service_name,${PROJECT_NAME})
	docker build -t ${SERVICE_NAME} \
	--build-arg ENVIRONMENT=$(ENVIRONMENT) \
	--build-arg STORAGE_WEB_URL=$(STORAGE_WEB_URL) \
	--build-arg SENDGRID_API_KEY=$(SENDGRID_API_KEY) \
	--build-arg CLIENT_ID=$(CLIENT_ID) \
	--build-arg CLIENT_SECRET=$(CLIENT_SECRET) \
	--build-arg TENANT_ID=$(TENANT_ID) \
	./${PROJECT_NAME}/.

service_admin_ui:
	$(call update_service_name,${PROJECT_NAME})
	@docker build -t ${SERVICE_NAME} \
	--build-arg ENVIRONMENT=$(ENVIRONMENT) \
	./$(PROJECT_NAME)/.

service_agent:
	$(call update_service_name,${PROJECT_NAME})
	docker build -t ${SERVICE_NAME} \
	--build-arg ENVIRONMENT=$(ENVIRONMENT) \
	--build-arg SENDGRID_API_KEY=$(SENDGRID_API_KEY) \
	./${PROJECT_NAME}/.

run:
	docker-compose -f docker-compose.yaml up -d --remove-orphans

stop:
	docker-compose -f docker-compose.yaml down