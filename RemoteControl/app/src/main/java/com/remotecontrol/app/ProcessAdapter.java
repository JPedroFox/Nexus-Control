package com.remotecontrol.app;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import java.util.ArrayList;
import java.util.List;

public class ProcessAdapter extends RecyclerView.Adapter<ProcessAdapter.ViewHolder> {

    public interface OnKillClickListener {
        void onKill(SocketClient.ProcessInfo processo);
    }

    private List<SocketClient.ProcessInfo> lista = new ArrayList<>();
    private final OnKillClickListener killListener;

    public ProcessAdapter(OnKillClickListener killListener) {
        this.killListener = killListener;
    }

    public void setData(List<SocketClient.ProcessInfo> novaLista) {
        this.lista = novaLista;
        notifyDataSetChanged();
    }

    /** Filtra a lista por nome (busca case-insensitive) */
    public void filter(String query, List<SocketClient.ProcessInfo> listaOriginal) {
        if (query.isEmpty()) {
            lista = new ArrayList<>(listaOriginal);
        } else {
            String q = query.toLowerCase();
            lista = new ArrayList<>();
            for (SocketClient.ProcessInfo p : listaOriginal) {
                if (p.nome.toLowerCase().contains(q)) lista.add(p);
            }
        }
        notifyDataSetChanged();
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        View view = LayoutInflater.from(parent.getContext())
                .inflate(R.layout.item_process, parent, false);
        return new ViewHolder(view);
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        SocketClient.ProcessInfo p = lista.get(position);

        holder.tvNome.setText(p.nome);
        holder.tvDetalhes.setText(holder.itemView.getContext()
                .getString(R.string.process_details, p.pid, p.memoriaMb));

        // Mostra título da janela se existir
        if (p.janela != null && !p.janela.isEmpty()) {
            holder.tvJanela.setVisibility(View.VISIBLE);
            holder.tvJanela.setText(p.janela);
        } else {
            holder.tvJanela.setVisibility(View.GONE);
        }

        holder.btnKill.setOnClickListener(v -> killListener.onKill(p));
    }

    @Override
    public int getItemCount() { return lista.size(); }

    static class ViewHolder extends RecyclerView.ViewHolder {
        TextView tvNome, tvDetalhes, tvJanela;
        Button   btnKill;

        ViewHolder(View itemView) {
            super(itemView);
            tvNome     = itemView.findViewById(R.id.tvNome);
            tvDetalhes = itemView.findViewById(R.id.tvDetalhes);
            tvJanela   = itemView.findViewById(R.id.tvJanela);
            btnKill    = itemView.findViewById(R.id.btnKill);
        }
    }
}