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

    // ─── STATE ────────────────────────────────────────────────────────────────
    private boolean isConnected = false;

    // Saved PIN for automatic reconnections
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
        etIp               = findViewById(R.id.etIp);
        etPort             = findViewById(R.id.etPort);
        btnConnect         = findViewById(R.id.btnConnect);
        tvStatus           = findViewById(R.id.tvStatus);
        layoutRecentIps    = findViewById(R.id.layoutRecentIps);
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
        btnConnect.setOnClickListener(v      -> handleConnectToggle());

        btnVolUp.setOnClickListener(v        -> send(CommandBuilder.volumeUp()));
        btnVolDown.setOnClickListener(v      -> send(CommandBuilder.volumeDown()));
        btnMute.setOnClickListener(v         -> send(CommandBuilder.mute()));

        btnPlay.setOnClickListener(v         -> send(CommandBuilder.playPause()));
        btnNext.setOnClickListener(v         -> send(CommandBuilder.nextTrack()));
        btnPrev.setOnClickListener(v         -> send(CommandBuilder.prevTrack()));
        btnSkipForward.setOnClickListener(v  -> send(CommandBuilder.skipForward()));
        btnSkipBackward.setOnClickListener(v -> send(CommandBuilder.skipBackward()));

        btnScreenshot.setOnClickListener(v   -> send(CommandBuilder.screenshot()));

        btnLock.setOnClickListener(v         -> send(CommandBuilder.lock()));
        btnRestart.setOnClickListener(v      -> confirmRestart());
        btnShutdown.setOnClickListener(v     -> confirmShutdown());

        btnProcessos.setOnClickListener(v    ->
                startActivity(new Intent(this, ProcessListActivity.class)));
        btnMouseKeyboard.setOnClickListener(v ->
                startActivity(new Intent(this, MouseKeyboardActivity.class)));
    }

    // ─── CONNECTION ───────────────────────────────────────────────────────────

    private void handleConnectToggle() {
        if (isConnected) {
            lastPin = null;
            socketClient.disconnect();
            return;
        }

        String ip   = etIp.getText().toString().trim();
        String port = etPort.getText().toString().trim();

        if (ip.isEmpty()) {
            showToast("Enter the PC's IP address");
            return;
        }

        int portNumber = port.isEmpty() ? 8888 : Integer.parseInt(port);
        setStatus("● Connecting to " + ip + ":" + portNumber + "…", false);

        // Connect passing the already-known PIN (may be null on first attempt)
        socketClient.connect(ip, portNumber, lastPin);
    }

    // ─── SOCKET LISTENER ─────────────────────────────────────────────────────

    @Override
    public void onConnected() {
        isConnected = true;
        saveRecentIp(etIp.getText().toString().trim());
        updateRecentIpsUI();
        setStatus("✅ Connected", true);
        btnConnect.setText(R.string.btn_disconnect);
        setControlsEnabled(true);
    }

    @Override
    public void onDisconnected(String reason) {
        isConnected = false;
        setStatus("🔴 Disconnected: " + reason, false);
        btnConnect.setText(R.string.btn_connect);
        setControlsEnabled(false);
    }

    /** Server signalled that this device is not yet authorised — show PIN dialog. */
    @Override
    public void onPinRequired() {
        setStatus("🔐 PIN required…", false);
        showPinDialog();
    }

    /** The PIN sent was rejected by the server. */
    @Override
    public void onAuthFailed() {
        lastPin = null;
        setStatus("❌ Invalid PIN. Connection refused.", false);
        showToast("Incorrect PIN. Check the code shown on the PC.");
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
            setStatus("Response: " + json, true);
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
        setStatus("📸 " + capturas.size() + " monitor(s) captured", true);
    }

    @Override
    public void onProcessListReceived(List<SocketClient.ProcessInfo> processos) {
        // Routed to ProcessListActivity via setListener() — not expected here
    }

    @Override
    public void onError(String message) {
        setStatus("❌ " + message, false);
        showToast(message);
    }

    // ─── PIN DIALOG ───────────────────────────────────────────────────────────

    /**
     * Styled dialog for entering the 6-digit PIN displayed on the PC.
     * On confirm → calls socketClient.submitPin().
     * On cancel  → disconnects.
     */
    private void showPinDialog() {
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
                .setTitle("🔐 Authentication PIN")
                .setMessage("Enter the 6-digit PIN shown in the Nexus Control window on your PC.")
                .setView(container)
                .setCancelable(false)
                .setPositiveButton("Connect", (dialog, which) -> {
                    String pin = etPin.getText().toString().trim();
                    if (pin.length() != 6) {
                        showToast("PIN must be 6 digits.");
                        // Re-open the dialog if the PIN is invalid
                        onPinRequired();
                        return;
                    }
                    lastPin = pin;
                    setStatus("🔐 Authenticating…", false);
                    socketClient.submitPin(pin);
                })
                .setNegativeButton("Cancel", (dialog, which) -> {
                    socketClient.disconnect();
                    setStatus("🔴 Connection cancelled", false);
                })
                .show();
    }

    // ─── CONFIRMATION DIALOGS ─────────────────────────────────────────────────

    /**
     * Asks the user to confirm before sending a restart command.
     * The PC will reboot after a 5-second delay (as defined in the server).
     */
    private void confirmRestart() {
        new AlertDialog.Builder(this)
                .setTitle("🔄 Restart PC")
                .setMessage("Are you sure you want to restart the PC?\n\nThe system will reboot in 5 seconds.")
                .setIcon(android.R.drawable.ic_dialog_alert)
                .setPositiveButton("Yes, restart", (d, w) -> send(CommandBuilder.restart()))
                .setNegativeButton("Cancel", null)
                .show();
    }

    /**
     * Asks the user to confirm before sending a shutdown command.
     * The PC will power off after a 5-second delay (as defined in the server).
     */
    private void confirmShutdown() {
        new AlertDialog.Builder(this)
                .setTitle("⛔ Shut Down PC")
                .setMessage("Are you sure you want to shut down the PC?\n\nThe system will power off in 5 seconds.\nMake sure all work is saved.")
                .setIcon(android.R.drawable.ic_dialog_alert)
                .setPositiveButton("Yes, shut down", (d, w) -> send(CommandBuilder.shutdown()))
                .setNegativeButton("Cancel", null)
                .show();
    }

    // ─── HELPERS ──────────────────────────────────────────────────────────────

    private void send(String command) {
        if (!isConnected) { showToast("Not connected"); return; }
        socketClient.send(command);
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

    // ─── RECENT IPs ───────────────────────────────────────────────────────────

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