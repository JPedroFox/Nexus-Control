package com.remotecontrol.app;

import android.content.Context;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

public class MouseKeyboardActivity extends AppCompatActivity
        implements SocketClient.SocketListener {

    private TouchpadView touchpad;
    private EditText     etKeyboard;
    private LinearLayout layoutKeyboard;
    private Button       btnToggleKeyboard;

    private String lastText = "";
    private boolean keyboardVisible = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_mouse_keyboard);

        if (getSupportActionBar() != null)
            getSupportActionBar().setTitle(R.string.title_mouse_keyboard);

        touchpad          = findViewById(R.id.touchpad);
        etKeyboard        = findViewById(R.id.etKeyboard);
        layoutKeyboard    = findViewById(R.id.layoutKeyboard);
        btnToggleKeyboard = findViewById(R.id.btnToggleKeyboard);

        setupTouchpad();
        setupKeyboard();
        setupMouseButtons();
        setupSpecialKeys();

        MainActivity.socketClient.setListener(this);
    }

    // ─── TOUCHPAD ─────────────────────────────────────────────────────────────

    private void setupTouchpad() {
        touchpad.setListener(new TouchpadView.TouchpadListener() {
            @Override public void onMove(int dx, int dy) {
                send(CommandBuilder.mouseMove(dx, dy));
            }
            @Override public void onLeftClick() {
                send(CommandBuilder.mouseLeftClick());
            }
            @Override public void onRightClick() {
                send(CommandBuilder.mouseRightClick());
            }
            @Override public void onScroll(int delta) {
                send(CommandBuilder.mouseScroll(delta));
            }
            @Override public void onLeftDown() {
                send(CommandBuilder.mouseLeftDown());
            }
            @Override public void onLeftUp() {
                send(CommandBuilder.mouseLeftUp());
            }
        });
    }

    // ─── BOTÕES DE MOUSE ──────────────────────────────────────────────────────

    private void setupMouseButtons() {
        findViewById(R.id.btnLeftClick).setOnClickListener(v ->
            send(CommandBuilder.mouseLeftClick()));

        findViewById(R.id.btnRightClick).setOnClickListener(v ->
            send(CommandBuilder.mouseRightClick()));

        findViewById(R.id.btnDoubleClick).setOnClickListener(v ->
            send(CommandBuilder.mouseDoubleClick()));
    }

    // ─── TECLADO ──────────────────────────────────────────────────────────────

    private void setupKeyboard() {
        btnToggleKeyboard.setOnClickListener(v -> toggleKeyboard());

        // TextWatcher detecta novos caracteres digitados
        etKeyboard.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {}

            @Override
            public void afterTextChanged(Editable s) {
                String current = s.toString();

                if (current.length() > lastText.length()) {
                    // Novo caractere adicionado
                    String newChars = current.substring(lastText.length());
                    send(CommandBuilder.keyboardText(newChars));

                } else if (current.length() < lastText.length()) {
                    // Backspace pressionado
                    send(CommandBuilder.keyboardKey("BACKSPACE"));
                }

                lastText = current;
            }
        });
    }

    private void toggleKeyboard() {
        keyboardVisible = !keyboardVisible;
        layoutKeyboard.setVisibility(keyboardVisible ? View.VISIBLE : View.GONE);
        btnToggleKeyboard.setText(keyboardVisible
            ? getString(R.string.btn_hide_keyboard)
            : getString(R.string.btn_show_keyboard));

        if (keyboardVisible) {
            etKeyboard.requestFocus();
            showSoftKeyboard();
        }
    }

    private void showSoftKeyboard() {
        InputMethodManager imm = (InputMethodManager)
            getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm != null)
            imm.showSoftInput(etKeyboard, InputMethodManager.SHOW_IMPLICIT);
    }

    // ─── TECLAS ESPECIAIS ─────────────────────────────────────────────────────

    private void setupSpecialKeys() {
        bindKey(R.id.keyEsc,      "ESC");
        bindKey(R.id.keyTab,      "TAB");
        bindKey(R.id.keyEnter,    "ENTER");
        bindKey(R.id.keyBackspace,"BACKSPACE");
        bindKey(R.id.keyUp,       "UP");
        bindKey(R.id.keyDown,     "DOWN");
        bindKey(R.id.keyLeft,     "LEFT");
        bindKey(R.id.keyRight,    "RIGHT");
        bindKey(R.id.keyHome,     "HOME");
        bindKey(R.id.keyEnd,      "END");
        bindKey(R.id.keyDelete,   "DELETE");
        bindKey(R.id.keyPageUp,   "PAGEUP");
        bindKey(R.id.keyPageDown, "PAGEDOWN");
        bindKey(R.id.keyWin,      "WIN");
    }

    private void bindKey(int viewId, String tecla) {
        Button btn = findViewById(viewId);
        if (btn != null) btn.setOnClickListener(v -> send(CommandBuilder.keyboardKey(tecla)));
    }

    // ─── SOCKET ───────────────────────────────────────────────────────────────

    private void send(String cmd) {
        if (MainActivity.socketClient != null)
            MainActivity.socketClient.send(cmd);
    }

    @Override public void onConnected() {}
    @Override public void onScreenshotsReceived(java.util.List<SocketClient.ScreenshotCaptura> c) {}
    @Override public void onProcessListReceived(java.util.List<SocketClient.ProcessInfo> p) {}
    @Override public void onMessageReceived(String json) {}
    @Override public void onError(String message) {
        Toast.makeText(this, message, Toast.LENGTH_SHORT).show();
    }
    @Override public void onDisconnected(String reason) {
        Toast.makeText(this, getString(R.string.disconnected, reason), Toast.LENGTH_SHORT).show();
        finish();
    }

    // ─── LIFECYCLE ────────────────────────────────────────────────────────────

    @Override
    protected void onDestroy() {
        super.onDestroy();
        MainActivity instance = MainActivity.getInstance();
        if (instance != null)
            MainActivity.socketClient.setListener(instance);
    }
    @Override
    public void onPinRequired() {
        // Não deve ocorrer aqui: autenticação é responsabilidade da MainActivity.
        // Se chegar, é porque houve reconexão inesperada — volta para a tela anterior.
        Toast.makeText(this, "Sessão expirada. Reconecte na tela principal.", Toast.LENGTH_LONG).show();
        finish();
    }

    @Override
    public void onAuthFailed() {
        Toast.makeText(this, "Autenticação falhou. Reconecte na tela principal.", Toast.LENGTH_LONG).show();
        finish();
    }
}
