# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Briefed.sln", "./"]
COPY ["src/Briefed.Core/Briefed.Core.csproj", "src/Briefed.Core/"]
COPY ["src/Briefed.Infrastructure/Briefed.Infrastructure.csproj", "src/Briefed.Infrastructure/"]
COPY ["src/Briefed.Web/Briefed.Web.csproj", "src/Briefed.Web/"]

# Restore dependencies
RUN dotnet restore "Briefed.sln"

# Copy the rest of the source code
COPY . .

# Build the application
WORKDIR "/src/src/Briefed.Web"
RUN dotnet build "Briefed.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Briefed.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Briefed.Web.dll"]
