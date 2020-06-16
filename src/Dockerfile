FROM mcr.microsoft.com/dotnet/core/sdk

ARG ENVIRONMENT

ADD . /src
WORKDIR /src

RUN echo "Building ${ENVIRONMENT}"
RUN dotnet build -c ${ENVIRONMENT}
RUN dotnet test
