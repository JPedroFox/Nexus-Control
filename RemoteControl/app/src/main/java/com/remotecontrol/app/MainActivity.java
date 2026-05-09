package com.remotecontrol.app;

import android.content.Intent;
import android.os.Bundle;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import java.util.ArrayList;
import java.util.List;

public class MainActivity extends AppCompatActivity implements SocketClient.SocketListener {

    // ─── VIEWS ────────────────────────────────────────────────────────────────
    private EditText etIp, etPort;
    private Button btnConnect;
    private TextView tvStatus;

    private Button btnVolUp, btnVolDown, btnMute;
    private Button btnPlay, btnNext, btnPrev;
    private Button btnScreenshot;
    private Button btnLock, btnRestart, btnShutdown;
    private Button btnProcessos;

    // ─── LÓGICA ───────────────────────────────────────────────────────────────
    private boolean isConnected = false;

    // Exposto estaticamente para ProcessListActivity reutilizar a conexão
    public static SocketClient socketClient;
    public static MainActivity instance;

    // Campos estáticos para passar listas de imagens ao ScreenshotViewer
    // (Intent não suporta byte arrays grandes — TransactionTooLargeException)
    public static List<byte[]>  pendingScreenshots = null;
    public static List<String>  pendingLabels      = null;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        bindViews();
        setupClickListeners();

        instance     = this;
        socketClient = new SocketClient(this);
        setControlsEnabled(false);
    }

    // ─── SETUP ────────────────────────────────────────────────────────────────

    private void bindViews() {
        etIp          = findViewById(R.id.etIp);
        etPort        = findViewById(R.id.etPort);
        btnConnect    = findViewById(R.id.btnConnect);
        tvStatus      = findViewById(R.id.tvStatus);

        btnVolUp      = findViewById(R.id.btnVolUp);
        btnVolDown    = findViewById(R.id.btnVolDown);
        btnMute       = findViewById(R.id.btnMute);

        btnPlay       = findViewById(R.id.btnPlay);
        btnNext       = findViewById(R.id.btnNext);
        btnPrev       = findViewById(R.id.btnPrev);

        btnScreenshot = findViewById(R.id.btnScreenshot);

        btnLock       = findViewById(R.id.btnLock);
        btnRestart    = findViewById(R.id.btnRestart);
        btnShutdown   = findViewById(R.id.btnShutdown);
        btnProcessos  = findViewById(R.id.btnProcessos);
    }

    private void setupClickListeners() {
        btnConnect.setOnClickListener(v    -> handleConnectToggle());

        btnVolUp.setOnClickListener(v      -> send(CommandBuilder.volumeUp()));
        btnVolDown.setOnClickListener(v    -> send(CommandBuilder.volumeDown()));
        btnMute.setOnClickListener(v       -> send(CommandBuilder.mute()));

        btnPlay.setOnClickListener(v       -> send(CommandBuilder.playPause()));
        btnNext.setOnClickListener(v       -> send(CommandBuilder.nextTrack()));
        btnPrev.setOnClickListener(v       -> send(CommandBuilder.prevTrack()));

        btnScreenshot.setOnClickListener(v -> send(CommandBuilder.screenshot()));

        btnLock.setOnClickListener(v       -> send(CommandBuilder.lock()));
        btnRestart.setOnClickListener(v    -> confirmAndSend("Reiniciar o PC?",  CommandBuilder.restart()));
        btnShutdown.setOnClickListener(v   -> confirmAndSend("Desligar o PC?",   CommandBuilder.shutdown()));
        btnProcessos.setOnClickListener(v  -> startActivity(new Intent(this, ProcessListActivity.class)));
    }

    // ─── CONEXÃO ──────────────────────────────────────────────────────────────

    private void handleConnectToggle() {
        if (isConnected) {
            socketClient.disconnect();
            return;
        }

        String ip   = etIp.getText().toString().trim();
        String port = etPort.getText().toString().trim();

        if (ip.isEmpty()) {
            showToast("Digite o IP do PC");
            return;
        }

        int portNumber = port.isEmpty() ? 8888 : Integer.parseInt(port);
        setStatus("Conectando em " + ip + ":" + portNumber + "...", false);
        socketClient.connect(ip, portNumber);
    }

    // ─── SOCKET LISTENER ─────────────────────────────────────────────────────

    @Override
    public void onConnected() {
        isConnected = true;
        setStatus("✅ Conectado", true);
        btnConnect.setText(R.string.btn_disconnect);
        setControlsEnabled(true);
    }

    @Override
    public void onDisconnected(String reason) {
        isConnected = false;
        setStatus("🔴 Desconectado: " + reason, false);
        btnConnect.setText(R.string.btn_connect);
        setControlsEnabled(false);
    }

    @Override
    public void onMessageReceived(String json) {
        try {
            JsonObject obj = JsonParser.parseString(json).getAsJsonObject();
            String status  = obj.get("status").getAsString();
            String msg     = obj.has("msg") ? obj.get("msg").getAsString() : "";

            if ("OK".equals(status)) {
                setStatus("✅ " + msg, true);
            } else {
                setStatus("⚠️ " + msg, false);
            }
        } catch (Exception e) {
            setStatus("Resposta: " + json, true);
        }
    }

    @Override
    public void onScreenshotsReceived(List<SocketClient.ScreenshotCaptura> capturas) {
        // Passa dados via campos estáticos — evita TransactionTooLargeException do Bundle
        pendingScreenshots = new ArrayList<>();
        pendingLabels      = new ArrayList<>();
        for (SocketClient.ScreenshotCaptura c : capturas) {
            pendingScreenshots.add(c.imageBytes);
            pendingLabels.add(c.label());
        }
        startActivity(new Intent(this, ScreenshotViewer.class));
        setStatus("📸 " + capturas.size() + " monitor(es) capturado(s)", true);
    }

    @Override
    public void onProcessListReceived(List<SocketClient.ProcessInfo> processos) {
        // Roteado para ProcessListActivity via setListener() — não chega aqui em uso normal
    }

    @Override
    public void onError(String message) {
        setStatus("❌ " + message, false);
        showToast(message);
    }

    // ─── HELPERS ──────────────────────────────────────────────────────────────

    private void send(String command) {
        if (!isConnected) {
            showToast("Não conectado");
            return;
        }
        socketClient.send(command);
    }

    private void confirmAndSend(String message, String command) {
        new androidx.appcompat.app.AlertDialog.Builder(this)
                .setTitle("Confirmar")
                .setMessage(message)
                .setPositiveButton("Sim", (d, w) -> send(command))
                .setNegativeButton("Cancelar", null)
                .show();
    }

    private void setStatus(String message, boolean ok) {
        tvStatus.setText(message);
        tvStatus.setTextColor(ok
                ? getColor(android.R.color.holo_green_dark)
                : getColor(android.R.color.holo_red_dark));
    }

    private void setControlsEnabled(boolean enabled) {
        btnVolUp.setEnabled(enabled);
        btnVolDown.setEnabled(enabled);
        btnMute.setEnabled(enabled);
        btnPlay.setEnabled(enabled);
        btnNext.setEnabled(enabled);
        btnPrev.setEnabled(enabled);
        btnScreenshot.setEnabled(enabled);
        btnLock.setEnabled(enabled);
        btnRestart.setEnabled(enabled);
        btnShutdown.setEnabled(enabled);
        btnProcessos.setEnabled(enabled);
    }

    private void showToast(String msg) {
        Toast.makeText(this, msg, Toast.LENGTH_SHORT).show();
    }

    // ─── LIFECYCLE ────────────────────────────────────────────────────────────

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (socketClient != null) socketClient.disconnect();
    }
}