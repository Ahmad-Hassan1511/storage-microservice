# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files for restore layer caching
COPY backend/StorageService.sln ./backend/
COPY backend/src/Storage.Domain/Storage.Domain.csproj ./backend/src/Storage.Domain/
COPY backend/src/Storage.Application/Storage.Application.csproj ./backend/src/Storage.Application/
COPY backend/src/Storage.Infrastructure.Persistence.SqlServer/Storage.Infrastructure.Persistence.SqlServer.csproj ./backend/src/Storage.Infrastructure.Persistence.SqlServer/
COPY backend/src/Storage.Infrastructure.Storage.Wasabi/Storage.Infrastructure.Storage.Wasabi.csproj ./backend/src/Storage.Infrastructure.Storage.Wasabi/
COPY backend/src/Storage.Infrastructure.Storage.AzureBlob/Storage.Infrastructure.Storage.AzureBlob.csproj ./backend/src/Storage.Infrastructure.Storage.AzureBlob/
COPY backend/src/Storage.Infrastructure.Storage.FileSystem/Storage.Infrastructure.Storage.FileSystem.csproj ./backend/src/Storage.Infrastructure.Storage.FileSystem/
COPY backend/src/Storage.Infrastructure.Cache.Redis/Storage.Infrastructure.Cache.Redis.csproj ./backend/src/Storage.Infrastructure.Cache.Redis/
COPY backend/src/Storage.Infrastructure.Cache.InMemory/Storage.Infrastructure.Cache.InMemory.csproj ./backend/src/Storage.Infrastructure.Cache.InMemory/
COPY backend/src/Storage.Infrastructure.Messaging.RabbitMQ/Storage.Infrastructure.Messaging.RabbitMQ.csproj ./backend/src/Storage.Infrastructure.Messaging.RabbitMQ/
COPY backend/src/Storage.Infrastructure.Messaging.AzureServiceBus/Storage.Infrastructure.Messaging.AzureServiceBus.csproj ./backend/src/Storage.Infrastructure.Messaging.AzureServiceBus/
COPY backend/src/Storage.Sdk/Storage.Sdk.csproj ./backend/src/Storage.Sdk/
COPY backend/src/Storage.Api/Storage.Api.csproj ./backend/src/Storage.Api/
COPY backend/src/Storage.Gateway/Storage.Gateway.csproj ./backend/src/Storage.Gateway/
COPY backend/Directory.Build.props ./backend/
COPY backend/Directory.Build.targets ./backend/

RUN dotnet restore backend/src/Storage.Api/Storage.Api.csproj

# Copy all source
COPY backend/ ./backend/

RUN dotnet publish backend/src/Storage.Api/Storage.Api.csproj -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Storage.Api.dll"]
