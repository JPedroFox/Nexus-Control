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

    // ─── LIST PROCESSES ───────────────────────────────────────────────────────

    public static String listProcesses() {
        return "{\"cmd\":\"LIST_PROCESSES\"}";
    }

    // ─── KILL PROCESS ─────────────────────────────────────────────────────────

    @SuppressWarnings("unused") // Reservado para uso futuro na UI
    public static String killProcess(String processName) {
        return "{\"cmd\":\"KILL_PROCESS\",\"nome\":\"" + processName + "\"}";
    }
}