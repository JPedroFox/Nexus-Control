package com.remotecontrol.app;

import android.os.Handler;
import android.os.Looper;
import android.util.Base64;
import android.util.Log;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.util.ArrayList;
import java.util.List;

public class SocketClient {

    private static final String TAG             = "SocketClient";
    private static final int    TIMEOUT_MS      = 5000;
    private static final int    RECONNECT_DELAY = 3000;
    private static final int    MAX_RECONNECTS  = 5;

    // Dados de uma captura individual de monitor
    public static class ScreenshotCaptura {
        public final int    monitor;
        public final boolean primaria;
        public final int    largura;
        public final int    altura;
        public final byte[] imageBytes;

        public ScreenshotCaptura(int monitor, boolean primaria, int largura, int altura, byte[] imageBytes) {
            this.monitor    = monitor;
            this.primaria   = primaria;
            this.largura    = largura;
            this.altura     = altura;
            this.imageBytes = imageBytes;
        }

        public String label() {
            return "Monitor " + monitor + (primaria ? " (Principal)" : "");
        }
    }

    public interface SocketListener {
        void onConnected();
        void onDisconnected(String reason);
        void onMessageReceived(String json);
        void onScreenshotsReceived(List<ScreenshotCaptura> capturas);
        void onProcessListReceived(List<ProcessInfo> processos);
        void onError(String message);
    }

    // Dados de um processo do Windows
    public static class ProcessInfo {
        public final String nome;
        public final int    pid;
        public final long   memoriaMb;
        public final String janela;

        public ProcessInfo(String nome, int pid, long memoriaMb, String janela) {
            this.nome      = nome;
            this.pid       = pid;
            this.memoriaMb = memoriaMb;
            this.janela    = janela;
        }
    }

    private final Handler uiHandler = new Handler(Looper.getMainLooper());

    private Socket         socket;
    private PrintWriter    writer;
    private BufferedReader reader;

    private String serverIp;
    private int    serverPort;
    private SocketListener listener; // não-final: ProcessListActivity pode assumir temporariamente

    private volatile boolean shouldReconnect = false;
    private int reconnectTries = 0;

    public SocketClient(SocketListener listener) {
        this.listener = listener;
    }

    /** Troca o listener em runtime — usado por ProcessListActivity */
    public void setListener(SocketListener listener) {
        this.listener = listener;
    }

    // ─── CONEXÃO ──────────────────────────────────────────────────────────────

    public void connect(String ip, int port) {
        this.serverIp        = ip;
        this.serverPort      = port;
        this.shouldReconnect = true;
        this.reconnectTries  = 0;
        new Thread(this::connectInternal, "SocketConnect").start();
    }

    private void connectInternal() {
        try {
            socket = new Socket();
            socket.connect(new InetSocketAddress(serverIp, serverPort), TIMEOUT_MS);
            socket.setSoTimeout(0);

            // Buffer maior para suportar JSON com múltiplos screenshots (pode ser vários MB)
            writer = new PrintWriter(socket.getOutputStream(), true);
            reader = new BufferedReader(new InputStreamReader(socket.getInputStream()), 1024 * 1024);

            reconnectTries = 0;
            uiHandler.post(listener::onConnected);
            listenLoop();
        } catch (IOException e) {
            uiHandler.post(() -> listener.onError("Falha ao conectar: " + e.getMessage()));
            tryReconnect();
        }
    }

    private void listenLoop() {
        try {
            String line;
            while ((line = reader.readLine()) != null) {
                final String msg = line.trim();
                if (msg.isEmpty()) continue;
                if (msg.contains("\"CONNECTED\"")) continue;

                if (msg.contains("\"capturas\"")) {
                    handleScreenshots(msg);
                } else if (msg.contains("\"PROCESS_LIST\"")) {
                    handleProcessList(msg);
                } else {
                    uiHandler.post(() -> listener.onMessageReceived(msg));
                }
            }
            uiHandler.post(() -> listener.onDisconnected("Servidor desconectou"));
            tryReconnect();
        } catch (IOException e) {
            if (shouldReconnect) {
                uiHandler.post(() -> listener.onDisconnected("Conexão perdida"));
                tryReconnect();
            }
        }
    }

    /**
     * Parseia o JSON com array de capturas e decodifica cada imagem Base64.
     * Formato esperado:
     * {"status":"OK","telas":2,"capturas":[{"monitor":1,"primaria":true,"largura":1920,"altura":1080,"dados":"..."},...]}"
     */
    private void handleScreenshots(String json) {
        try {
            JsonObject root     = JsonParser.parseString(json).getAsJsonObject();
            JsonArray  capturas = root.getAsJsonArray("capturas");

            List<ScreenshotCaptura> lista = new ArrayList<>();
            for (int i = 0; i < capturas.size(); i++) {
                JsonObject c = capturas.get(i).getAsJsonObject();
                byte[] imageBytes = Base64.decode(c.get("dados").getAsString(), Base64.DEFAULT);
                lista.add(new ScreenshotCaptura(
                        c.get("monitor").getAsInt(),
                        c.get("primaria").getAsBoolean(),
                        c.get("largura").getAsInt(),
                        c.get("altura").getAsInt(),
                        imageBytes
                ));
            }

            uiHandler.post(() -> listener.onScreenshotsReceived(lista));
        } catch (Exception e) {
            Log.e(TAG, "Erro ao parsear screenshots: " + e.getMessage());
        }
    }

    private void handleProcessList(String json) {
        try {
            JsonObject root      = JsonParser.parseString(json).getAsJsonObject();
            JsonArray  processos = root.getAsJsonArray("processos");

            List<ProcessInfo> lista = new ArrayList<>();
            for (int i = 0; i < processos.size(); i++) {
                JsonObject p = processos.get(i).getAsJsonObject();
                lista.add(new ProcessInfo(
                        p.get("nome").getAsString(),
                        p.get("pid").getAsInt(),
                        p.get("memoria_mb").getAsLong(),
                        p.has("janela") ? p.get("janela").getAsString() : ""
                ));
            }
            uiHandler.post(() -> listener.onProcessListReceived(lista));
        } catch (Exception e) {
            Log.e(TAG, "Erro ao parsear process list: " + e.getMessage());
        }
    }

    // ─── ENVIO ────────────────────────────────────────────────────────────────

    public void send(String jsonCommand) {
        if (!isConnected()) {
            uiHandler.post(() -> listener.onError("Não conectado"));
            return;
        }
        new Thread(() -> {
            if (writer != null) writer.println(jsonCommand);
        }, "SocketSend").start();
    }

    // ─── DESCONEXÃO ───────────────────────────────────────────────────────────

    public void disconnect() {
        shouldReconnect = false;
        try {
            if (socket != null && !socket.isClosed()) socket.close();
        } catch (IOException ignored) {}
        uiHandler.post(() -> listener.onDisconnected("Desconectado pelo usuário"));
    }

    // ─── RECONEXÃO ────────────────────────────────────────────────────────────

    private void tryReconnect() {
        if (!shouldReconnect) return;
        if (reconnectTries >= MAX_RECONNECTS) {
            uiHandler.post(() -> listener.onError("Máximo de tentativas atingido."));
            return;
        }
        reconnectTries++;
        int delay = RECONNECT_DELAY * reconnectTries;
        uiHandler.postDelayed(
                () -> new Thread(this::connectInternal, "SocketReconnect").start(),
                delay
        );
    }

    public boolean isConnected() {
        return socket != null && socket.isConnected() && !socket.isClosed();
    }
}