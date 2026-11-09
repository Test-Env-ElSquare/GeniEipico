(function () {
    "use strict";

    const hubPath = "/hubs/alerts";
    const eventName = "ReceiveAlert";

    const apiBaseInput = document.getElementById("apiBaseUrl");
    const emailInput = document.getElementById("email");
    const passwordInput = document.getElementById("password");
    const tokenInput = document.getElementById("token");
    const loginButton = document.getElementById("loginButton");
    const connectButton = document.getElementById("connectButton");
    const disconnectButton = document.getElementById("disconnectButton");
    const sampleButton = document.getElementById("sampleButton");
    const clearButton = document.getElementById("clearButton");
    const statusElement = document.getElementById("connectionStatus");
    const statusText = document.getElementById("connectionStatusText");
    const statsElement = document.getElementById("messageStats");
    const connectionMeta = document.getElementById("connectionMeta");
    const alertsElement = document.getElementById("alerts");
    const outputElement = document.getElementById("output");

    let connection = null;
    let messageCount = 0;

    apiBaseInput.value = inferApiBaseUrl();

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

    function setStatus(state, message) {
        statusElement.className = `status ${state}`;
        statusText.textContent = message;
    }

    function setConnectionId(value) {
        connectionMeta.textContent = `Connection ID: ${value || "—"}`;
    }

    function showError(error) {
        const message = error?.message || String(error);
        setStatus("error", message);
        outputElement.textContent = message;
    }

    function severityName(value) {
        return ({ 1: "Low", 2: "Medium", 3: "High", 4: "Critical" })[value]
            || `Unknown (${value ?? "null"})`;
    }

    function formatDate(value) {
        if (!value) return "No date";
        const parsed = new Date(value);
        return Number.isNaN(parsed.getTime()) ? String(value) : parsed.toLocaleString();
    }

    function addMeta(container, text) {
        const item = document.createElement("span");
        item.textContent = text;
        container.append(item);
    }

    function renderAlert(payload, source) {
        messageCount += 1;
        statsElement.textContent =
            `${messageCount} message(s) · ${source} · ${new Date().toLocaleString()}`;
        outputElement.textContent = JSON.stringify(payload, null, 2);

        if (messageCount === 1) alertsElement.replaceChildren();

        const card = document.createElement("article");
        const severity = Number(payload?.severity || 0);
        card.className = `alert-card severity-${severity}`;

        const top = document.createElement("div");
        top.className = "alert-top";

        const title = document.createElement("div");
        title.className = "alert-title";
        title.textContent = payload?.title || "Untitled alert";

        const badge = document.createElement("span");
        badge.className = "severity";
        badge.textContent = severityName(severity);
        top.append(title, badge);

        const message = document.createElement("div");
        message.className = "alert-message";
        message.textContent = payload?.message || "No message";

        const meta = document.createElement("div");
        meta.className = "alert-meta";
        addMeta(meta, `Alert #${payload?.id ?? "—"}`);
        addMeta(meta, `Type ${payload?.type ?? "—"}`);
        addMeta(meta, formatDate(payload?.createdAtUtc));
        if (payload?.ticketId != null) addMeta(meta, `Ticket #${payload.ticketId}`);

        card.append(top, message, meta);
        alertsElement.prepend(card);
    }

    async function stopConnection() {
        if (!connection) return;
        await connection.stop();
        connection = null;
        setConnectionId(null);
    }

    loginButton.addEventListener("click", async function () {
        try {
            setStatus("connecting", "Signing in...");
            const apiBaseUrl = normalizeBaseUrl(apiBaseInput.value);
            if (!apiBaseUrl) throw new Error("API base URL is required.");

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
                throw new Error("The local SignalR browser client failed to load.");
            }

            const token = tokenInput.value.trim();
            if (!token) throw new Error("Login first or paste an access token.");

            await stopConnection();
            const apiBaseUrl = normalizeBaseUrl(apiBaseInput.value);
            if (!apiBaseUrl) throw new Error("API base URL is required.");

            connection = new signalR.HubConnectionBuilder()
                .withUrl(`${apiBaseUrl}${hubPath}`, {
                    accessTokenFactory: () => tokenInput.value.trim(),
                    withCredentials: false
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            connection.on(eventName, payload => renderAlert(payload, eventName));
            connection.onreconnecting(error => {
                setStatus("connecting", error?.message || "Reconnecting...");
                setConnectionId(null);
            });
            connection.onreconnected(connectionId => {
                setStatus("connected", "Connected");
                setConnectionId(connectionId);
            });
            connection.onclose(error => {
                setStatus(error ? "error" : "", error?.message || "Disconnected");
                setConnectionId(null);
            });

            setStatus("connecting", "Connecting...");
            await connection.start();
            setStatus("connected", "Connected — waiting for ReceiveAlert");
            setConnectionId(connection.connectionId);
            outputElement.textContent = "Connected successfully. Waiting for a new Genie alarm.";
        } catch (error) {
            showError(error);
        }
    });

    disconnectButton.addEventListener("click", async function () {
        try {
            await stopConnection();
            setStatus("", "Disconnected");
        } catch (error) {
            showError(error);
        }
    });

    clearButton.addEventListener("click", function () {
        messageCount = 0;
        statsElement.textContent = "0 messages";
        outputElement.textContent = "Waiting for ReceiveAlert...";
        const empty = document.createElement("div");
        empty.className = "empty";
        empty.textContent = "Waiting for ReceiveAlert...";
        alertsElement.replaceChildren(empty);
    });

    sampleButton.addEventListener("click", function () {
        renderAlert({
            id: 1001,
            title: "Sample high voltage alert",
            message: "This is a browser-only sample used to verify payload rendering.",
            type: 1,
            severity: 4,
            isRead: false,
            createdAtUtc: new Date().toISOString(),
            readAtUtc: null,
            alarmLogId: 1001,
            ticketId: null
        }, "browser sample — not SignalR");
    });
})();
