# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY FitLife.sln ./
COPY FitLife.Api/FitLife.Api.csproj FitLife.Api/
COPY FitLife.Core/FitLife.Core.csproj FitLife.Core/
COPY FitLife.Infrastructure/FitLife.Infrastructure.csproj FitLife.Infrastructure/
COPY FitLife.Tests/FitLife.Tests.csproj FitLife.Tests/

# Restore dependencies
RUN dotnet restore FitLife.Api/FitLife.Api.csproj

# Copy everything else and build
COPY . .
WORKDIR /src/FitLife.Api
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r fitlife && useradd -r -g fitlife fitlife
USER fitlife

COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "FitLife.Api.dll"]
