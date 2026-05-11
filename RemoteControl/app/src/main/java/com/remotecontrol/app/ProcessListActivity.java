package com.remotecontrol.app;

import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.View;
import android.widget.EditText;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import java.util.ArrayList;
import java.util.List;

public class ProcessListActivity extends AppCompatActivity
        implements SocketClient.SocketListener {

    private ProcessAdapter adapter;
    private ProgressBar    progressBar;
    private TextView       tvEmpty;
    private EditText       etSearch;

    // Cópia completa da lista para filtro local
    private List<SocketClient.ProcessInfo> listaCompleta = new ArrayList<>();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_process_list);

        if (getSupportActionBar() != null)
            getSupportActionBar().setTitle(R.string.title_processos);

        progressBar = findViewById(R.id.progressBar);
        tvEmpty     = findViewById(R.id.tvEmpty);
        etSearch    = findViewById(R.id.etSearch);

        RecyclerView recyclerView = findViewById(R.id.recyclerView);
        recyclerView.setLayoutManager(new LinearLayoutManager(this));

        adapter = new ProcessAdapter(this::onKillClicked);
        recyclerView.setAdapter(adapter);

        // Filtro de busca em tempo real
        etSearch.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {}
            @Override public void afterTextChanged(Editable s) {
                adapter.filter(s.toString(), listaCompleta);
            }
        });

        // Assume o listener do socket para receber os callbacks aqui
        MainActivity.socketClient.setListener(this);
        carregarProcessos();
    }

    private void carregarProcessos() {
        progressBar.setVisibility(View.VISIBLE);
        tvEmpty.setVisibility(View.GONE);
        MainActivity.socketClient.send(CommandBuilder.listProcesses());
    }

    private void onKillClicked(SocketClient.ProcessInfo processo) {
        new AlertDialog.Builder(this)
                .setTitle(R.string.kill_confirm_title)
                .setMessage(getString(R.string.kill_confirm_msg, processo.nome))
                .setPositiveButton(R.string.kill_yes, (d, w) -> {
                    MainActivity.socketClient.send(CommandBuilder.killProcess(processo.nome));
                    // Atualiza a lista após pequeno delay para o servidor processar
                    etSearch.postDelayed(this::carregarProcessos, 800);
                })
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    // ─── SOCKET LISTENER ─────────────────────────────────────────────────────

    @Override
    public void onProcessListReceived(List<SocketClient.ProcessInfo> processos) {
        progressBar.setVisibility(View.GONE);
        listaCompleta = processos;

        // Aplica o filtro atual se houver texto na busca
        String query = etSearch.getText().toString();
        adapter.filter(query, listaCompleta);

        tvEmpty.setVisibility(processos.isEmpty() ? View.VISIBLE : View.GONE);
    }

    @Override
    public void onMessageReceived(String json) {
        // Resposta do kill — mostra feedback
        try {
            com.google.gson.JsonObject obj = com.google.gson.JsonParser.parseString(json).getAsJsonObject();
            String msg = obj.has("msg") ? obj.get("msg").getAsString() : "";
            if (!msg.isEmpty()) Toast.makeText(this, msg, Toast.LENGTH_SHORT).show();
        } catch (Exception ignored) {}
    }

    @Override public void onConnected() {}
    @Override public void onScreenshotsReceived(List<SocketClient.ScreenshotCaptura> c) {}
    @Override public void onError(String message) {
        progressBar.setVisibility(View.GONE);
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
        // Devolve o listener para a MainActivity ao fechar
        if (MainActivity.getInstance() != null)
            MainActivity.socketClient.setListener(MainActivity.getInstance());
    }
}