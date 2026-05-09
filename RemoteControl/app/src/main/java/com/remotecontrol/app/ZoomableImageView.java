package com.remotecontrol.app;

import android.content.Context;
import android.graphics.Matrix;
import android.graphics.PointF;
import android.util.AttributeSet;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import androidx.appcompat.widget.AppCompatImageView;

/**
 * ImageView com suporte a pinch-to-zoom e arraste (pan).
 * Limites: zoom mínimo 1x (fit screen), máximo 5x.
 */
public class ZoomableImageView extends AppCompatImageView {

    private static final float MIN_ZOOM = 1f;
    private static final float MAX_ZOOM = 5f;

    private final Matrix matrix      = new Matrix();
    private final float[] matrixValues = new float[9];
    private final PointF  lastTouch  = new PointF();

    private ScaleGestureDetector scaleDetector;
    private boolean isDragging = false;

    public ZoomableImageView(Context context) {
        super(context);
        init(context);
    }

    public ZoomableImageView(Context context, AttributeSet attrs) {
        super(context, attrs);
        init(context);
    }

    private void init(Context context) {
        setScaleType(ScaleType.MATRIX);
        scaleDetector = new ScaleGestureDetector(context, new ScaleListener());
    }

    @Override
    protected void onSizeChanged(int w, int h, int oldW, int oldH) {
        super.onSizeChanged(w, h, oldW, oldH);
        if (getDrawable() != null) fitImageToView();
    }

    @Override
    public void setImageBitmap(android.graphics.Bitmap bm) {
        super.setImageBitmap(bm);
        post(this::fitImageToView);
    }

    private void fitImageToView() {
        if (getDrawable() == null || getWidth() == 0) return;

        int imgW = getDrawable().getIntrinsicWidth();
        int imgH = getDrawable().getIntrinsicHeight();

        float scale   = Math.min((float) getWidth() / imgW, (float) getHeight() / imgH);
        float offsetX = (getWidth()  - imgW * scale) / 2f;
        float offsetY = (getHeight() - imgH * scale) / 2f;

        matrix.reset();
        matrix.postScale(scale, scale);
        matrix.postTranslate(offsetX, offsetY);
        setImageMatrix(matrix);
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        scaleDetector.onTouchEvent(event);

        switch (event.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                lastTouch.set(event.getX(), event.getY());
                isDragging = true;
                break;

            case MotionEvent.ACTION_MOVE:
                if (isDragging && !scaleDetector.isInProgress()) {
                    matrix.postTranslate(
                            event.getX() - lastTouch.x,
                            event.getY() - lastTouch.y
                    );
                    clampMatrix();
                    setImageMatrix(matrix);
                    lastTouch.set(event.getX(), event.getY());
                }
                break;

            case MotionEvent.ACTION_UP:
                isDragging = false;
                // Obrigatório quando onTouchEvent é sobrescrito — acessibilidade
                performClick();
                break;

            case MotionEvent.ACTION_CANCEL:
                isDragging = false;
                break;
        }

        return true;
    }

    // Obrigatório ao sobrescrever onTouchEvent — satisfaz acessibilidade
    @Override
    public boolean performClick() {
        return super.performClick();
    }

    private void clampMatrix() {
        if (getDrawable() == null) return;

        matrix.getValues(matrixValues);
        float scaleX  = matrixValues[Matrix.MSCALE_X];
        float transX  = matrixValues[Matrix.MTRANS_X];
        float transY  = matrixValues[Matrix.MTRANS_Y];

        float scaledW = getDrawable().getIntrinsicWidth()  * scaleX;
        float scaledH = getDrawable().getIntrinsicHeight() * scaleX;

        matrixValues[Matrix.MTRANS_X] = scaledW < getWidth()
                ? (getWidth()  - scaledW) / 2f
                : Math.max(getWidth()  - scaledW, Math.min(0, transX));

        matrixValues[Matrix.MTRANS_Y] = scaledH < getHeight()
                ? (getHeight() - scaledH) / 2f
                : Math.max(getHeight() - scaledH, Math.min(0, transY));

        matrix.setValues(matrixValues);
    }

    private class ScaleListener extends ScaleGestureDetector.SimpleOnScaleGestureListener {
        @Override
        public boolean onScale(ScaleGestureDetector detector) {
            matrix.getValues(matrixValues);
            float current  = matrixValues[Matrix.MSCALE_X];
            float newScale = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, current * detector.getScaleFactor()));
            float factor   = newScale / current;

            matrix.postScale(factor, factor, detector.getFocusX(), detector.getFocusY());
            clampMatrix();
            setImageMatrix(matrix);
            return true;
        }
    }
}