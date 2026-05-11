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

    /** Filtra a lista por nome e atualiza o RecyclerView */
    public void filter(String query, List<SocketClient.ProcessInfo> listaOriginal) {
        int oldSize = lista.size();

        if (query.isEmpty()) {
            lista = new ArrayList<>(listaOriginal);
        } else {
            String q = query.toLowerCase();
            lista = new ArrayList<>();
            for (SocketClient.ProcessInfo p : listaOriginal) {
                if (p.nome.toLowerCase().contains(q)) lista.add(p);
            }
        }

        // Notifica inserções/remoções precisas em vez de invalidar tudo
        int newSize = lista.size();
        if (newSize > oldSize) {
            notifyItemRangeInserted(oldSize, newSize - oldSize);
            notifyItemRangeChanged(0, oldSize);
        } else if (newSize < oldSize) {
            notifyItemRangeRemoved(newSize, oldSize - newSize);
            notifyItemRangeChanged(0, newSize);
        } else {
            notifyItemRangeChanged(0, newSize);
        }
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



    // private: não precisa ser visível fora do adapter
    @SuppressWarnings("WeakerAccess")
    public static final class ViewHolder extends RecyclerView.ViewHolder {
        final TextView tvNome, tvDetalhes, tvJanela;
        final Button   btnKill;

        ViewHolder(View itemView) {
            super(itemView);
            tvNome     = itemView.findViewById(R.id.tvNome);
            tvDetalhes = itemView.findViewById(R.id.tvDetalhes);
            tvJanela   = itemView.findViewById(R.id.tvJanela);
            btnKill    = itemView.findViewById(R.id.btnKill);
        }
    }
}