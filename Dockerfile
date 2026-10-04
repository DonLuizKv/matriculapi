# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY parcial.csproj ./
RUN dotnet restore parcial.csproj

COPY . .
RUN dotnet publish parcial.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_NOLOGO=1

EXPOSE 8080

COPY --from=build --chown=$APP_UID /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "parcial.dll"]