FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY backend/StorageService.sln ./backend/
COPY backend/src/Storage.Gateway/Storage.Gateway.csproj ./backend/src/Storage.Gateway/
COPY backend/Directory.Build.props ./backend/
COPY backend/Directory.Build.targets ./backend/

RUN dotnet restore backend/src/Storage.Gateway/Storage.Gateway.csproj

COPY backend/src/Storage.Gateway/ ./backend/src/Storage.Gateway/

RUN dotnet publish backend/src/Storage.Gateway/Storage.Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# ocelot.json and appsettings.json are baked into the image from build output
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Storage.Gateway.dll"]
