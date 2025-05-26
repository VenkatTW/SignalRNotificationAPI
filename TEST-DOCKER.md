# Docker Image Testing Guide

This guide shows you how to test your containerized SignalR API locally before deploying to Azure.

## 🧪 Testing Options

### Option 1: Test with Docker Compose (Recommended)
This uses Azure SQL Database for complete testing (no local SQL Server needed).

```bash
cd SignalRNotificationAPI

# Start the API service (connects to Azure SQL Database)
docker-compose up --build

# Or run in detached mode
docker-compose up -d --build
```

**Access Points:**
- API: http://localhost:8080
- Health Check: http://localhost:8080/health
- Swagger: http://localhost:8080/swagger
- SignalR Hub: ws://localhost:8080/Hubs/NotificationHub

### Option 2: Test with Custom Azure SQL Database Connection

```bash
cd SignalRNotificationAPI

# Build the image
docker build -t signalr-test .

# Run with your Azure SQL connection string
docker run -d \
  --name signalr-test-container \
  -p 8080:80 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="YOUR_AZURE_SQL_CONNECTION_STRING" \
  -e ConnectionStrings__SignalRBackplane="YOUR_AZURE_SQL_CONNECTION_STRING" \
  -e CorsSettings__AngularAppUrl="http://localhost:4200" \
  signalr-test
```

## 🔍 Testing Steps

### 1. Basic Health Check
```bash
# Test if the container is running and healthy
curl http://localhost:8080/health

# Expected response: "Healthy" with 200 status code
```

### 2. API Endpoints Test
```bash
# Test if the API is responding
curl http://localhost:8080/api/DocumentGenerator/test

# Or check Swagger documentation
open http://localhost:8080/swagger
```

### 3. SignalR Hub Test

Create a simple HTML test file:

```html
<!DOCTYPE html>
<html>
<head>
    <title>SignalR Test</title>
    <script src="https://unpkg.com/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
</head>
<body>
    <h1>SignalR Connection Test</h1>
    <div id="status">Connecting...</div>
    <button onclick="sendMessage()">Send Test Message</button>
    <div id="messages"></div>

    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:8080/Hubs/NotificationHub")
            .build();

        connection.start().then(function () {
            document.getElementById("status").innerHTML = "Connected!";
            console.log("SignalR Connected");
        }).catch(function (err) {
            document.getElementById("status").innerHTML = "Connection Failed: " + err;
            console.error(err.toString());
        });

        connection.on("ReceiveNotification", function (message) {
            const div = document.createElement("div");
            div.innerHTML = "Received: " + message;
            document.getElementById("messages").appendChild(div);
        });

        function sendMessage() {
            connection.invoke("SendNotification", "Test message from browser")
                .catch(function (err) {
                    console.error(err.toString());
                });
        }
    </script>
</body>
</html>
```

Save as `test-signalr.html` and open in your browser.

### 4. Database Connection Test

Check if migrations ran successfully:

```bash
# View container logs
docker logs signalr-test-container

# Look for migration messages like:
# "Applying migration '20250525142252_InitialSignalRScaling'"
# "Done."
```

### 5. WebSocket Test

```bash
# Test WebSocket endpoint
curl -i -N \
  -H "Connection: Upgrade" \
  -H "Upgrade: websocket" \
  -H "Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==" \
  -H "Sec-WebSocket-Version: 13" \
  http://localhost:8080/ws?userId=testuser
```

## 🐛 Troubleshooting

### Container Won't Start

```bash
# Check container status
docker ps -a

# View container logs
docker logs signalr-test-container

# Common issues:
# - Port 8080 already in use: Change to different port (-p 8081:80)
# - Azure SQL Database connection: Check firewall settings and connection string format
```

### Azure SQL Database Connection Issues

```bash
# Ensure Azure SQL Database allows public network access
# Check Azure Portal → SQL databases → signalrspike → Networking
# Verify firewall rules include your IP address

# Test connection with updated connection string
docker run -d \
  --name signalr-test-azure \
  -p 8080:80 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Server=tcp:signalrspike.database.windows.net,1433;Initial Catalog=SignalRPOC;Persist Security Info=False;User ID=admin_poc;Password=Welcome@123;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" \
  signalr-test
```

### CORS Issues

If testing with a frontend:

```bash
# Run with specific CORS settings
docker run -d \
  --name signalr-test-cors \
  -p 8080:80 \
  -e CorsSettings__AngularAppUrl="http://localhost:3000,http://localhost:4200" \
  signalr-test
```

## 📊 Performance Testing

### Load Test SignalR Connections

```javascript
// Node.js script to test multiple connections
const signalR = require("@microsoft/signalr");

async function testConnections(count) {
    const connections = [];
    
    for (let i = 0; i < count; i++) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:8080/Hubs/NotificationHub")
            .build();
            
        connections.push(connection);
        
        try {
            await connection.start();
            console.log(`Connection ${i + 1} established`);
        } catch (err) {
            console.error(`Connection ${i + 1} failed:`, err);
        }
    }
    
    console.log(`${connections.length} connections established`);
}

// Test with 10 concurrent connections
testConnections(10);
```

## 🧹 Cleanup

```bash
# Stop and remove test containers
docker stop signalr-test-container
docker rm signalr-test-container

# Or if using docker-compose
docker-compose down

# Remove test image
docker rmi signalr-test

# Clean up unused images and containers
docker system prune
```

## ✅ Pre-Deployment Checklist

Before pushing to Docker Hub and deploying to Azure:

- [ ] Health check returns "Healthy"
- [ ] Swagger UI loads correctly
- [ ] SignalR hub accepts connections
- [ ] Azure SQL Database migrations run successfully
- [ ] CORS settings work with your frontend
- [ ] WebSocket connections work
- [ ] No error logs in container output
- [ ] Azure SQL Database firewall allows application access

## 🚀 Ready for Deployment

Once all tests pass, you're ready to:

1. Push to Docker Hub: `docker push YOUR_USERNAME/signalr-notification-api:latest`
2. Deploy to Azure Web App using the DEPLOY-DOCKERHUB.md guide

Your Docker image is tested and ready for production deployment with Azure SQL Database!
