FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, in its own layer, so code changes do not re-download packages.
COPY src/Expresso.Api/Expresso.Api.csproj src/Expresso.Api/
RUN dotnet restore src/Expresso.Api/Expresso.Api.csproj

COPY src/ src/
RUN dotnet publish src/Expresso.Api/Expresso.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The base image listens on 8080 and ships a non-root "app" user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Expresso.Api.dll"]
