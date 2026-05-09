package com.remotecontrol.app;

import android.content.ContentValues;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Bundle;
import android.provider.MediaStore;
import android.widget.ImageButton;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import java.io.IOException;
import java.io.OutputStream;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

public class ScreenshotViewer extends AppCompatActivity {

    private ZoomableImageView imageView;
    private TextView tvInfo;
    private TextView tvCounter;
    private ImageButton btnPrev;
    private ImageButton btnNext;

    private List<byte[]>  imageBytesList = new ArrayList<>();
    private List<String>  labelList      = new ArrayList<>();
    private Bitmap        currentBitmap;
    private int           currentIndex   = 0;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_screenshot_viewer);

        imageView  = findViewById(R.id.zoomableImage);
        tvInfo     = findViewById(R.id.tvInfo);
        tvCounter  = findViewById(R.id.tvCounter);
        btnPrev    = findViewById(R.id.btnPrev);
        btnNext    = findViewById(R.id.btnNext);

        ImageButton btnClose = findViewById(R.id.btnClose);
        ImageButton btnSave  = findViewById(R.id.btnSave);

        // Recebe listas de bytes e labels enviadas pela MainActivity
        List<byte[]> bytes  = MainActivity.pendingScreenshots;
        List<String> labels = MainActivity.pendingLabels;

        if (bytes == null || bytes.isEmpty()) {
            Toast.makeText(this, R.string.error_no_image, Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        imageBytesList = bytes;
        labelList      = labels != null ? labels : new ArrayList<>();

        showImage(0);

        btnClose.setOnClickListener(v -> finish());
        btnSave.setOnClickListener(v  -> saveCurrentToGallery());

        btnPrev.setOnClickListener(v -> {
            if (currentIndex > 0) showImage(currentIndex - 1);
        });

        btnNext.setOnClickListener(v -> {
            if (currentIndex < imageBytesList.size() - 1) showImage(currentIndex + 1);
        });
    }

    // ─── NAVEGAÇÃO ────────────────────────────────────────────────────────────

    private void showImage(int index) {
        currentIndex = index;

        // Libera bitmap anterior da memória
        if (currentBitmap != null && !currentBitmap.isRecycled()) {
            currentBitmap.recycle();
        }

        byte[] bytes = imageBytesList.get(index);
        currentBitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.length);

        if (currentBitmap == null) {
            Toast.makeText(this, R.string.error_decode_image, Toast.LENGTH_SHORT).show();
            return;
        }

        imageView.setImageBitmap(currentBitmap);

        // Atualiza info de resolução e label do monitor
        String label = index < labelList.size() ? labelList.get(index)
                : getString(R.string.monitor_default, index + 1);
        tvInfo.setText(getString(R.string.screenshot_info, label,
                currentBitmap.getWidth(), currentBitmap.getHeight()));

        // Contador "1 / 2"
        tvCounter.setText(getString(R.string.screenshot_counter,
                index + 1, imageBytesList.size()));

        // Habilita/desabilita botões de navegação nas extremidades
        btnPrev.setEnabled(index > 0);
        btnNext.setEnabled(index < imageBytesList.size() - 1);
        btnPrev.setAlpha(index > 0 ? 1f : 0.3f);
        btnNext.setAlpha(index < imageBytesList.size() - 1 ? 1f : 0.3f);
    }

    // ─── SALVAR ───────────────────────────────────────────────────────────────

    private void saveCurrentToGallery() {
        if (currentBitmap == null) return;

        String label = currentIndex < labelList.size()
                ? labelList.get(currentIndex)
                .replace(" ", "_").replace("(", "").replace(")", "")
                : getString(R.string.monitor_default, currentIndex + 1)
                .replace(" ", "_");

        String filename = "RemoteControl_" + label + "_"
                + new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(new Date())
                + ".jpg";

        ContentValues values = new ContentValues();
        values.put(MediaStore.Images.Media.DISPLAY_NAME, filename);
        values.put(MediaStore.Images.Media.MIME_TYPE, "image/jpeg");
        values.put(MediaStore.Images.Media.RELATIVE_PATH, "Pictures/RemoteControl");

        Uri uri = getContentResolver().insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, values);
        if (uri == null) {
            Toast.makeText(this, R.string.error_create_file, Toast.LENGTH_SHORT).show();
            return;
        }

        try {
            OutputStream stream = getContentResolver().openOutputStream(uri);
            if (stream == null) {
                Toast.makeText(this, R.string.error_null_stream, Toast.LENGTH_SHORT).show();
                return;
            }
            try (OutputStream out = stream) {
                currentBitmap.compress(Bitmap.CompressFormat.JPEG, 95, out);
            }

            Toast.makeText(this,
                    getString(R.string.save_success, filename), Toast.LENGTH_LONG).show();

            Intent view = new Intent(Intent.ACTION_VIEW);
            view.setDataAndType(uri, "image/jpeg");
            view.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivity(view);

        } catch (IOException e) {
            Toast.makeText(this,
                    getString(R.string.error_save, e.getMessage()), Toast.LENGTH_SHORT).show();
        }
    }

    // ─── LIFECYCLE ────────────────────────────────────────────────────────────

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (currentBitmap != null && !currentBitmap.isRecycled()) currentBitmap.recycle();
        // Limpa os dados estáticos ao fechar
        MainActivity.pendingScreenshots = null;
        MainActivity.pendingLabels      = null;
    }
}