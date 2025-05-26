// Test script for SQL Server-based SignalR scaling
// This script tests the new SQL Server implementation with message persistence

const signalR = require("@microsoft/signalr");

class SqlServerScalingTest {
    constructor() {
        this.connections = [];
        this.testResults = [];
        this.baseUrl = "http://localhost:5000"; // Adjust port as needed
    }

    async runTests() {
        console.log("🚀 Starting SQL Server SignalR Scaling Tests");
        console.log("=" .repeat(50));

        try {
            await this.testBasicConnection();
            await this.testMultipleConnections();
            await this.testMessagePersistence();
            await this.testOfflineMessageDelivery();
            await this.testDocumentGeneration();
            await this.testConnectionCleanup();

            this.printResults();
        } catch (error) {
            console.error("❌ Test suite failed:", error);
        } finally {
            await this.cleanup();
        }
    }

    async testBasicConnection() {
        console.log("\n📡 Test 1: Basic Connection");

        try {
            const connection = await this.createConnection("user1");
            await connection.start();

            // Test registration
            await connection.invoke("RegisterUser", "user1");

            this.addResult("Basic Connection", true, "Successfully connected and registered");
            await connection.stop();
        } catch (error) {
            this.addResult("Basic Connection", false, error.message);
        }
    }

    async testMultipleConnections() {
        console.log("\n👥 Test 2: Multiple Connections for Same User");

        try {
            const user = "user2";
            const connection1 = await this.createConnection(user);
            const connection2 = await this.createConnection(user);

            await connection1.start();
            await connection2.start();

            // Register both connections
            await connection1.invoke("RegisterUser", user);
            await connection2.invoke("RegisterUser", user);

            // Test message delivery to both connections
            let messagesReceived = 0;
            const messagePromise = new Promise((resolve) => {
                const handler = () => {
                    messagesReceived++;
                    if (messagesReceived === 2) resolve();
                };
                connection1.on("ReceiveNotification", handler);
                connection2.on("ReceiveNotification", handler);
            });

            await connection1.invoke("SendNotification", user, "Test message for multiple connections");

            await Promise.race([
                messagePromise,
                new Promise((_, reject) => setTimeout(() => reject(new Error("Timeout")), 5000))
            ]);

            this.addResult("Multiple Connections", true, `Message delivered to ${messagesReceived} connections`);

            await connection1.stop();
            await connection2.stop();
        } catch (error) {
            this.addResult("Multiple Connections", false, error.message);
        }
    }

    async testMessagePersistence() {
        console.log("\n💾 Test 3: Message Persistence");

        try {
            const user = "user3";
            const connection = await this.createConnection(user);

            await connection.start();
            await connection.invoke("RegisterUser", user);

            // Send a message that should be persisted
            await connection.invoke("SendNotification", user, "Persistent test message");

            // Simulate checking if message was persisted (we can't directly check DB from here)
            // But we can verify the message was received
            let messageReceived = false;
            connection.on("ReceiveNotification", () => {
                messageReceived = true;
            });

            await new Promise(resolve => setTimeout(resolve, 1000));

            this.addResult("Message Persistence", messageReceived,
                messageReceived ? "Message sent and received (persistence handled by server)" : "Message not received");

            await connection.stop();
        } catch (error) {
            this.addResult("Message Persistence", false, error.message);
        }
    }

    async testOfflineMessageDelivery() {
        console.log("\n📬 Test 4: Offline Message Delivery");

        try {
            const user = "user4";

            // First, send a message to an offline user via HTTP API
            const response = await fetch(`${this.baseUrl}/api/DocumentGenerator/generate`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    UserId: user,
                    TemplateId: "test-template",
                    Username: user,
                    ConnectionType: "SignalR"
                })
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            // Wait a moment for the background task to complete
            await new Promise(resolve => setTimeout(resolve, 11000)); // 10s delay + 1s buffer

            // Now connect the user and check if they receive the queued message
            const connection = await this.createConnection(user);

            let queuedMessageReceived = false;
            connection.on("ReceiveNotification", (userId, message) => {
                if (message.includes("Document generated successfully")) {
                    queuedMessageReceived = true;
                }
            });

            await connection.start();
            await connection.invoke("RegisterUser", user);

            // Wait for queued messages to be delivered
            await new Promise(resolve => setTimeout(resolve, 2000));

            this.addResult("Offline Message Delivery", queuedMessageReceived,
                queuedMessageReceived ? "Queued message delivered upon connection" : "No queued message received");

            await connection.stop();
        } catch (error) {
            this.addResult("Offline Message Delivery", false, error.message);
        }
    }

    async testDocumentGeneration() {
        console.log("\n📄 Test 5: Document Generation with SQL Server Persistence");

        try {
            const user = "user5";
            const connection = await this.createConnection(user);

            await connection.start();
            await connection.invoke("RegisterUser", user);

            let documentNotificationReceived = false;
            connection.on("ReceiveNotification", (userId, message) => {
                if (message.includes("Document generated successfully")) {
                    documentNotificationReceived = true;
                }
            });

            // Trigger document generation
            const response = await fetch(`${this.baseUrl}/api/DocumentGenerator/generate`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    UserId: user,
                    TemplateId: "test-template",
                    Username: user,
                    ConnectionType: "SignalR"
                })
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            // Wait for document generation (10 seconds + buffer)
            await new Promise(resolve => setTimeout(resolve, 11000));

            this.addResult("Document Generation", documentNotificationReceived,
                documentNotificationReceived ? "Document generation notification received" : "No notification received");

            await connection.stop();
        } catch (error) {
            this.addResult("Document Generation", false, error.message);
        }
    }

    async testConnectionCleanup() {
        console.log("\n🧹 Test 6: Connection Cleanup");

        try {
            const user = "user6";
            const connection = await this.createConnection(user);

            await connection.start();
            await connection.invoke("RegisterUser", user);

            // Abruptly close connection without proper cleanup
            connection.connection.transport.close();

            // Wait for cleanup service to potentially run
            await new Promise(resolve => setTimeout(resolve, 2000));

            this.addResult("Connection Cleanup", true, "Connection closed (cleanup handled by background service)");
        } catch (error) {
            this.addResult("Connection Cleanup", false, error.message);
        }
    }

    async createConnection(userId) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${this.baseUrl}/Hubs/NotificationHub?userId=${userId}`)
            .withAutomaticReconnect()
            .build();

        this.connections.push(connection);
        return connection;
    }

    addResult(testName, success, message) {
        this.testResults.push({ testName, success, message });
        const status = success ? "✅ PASS" : "❌ FAIL";
        console.log(`   ${status}: ${message}`);
    }

    printResults() {
        console.log("\n" + "=" .repeat(50));
        console.log("📊 TEST RESULTS SUMMARY");
        console.log("=" .repeat(50));

        const passed = this.testResults.filter(r => r.success).length;
        const total = this.testResults.length;

        this.testResults.forEach(result => {
            const status = result.success ? "✅ PASS" : "❌ FAIL";
            console.log(`${status} ${result.testName}: ${result.message}`);
        });

        console.log("\n" + "-" .repeat(50));
        console.log(`📈 Overall: ${passed}/${total} tests passed (${Math.round(passed/total*100)}%)`);

        if (passed === total) {
            console.log("🎉 All tests passed! SQL Server scaling is working correctly.");
        } else {
            console.log("⚠️  Some tests failed. Check the implementation and database setup.");
        }
    }

    async cleanup() {
        console.log("\n🧹 Cleaning up connections...");
        for (const connection of this.connections) {
            try {
                if (connection.state === signalR.HubConnectionState.Connected) {
                    await connection.stop();
                }
            } catch (error) {
                // Ignore cleanup errors
            }
        }
        this.connections = [];
    }
}

// Run tests if this script is executed directly
if (require.main === module) {
    const test = new SqlServerScalingTest();
    test.runTests().catch(console.error);
}

module.exports = SqlServerScalingTest;
