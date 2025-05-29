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

# Copy published app
COPY --from=publish /app/publish .

# Expose port 80 for Azure Web App
EXPOSE 80

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80

# Run as root for Azure Web Apps (Azure handles security)
ENTRYPOINT ["dotnet", "SignalRNotificationAPI.dll"]
