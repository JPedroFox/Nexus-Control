package com.remotecontrol.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.util.AttributeSet;
import android.view.MotionEvent;
import android.view.View;

/**
 * Área de touchpad: converte movimento de dedo em deltas de mouse.
 * Gestos:
 *  - 1 dedo mover               → MOUSE_MOVE (delta)
 *  - 1 dedo tap curto           → LEFT_CLICK
 *  - 1 dedo tap + tap e segura  → LEFT_DOWN (drag); soltar → LEFT_UP
 *  - 2 dedos mover              → SCROLL
 *  - 2 dedos tap                → RIGHT_CLICK
 */
public class TouchpadView extends View {

    public interface TouchpadListener {
        void onMove(int dx, int dy);
        void onLeftClick();
        void onRightClick();
        void onScroll(int delta);
        void onLeftDown();
        void onLeftUp();
    }

    private static final float SENSITIVITY   = 1.8f;
    private static final long  TAP_MAX_MS    = 200;  // tap válido se solto em < 200ms
    private static final float TAP_MAX_MOVE  = 12f;  // tap válido se moveu < 12px
    private static final long  DOUBLE_TAP_MS = 300;  // janela para detectar double-tap

    private TouchpadListener listener;

    private final Paint bgPaint   = new Paint();
    private final Paint dotPaint  = new Paint();
    private final Paint hintPaint = new Paint();
    private final Paint subPaint  = new Paint();
    private final RectF bgRect    = new RectF();

    // Estado do toque
    private float   lastX, lastY;
    private long    touchDownTime;
    private float   touchDownX, touchDownY;
    private boolean twoFingerActive = false;

    // Double-tap-hold
    private long    lastTapUpTime = 0;   // momento em que o 1.º tap foi solto
    private boolean isDragMode    = false;

    // Runnable que dispara o click do 1.º tap após a janela de double-tap expirar
    private final Runnable pendingClick = () -> {
        if (listener != null) listener.onLeftClick();
    };

    public TouchpadView(Context context) { super(context); init(); }
    public TouchpadView(Context context, AttributeSet attrs) { super(context, attrs); init(); }

    private void init() {
        bgPaint.setColor(0xFF0D0D1A);
        bgPaint.setAntiAlias(true);

        dotPaint.setColor(0x3300E5FF);
        dotPaint.setAntiAlias(true);

        hintPaint.setColor(0x55FFFFFF);
        hintPaint.setAntiAlias(true);
        hintPaint.setTextSize(32f);
        hintPaint.setTextAlign(Paint.Align.CENTER);

        subPaint.set(hintPaint);
        subPaint.setTextSize(22f);
        subPaint.setColor(0x33FFFFFF);
    }

    public void setListener(TouchpadListener listener) {
        this.listener = listener;
    }

    @Override
    protected void onDraw(Canvas canvas) {
        float w = getWidth(), h = getHeight();

        bgRect.set(0, 0, w, h);
        canvas.drawRoundRect(bgRect, 24f, 24f, bgPaint);

        dotPaint.setStyle(Paint.Style.FILL);
        for (float x = 32; x < w; x += 48)
            for (float y = 32; y < h; y += 48)
                canvas.drawCircle(x, y, 2f, dotPaint);

        canvas.drawText("TOUCHPAD", w / 2f, h / 2f - 12f, hintPaint);
        canvas.drawText("toque e arraste para mover", w / 2f, h / 2f + 22f, subPaint);
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        if (listener == null) return true;

        int pointerCount = event.getPointerCount();

        switch (event.getActionMasked()) {

            case MotionEvent.ACTION_DOWN: {
                lastX         = event.getX();
                lastY         = event.getY();
                touchDownX    = lastX;
                touchDownY    = lastY;
                touchDownTime = System.currentTimeMillis();
                twoFingerActive = false;

                long now = System.currentTimeMillis();
                if (!isDragMode && lastTapUpTime > 0 && now - lastTapUpTime < DOUBLE_TAP_MS) {
                    // Segundo toque chegou dentro da janela → cancela o click pendente
                    // e aguarda o ACTION_UP para decidir: drag ou double-tap
                    removeCallbacks(pendingClick);
                    lastTapUpTime = -1; // marca que estamos no segundo toque
                }
                break;
            }

            case MotionEvent.ACTION_POINTER_DOWN:
                if (event.getPointerCount() == 2) {
                    twoFingerActive = true;
                    lastX = event.getX(0);
                    lastY = event.getY(0);
                }
                break;

            case MotionEvent.ACTION_MOVE:
                if (pointerCount == 1 && !twoFingerActive) {
                    float dx = (event.getX() - lastX) * SENSITIVITY;
                    float dy = (event.getY() - lastY) * SENSITIVITY;

                    if (Math.abs(dx) > 0.5f || Math.abs(dy) > 0.5f) {
                        // Se estava no segundo toque e começou a mover → inicia drag
                        if (lastTapUpTime == -1 && !isDragMode) {
                            isDragMode    = true;
                            lastTapUpTime = 0;
                            listener.onLeftDown();
                        }
                        listener.onMove((int) dx, (int) dy);
                    }

                    lastX = event.getX();
                    lastY = event.getY();

                } else if (pointerCount >= 2) {
                    float dy = event.getY(0) - lastY;
                    if (Math.abs(dy) > 4f) {
                        listener.onScroll(dy > 0 ? -1 : 1);
                        lastY = event.getY(0);
                    }
                    lastX = event.getX(0);
                }
                break;

            case MotionEvent.ACTION_UP: {
                long now      = System.currentTimeMillis();
                long duration = now - touchDownTime;
                float moved   = distance(touchDownX, touchDownY, event.getX(), event.getY());
                boolean wasTap = duration < TAP_MAX_MS && moved < TAP_MAX_MOVE;

                if (twoFingerActive) {
                    if (wasTap) listener.onRightClick();

                } else if (isDragMode) {
                    // Solta o botão esquerdo
                    listener.onLeftUp();
                    isDragMode    = false;
                    lastTapUpTime = 0;

                } else if (lastTapUpTime == -1) {
                    // Era o segundo toque mas foi solto rápido → double-click normal
                    lastTapUpTime = 0;
                    listener.onLeftClick();
                    listener.onLeftClick();

                } else if (wasTap) {
                    // Primeiro tap: agenda o click com delay para dar chance ao double-tap
                    lastTapUpTime = now;
                    postDelayed(pendingClick, DOUBLE_TAP_MS);

                } else {
                    // Movimento sem tap: reseta
                    lastTapUpTime = 0;
                }

                twoFingerActive = false;
                break;
            }
        }

        // Se está no segundo toque E o dedo ficou parado (sem ACTION_MOVE),
        // e passou tempo suficiente → inicia drag mode via hold
        if (event.getActionMasked() == MotionEvent.ACTION_DOWN && lastTapUpTime == -1) {
            postDelayed(() -> {
                if (lastTapUpTime == -1 && !isDragMode) {
                    isDragMode    = true;
                    lastTapUpTime = 0;
                    if (listener != null) listener.onLeftDown();
                }
            }, TAP_MAX_MS);
        }

        performClick();
        return true;
    }

    @Override
    public boolean performClick() {
        return super.performClick();
    }

    private static float distance(float x1, float y1, float x2, float y2) {
        float dx = x2 - x1, dy = y2 - y1;
        return (float) Math.sqrt(dx * dx + dy * dy);
    }
}