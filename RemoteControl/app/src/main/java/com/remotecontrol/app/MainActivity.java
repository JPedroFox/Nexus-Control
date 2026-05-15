package com.remotecontrol.app;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.text.InputFilter;
import android.text.InputType;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.chip.Chip;
import com.google.android.material.chip.ChipGroup;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import java.lang.ref.WeakReference;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedList;
import java.util.List;

public class MainActivity extends AppCompatActivity implements SocketClient.SocketListener {

    // ─── PREFS ────────────────────────────────────────────────────────────────
    private static final String PREFS_NAME     = "rc_prefs";
    private static final String KEY_RECENT_IPS = "recent_ips";
    private static final int    MAX_RECENT_IPS = 5;
    private static final String IP_SEPARATOR   = "||";

    // ─── VIEWS ────────────────────────────────────────────────────────────────
    private EditText etIp, etPort;
    private Button btnConnect;
    private TextView tvStatus;
    private LinearLayout layoutRecentIps;
    private ChipGroup chipGroupRecentIps;

    private Button btnVolUp, btnVolDown, btnMute;
    private Button btnPlay, btnNext, btnPrev;
    private Button btnSkipForward, btnSkipBackward;
    private Button btnScreenshot;
    private Button btnLock, btnRestart, btnShutdown;
    private Button btnProcessos, btnMouseKeyboard;

    // ─── ESTADO ───────────────────────────────────────────────────────────────
    private boolean isConnected = false;

    // Guardamos o PIN digitado para reconexões automáticas
    private String lastPin = null;

    public static SocketClient socketClient;

    private static WeakReference<MainActivity> instanceRef;
    public static MainActivity getInstance() {
        return instanceRef != null ? instanceRef.get() : null;
    }

    public static List<byte[]> pendingScreenshots = null;
    public static List<String> pendingLabels      = null;

    // ─── LIFECYCLE ────────────────────────────────────────────────────────────

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        instanceRef  = new WeakReference<>(this);
        socketClient = new SocketClient(this);

        bindViews();
        setupClickListeners();
        setControlsEnabled(false);
        updateRecentIpsUI();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (socketClient != null) socketClient.disconnect();
    }

    // ─── SETUP ────────────────────────────────────────────────────────────────

    private void bindViews() {
        etIp             = findViewById(R.id.etIp);
        etPort           = findViewById(R.id.etPort);
        btnConnect       = findViewById(R.id.btnConnect);
        tvStatus         = findViewById(R.id.tvStatus);
        layoutRecentIps  = findViewById(R.id.layoutRecentIps);
        chipGroupRecentIps = findViewById(R.id.chipGroupRecentIps);

        btnVolUp         = findViewById(R.id.btnVolUp);
        btnVolDown       = findViewById(R.id.btnVolDown);
        btnMute          = findViewById(R.id.btnMute);

        btnPlay          = findViewById(R.id.btnPlay);
        btnNext          = findViewById(R.id.btnNext);
        btnPrev          = findViewById(R.id.btnPrev);
        btnSkipForward   = findViewById(R.id.btnSkipForward);
        btnSkipBackward  = findViewById(R.id.btnSkipBackward);

        btnScreenshot    = findViewById(R.id.btnScreenshot);

        btnLock          = findViewById(R.id.btnLock);
        btnRestart       = findViewById(R.id.btnRestart);
        btnShutdown      = findViewById(R.id.btnShutdown);
        btnProcessos     = findViewById(R.id.btnProcessos);
        btnMouseKeyboard = findViewById(R.id.btnMouseKeyboard);
    }

    private void setupClickListeners() {
        btnConnect.setOnClickListener(v    -> handleConnectToggle());

        btnVolUp.setOnClickListener(v      -> send(CommandBuilder.volumeUp()));
        btnVolDown.setOnClickListener(v    -> send(CommandBuilder.volumeDown()));
        btnMute.setOnClickListener(v       -> send(CommandBuilder.mute()));

        btnPlay.setOnClickListener(v       -> send(CommandBuilder.playPause()));
        btnNext.setOnClickListener(v       -> send(CommandBuilder.nextTrack()));
        btnPrev.setOnClickListener(v       -> send(CommandBuilder.prevTrack()));
        btnSkipForward.setOnClickListener(v  -> send(CommandBuilder.skipForward()));
        btnSkipBackward.setOnClickListener(v -> send(CommandBuilder.skipBackward()));

        btnScreenshot.setOnClickListener(v -> send(CommandBuilder.screenshot()));

        btnLock.setOnClickListener(v       -> send(CommandBuilder.lock()));
        btnRestart.setOnClickListener(v    -> confirmAndSend("Reiniciar o PC?", CommandBuilder.restart()));
        btnShutdown.setOnClickListener(v   -> confirmAndSend("Desligar o PC?",  CommandBuilder.shutdown()));

        btnProcessos.setOnClickListener(v  ->
                startActivity(new Intent(this, ProcessListActivity.class)));
        btnMouseKeyboard.setOnClickListener(v ->
                startActivity(new Intent(this, MouseKeyboardActivity.class)));
    }

    // ─── CONEXÃO ──────────────────────────────────────────────────────────────

    private void handleConnectToggle() {
        if (isConnected) {
            lastPin = null;
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

        // Conecta passando o PIN já conhecido (pode ser null se for primeira vez)
        socketClient.connect(ip, portNumber, lastPin);
    }

    // ─── SOCKET LISTENER ─────────────────────────────────────────────────────

    @Override
    public void onConnected() {
        isConnected = true;
        saveRecentIp(etIp.getText().toString().trim());
        updateRecentIpsUI();
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

    /**
     * Servidor sinalizou que este dispositivo ainda não está autorizado.
     * Exibe um dialog para o usuário digitar o PIN exibido no PC.
     */
    @Override
    public void onPinRequired() {
        setStatus("🔐 PIN necessário...", false);
        showPinDialog();
    }

    /**
     * O PIN enviado foi recusado pelo servidor.
     */
    @Override
    public void onAuthFailed() {
        lastPin = null;
        setStatus("❌ PIN inválido. Conexão recusada.", false);
        showToast("PIN incorreto. Verifique o código exibido no PC.");
        btnConnect.setText(R.string.btn_connect);
        setControlsEnabled(false);
    }

    @Override
    public void onMessageReceived(String json) {
        try {
            JsonObject obj = JsonParser.parseString(json).getAsJsonObject();
            String status  = obj.get("status").getAsString();
            String msg     = obj.has("msg") ? obj.get("msg").getAsString() : "";
            setStatus("OK".equals(status) ? "✅ " + msg : "⚠️ " + msg, "OK".equals(status));
        } catch (Exception e) {
            setStatus("Resposta: " + json, true);
        }
    }

    @Override
    public void onScreenshotsReceived(List<SocketClient.ScreenshotCaptura> capturas) {
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
        // Roteado para ProcessListActivity via setListener()
    }

    @Override
    public void onError(String message) {
        setStatus("❌ " + message, false);
        showToast(message);
    }

    // ─── DIALOG DE PIN ────────────────────────────────────────────────────────

    /**
     * Dialog estilizado para entrada do PIN de 6 dígitos.
     * Ao confirmar, chama socketClient.submitPin() para autenticar.
     * Ao cancelar, encerra a conexão.
     */
    private void showPinDialog() {
        // Container do campo de PIN
        LinearLayout container = new LinearLayout(this);
        container.setOrientation(LinearLayout.VERTICAL);
        container.setPadding(64, 32, 64, 8);

        EditText etPin = new EditText(this);
        etPin.setHint("000000");
        etPin.setInputType(InputType.TYPE_CLASS_NUMBER);
        etPin.setFilters(new InputFilter[]{ new InputFilter.LengthFilter(6) });
        etPin.setTextSize(28f);
        etPin.setLetterSpacing(0.4f);
        etPin.setTextAlignment(TextView.TEXT_ALIGNMENT_CENTER);
        etPin.setTextColor(Color.WHITE);
        etPin.setHintTextColor(Color.GRAY);
        etPin.setBackgroundTintList(
                android.content.res.ColorStateList.valueOf(Color.parseColor("#00E5FF")));

        container.addView(etPin);

        new AlertDialog.Builder(this, R.style.PinDialogTheme)
                .setTitle("🔐 PIN de Autenticação")
                .setMessage("Digite o PIN de 6 dígitos exibido na janela do Nexus Control no PC.")
                .setView(container)
                .setCancelable(false)
                .setPositiveButton("Conectar", (dialog, which) -> {
                    String pin = etPin.getText().toString().trim();
                    if (pin.length() != 6) {
                        showToast("O PIN deve ter 6 dígitos.");
                        // Reabre o dialog se o PIN for inválido
                        onPinRequired();
                        return;
                    }
                    lastPin = pin;
                    setStatus("🔐 Autenticando...", false);
                    socketClient.submitPin(pin);
                })
                .setNegativeButton("Cancelar", (dialog, which) -> {
                    socketClient.disconnect();
                    setStatus("🔴 Conexão cancelada", false);
                })
                .show();
    }

    // ─── HELPERS ──────────────────────────────────────────────────────────────

    private void send(String command) {
        if (!isConnected) { showToast("Não conectado"); return; }
        socketClient.send(command);
    }

    private void confirmAndSend(String message, String command) {
        new AlertDialog.Builder(this)
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
        btnVolUp.setEnabled(enabled);         btnVolDown.setEnabled(enabled);
        btnMute.setEnabled(enabled);          btnPlay.setEnabled(enabled);
        btnNext.setEnabled(enabled);          btnPrev.setEnabled(enabled);
        btnSkipForward.setEnabled(enabled);   btnSkipBackward.setEnabled(enabled);
        btnScreenshot.setEnabled(enabled);    btnLock.setEnabled(enabled);
        btnRestart.setEnabled(enabled);       btnShutdown.setEnabled(enabled);
        btnProcessos.setEnabled(enabled);     btnMouseKeyboard.setEnabled(enabled);
    }

    private void showToast(String msg) {
        Toast.makeText(this, msg, Toast.LENGTH_SHORT).show();
    }

    // ─── IPs RECENTES ─────────────────────────────────────────────────────────

    private void saveRecentIp(String ip) {
        if (ip.isEmpty()) return;
        LinkedList<String> ips = new LinkedList<>(loadRecentIps());
        ips.remove(ip);
        ips.addFirst(ip);
        while (ips.size() > MAX_RECENT_IPS) ips.removeLast();

        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit()
                .putString(KEY_RECENT_IPS, String.join(IP_SEPARATOR, ips))
                .apply();
    }

    private List<String> loadRecentIps() {
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        String raw = prefs.getString(KEY_RECENT_IPS, "");
        if (raw.isEmpty()) return new ArrayList<>();
        return new ArrayList<>(Arrays.asList(raw.split("\\|\\|")));
    }

    private void updateRecentIpsUI() {
        List<String> ips = loadRecentIps();
        chipGroupRecentIps.removeAllViews();

        if (ips.isEmpty()) {
            layoutRecentIps.setVisibility(android.view.View.GONE);
            return;
        }

        layoutRecentIps.setVisibility(android.view.View.VISIBLE);

        for (String ip : ips) {
            Chip chip = new Chip(this);
            chip.setText(ip);
            chip.setTextColor(Color.parseColor("#00E5FF"));
            chip.setChipBackgroundColorResource(android.R.color.transparent);
            chip.setChipStrokeWidth(1f);
            chip.setChipStrokeColor(android.content.res.ColorStateList.valueOf(
                    Color.parseColor("#1A3A4A")));
            chip.setTextSize(11f);
            chip.setTypeface(android.graphics.Typeface.MONOSPACE);
            chip.setCloseIconVisible(true);
            chip.setCloseIconTint(android.content.res.ColorStateList.valueOf(
                    Color.parseColor("#334455")));

            chip.setOnClickListener(v -> {
                etIp.setText(ip);
                etIp.setSelection(ip.length());
            });

            chip.setOnCloseIconClickListener(v -> {
                SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
                List<String> current = new ArrayList<>(loadRecentIps());
                current.remove(ip);
                prefs.edit()
                        .putString(KEY_RECENT_IPS, String.join(IP_SEPARATOR, current))
                        .apply();
                updateRecentIpsUI();
            });

            chipGroupRecentIps.addView(chip);
        }
    }
}
