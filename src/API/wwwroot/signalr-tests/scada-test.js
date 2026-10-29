(function () {
    "use strict";

    function inferApiBaseUrl() {
        if (!window.location.protocol.startsWith("http")) {
            return "https://localhost:51877";
        }

        const marker = "/signalr-tests/";
        const lowerPath = window.location.pathname.toLowerCase();
        const markerIndex = lowerPath.indexOf(marker);
        const pathBase = markerIndex >= 0
            ? window.location.pathname.substring(0, markerIndex)
            : "";

        return `${window.location.origin}${pathBase}`;
    }

    function normalizeBaseUrl(value) {
        return value.trim().replace(/\/+$/, "");
    }

    window.createScadaSignalRTester = function (options) {
        const apiBaseInput = document.getElementById("apiBaseUrl");
        const emailInput = document.getElementById("email");
        const passwordInput = document.getElementById("password");
        const tokenInput = document.getElementById("token");
        const factoryInput = document.getElementById("factoryId");
        const lineInput = document.getElementById("lineId");
        const loginButton = document.getElementById("loginButton");
        const connectButton = document.getElementById("connectButton");
        const disconnectButton = document.getElementById("disconnectButton");
        const statusElement = document.getElementById("connectionStatus");
        const statusText = document.getElementById("connectionStatusText");
        const statsElement = document.getElementById("messageStats");
        const outputElement = document.getElementById("output");

        let connection = null;
        let messageCount = 0;

        apiBaseInput.value = inferApiBaseUrl();

        function setStatus(state, message) {
            statusElement.className = `status ${state}`;
            statusText.textContent = message;
        }

        function showPayload(payload, source) {
            messageCount += 1;
            statsElement.textContent =
                `${messageCount} message(s) | ${source} | ${new Date().toLocaleString()}`;
            outputElement.textContent = JSON.stringify(payload, null, 2);
            if (typeof options.onPayload === "function") {
                options.onPayload(payload, source);
            }
        }

        function showError(error) {
            const message = error?.message || String(error);
            setStatus("error", message);
            outputElement.textContent = message;
        }

        function subscriptionArguments() {
            const factoryId = Number(factoryInput.value);
            if (!Number.isInteger(factoryId) || factoryId <= 0) {
                throw new Error("FactoryId must be a positive integer.");
            }

            const lineValue = lineInput.value.trim();
            if (!lineValue) {
                return [factoryId, null];
            }

            const lineId = Number(lineValue);
            if (!Number.isInteger(lineId) || lineId <= 0) {
                throw new Error("LineId must be a positive integer when supplied.");
            }

            return [factoryId, lineId];
        }

        async function subscribe() {
            const initialPayload = await connection.invoke(
                options.subscribeMethod,
                ...subscriptionArguments());
            showPayload(initialPayload, "initial subscription result");
        }

        loginButton.addEventListener("click", async function () {
            try {
                setStatus("connecting", "Signing in...");
                const apiBaseUrl = normalizeBaseUrl(apiBaseInput.value);
                const response = await fetch(`${apiBaseUrl}/v1/auth/login`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        email: emailInput.value.trim(),
                        password: passwordInput.value
                    })
                });
                const body = await response.json();
                if (!response.ok || !body.success || !body.data?.accessToken) {
                    throw new Error(
                        body?.error?.message || `Login failed with HTTP ${response.status}.`);
                }

                tokenInput.value = body.data.accessToken;
                setStatus("", "Token received. Press Connect.");
                outputElement.textContent = "Login completed successfully.";
            } catch (error) {
                showError(error);
            }
        });

        connectButton.addEventListener("click", async function () {
            try {
                if (typeof signalR === "undefined") {
                    throw new Error(
                        "The local SignalR browser client failed to load.");
                }

                const token = tokenInput.value.trim();
                if (!token) {
                    throw new Error("Login first or paste an access token.");
                }

                if (connection) {
                    await connection.stop();
                    connection = null;
                }

                messageCount = 0;
                const apiBaseUrl = normalizeBaseUrl(apiBaseInput.value);
                connection = new signalR.HubConnectionBuilder()
                    .withUrl(`${apiBaseUrl}/hubs/scada`, {
                        accessTokenFactory: () => tokenInput.value.trim(),
                        withCredentials: false
                    })
                    .withAutomaticReconnect([0, 2000, 5000, 10000])
                    .configureLogging(signalR.LogLevel.Information)
                    .build();

                connection.on(options.eventName, payload => {
                    showPayload(payload, options.eventName);
                });
                connection.onreconnecting(() => {
                    setStatus("connecting", "Reconnecting...");
                });
                connection.onreconnected(async () => {
                    setStatus("connected", "Connected");
                    try {
                        await subscribe();
                    } catch (error) {
                        showError(error);
                    }
                });
                connection.onclose(error => {
                    setStatus(error ? "error" : "", error?.message || "Disconnected");
                });

                setStatus("connecting", "Connecting...");
                await connection.start();
                setStatus("connected", "Connected");
                await subscribe();
            } catch (error) {
                showError(error);
            }
        });

        disconnectButton.addEventListener("click", async function () {
            try {
                if (!connection) {
                    setStatus("", "Disconnected");
                    return;
                }

                if (connection.state === signalR.HubConnectionState.Connected) {
                    await connection.invoke(options.unsubscribeMethod);
                }
                await connection.stop();
                connection = null;
                setStatus("", "Disconnected");
            } catch (error) {
                showError(error);
            }
        });
    };
})();
