# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["SnowKingdomBackendAPI.RGS/SnowKingdomBackendAPI.RGS.csproj", "SnowKingdomBackendAPI.RGS/"]
COPY ["SnowKingdomBackendAPI.ApiService/SnowKingdomBackendAPI.ApiService.csproj", "SnowKingdomBackendAPI.ApiService/"]
COPY ["SnowKingdomBackendAPI.ServiceDefaults/SnowKingdomBackendAPI.ServiceDefaults.csproj", "SnowKingdomBackendAPI.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "SnowKingdomBackendAPI.RGS/SnowKingdomBackendAPI.RGS.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/SnowKingdomBackendAPI.RGS"
RUN dotnet build "SnowKingdomBackendAPI.RGS.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "SnowKingdomBackendAPI.RGS.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "SnowKingdomBackendAPI.RGS.dll"]
