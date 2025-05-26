# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["SignalRNotificationAPI.csproj", "."]
RUN dotnet restore "SignalRNotificationAPI.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src"
RUN dotnet build "SignalRNotificationAPI.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SignalRNotificationAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
WORKDIR /app

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published app
COPY --from=publish /app/publish .

# Expose port 80 for Azure Web App
EXPOSE 80

# Add health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "SignalRNotificationAPI.dll"]
