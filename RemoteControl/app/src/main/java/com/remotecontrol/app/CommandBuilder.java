package com.remotecontrol.app;

/**
 * Constrói as strings JSON que o servidor C# espera receber.
 * Centraliza o protocolo num único lugar — se mudar o contrato,
 * muda só aqui.
 * Baseado em protocol.md
 */
public class CommandBuilder {

    // ─── MÍDIA ────────────────────────────────────────────────────────────────

    public static String volumeUp()    { return media("VOLUME_UP");   }
    public static String volumeDown()  { return media("VOLUME_DOWN"); }
    public static String mute()        { return media("MUTE");        }
    public static String playPause()   { return media("PLAY_PAUSE");  }
    public static String nextTrack()   { return media("NEXT");        }
    public static String prevTrack()   { return media("PREV");        }

    private static String media(String acao) {
        return "{\"cmd\":\"MEDIA\",\"acao\":\"" + acao + "\"}";
    }

    // ─── SISTEMA ──────────────────────────────────────────────────────────────

    public static String shutdown() { return sistema("SHUTDOWN"); }
    public static String restart()  { return sistema("RESTART");  }
    public static String lock()     { return sistema("LOCK");     }

    private static String sistema(String acao) {
        return "{\"cmd\":\"SISTEMA\",\"acao\":\"" + acao + "\"}";
    }

    // ─── SCREENSHOT ───────────────────────────────────────────────────────────

    public static String screenshot() {
        return "{\"cmd\":\"SCREENSHOT\"}";
    }

    // ─── PROCESSOS ────────────────────────────────────────────────────────────

    public static String listProcesses() {
        return "{\"cmd\":\"LIST_PROCESSES\"}";
    }

    public static String killProcess(String processName) {
        return "{\"cmd\":\"KILL_PROCESS\",\"nome\":\"" + processName + "\"}";
    }

    // ─── MOUSE ────────────────────────────────────────────────────────────────

    public static String mouseMove(int dx, int dy) {
        return "{\"cmd\":\"MOUSE\",\"acao\":\"MOVE\",\"dx\":" + dx + ",\"dy\":" + dy + "}";
    }
    public static String mouseLeftClick()  { return mouse("LEFT_CLICK");  }
    public static String mouseRightClick() { return mouse("RIGHT_CLICK"); }
    public static String mouseDoubleClick(){ return mouse("DOUBLE_CLICK");}
    public static String mouseLeftDown()   { return mouse("LEFT_DOWN");   }
    public static String mouseLeftUp()     { return mouse("LEFT_UP");     }

    public static String mouseScroll(int delta) {
        return "{\"cmd\":\"MOUSE\",\"acao\":\"SCROLL\",\"delta\":" + delta + "}";
    }

    private static String mouse(String acao) {
        return "{\"cmd\":\"MOUSE\",\"acao\":\"" + acao + "\"}";
    }

    // ─── TECLADO ──────────────────────────────────────────────────────────────

    public static String keyboardText(String texto) {
        // Escapa aspas e barras invertidas no texto
        String safe = texto.replace("\\", "\\\\").replace("\"", "\\\"");
        return "{\"cmd\":\"TECLADO\",\"texto\":\"" + safe + "\"}";
    }

    public static String keyboardKey(String tecla) {
        return "{\"cmd\":\"TECLADO\",\"tecla\":\"" + tecla + "\"}";
    }
}