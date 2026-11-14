# Multi-stage Dockerfile for PushpakClasses API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for optimized layer caching
COPY ["PushpakClasses.Domain/PushpakClasses.Domain.csproj", "PushpakClasses.Domain/"]
COPY ["PushpakClasses.Application/PushpakClasses.Application.csproj", "PushpakClasses.Application/"]
COPY ["PushpakClasses.Infrastructure/PushpakClasses.Infrastructure.csproj", "PushpakClasses.Infrastructure/"]
COPY ["PushpakClasses.Api/PushpakClasses.Api.csproj", "PushpakClasses.Api/"]

# Restore dependencies
RUN dotnet restore "PushpakClasses.Api/PushpakClasses.Api.csproj"

# Copy remaining source code
COPY . .

# Build and publish release artifacts
WORKDIR "/src/PushpakClasses.Api"
RUN dotnet publish "PushpakClasses.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for Docker healthchecks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Set ASP.NET Core environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PushpakClasses.Api.dll"]
