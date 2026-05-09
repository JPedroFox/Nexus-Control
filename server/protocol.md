# Protocolo de Comunicação — RemoteServer

> Contrato de mensagens JSON entre o Server (C#) e o Client (Android/Java).
> Toda mensagem é uma **linha única JSON** terminada com `\n`.

---

## Fluxo de Conexão

```
[Android]                          [Windows C#]
   |                                    |
   |--- TCP connect :8888 ------------>|
   |<-- {"status":"CONNECTED",...} ----|   ← Handshake
   |                                    |
   |--- {"cmd":"MEDIA","acao":"..."} -->|   ← Comando
   |<-- {"status":"OK","msg":"..."} ----|   ← Resposta
   |                                    |
   |--- {"cmd":"SCREENSHOT"} --------->|
   |<-- {"status":"OK","dados":"..."} --|   ← PNG em Base64
```

---

## Comandos disponíveis

### 🔊 Mídia
```json
{"cmd": "MEDIA", "acao": "VOLUME_UP"}
{"cmd": "MEDIA", "acao": "VOLUME_DOWN"}
{"cmd": "MEDIA", "acao": "MUTE"}
{"cmd": "MEDIA", "acao": "PLAY_PAUSE"}
{"cmd": "MEDIA", "acao": "NEXT"}
{"cmd": "MEDIA", "acao": "PREV"}
```

### 💻 Sistema
```json
{"cmd": "SISTEMA", "acao": "SHUTDOWN"}
{"cmd": "SISTEMA", "acao": "RESTART"}
{"cmd": "SISTEMA", "acao": "LOCK"}
```

### 📸 Screenshot
```json
{"cmd": "SCREENSHOT"}
```

### ☠️ Matar processo
```json
{"cmd": "KILL_PROCESS", "nome": "chrome.exe"}
```

---

## Respostas do servidor

### Sucesso
```json
{"status": "OK", "msg": "Volume aumentado para 60%"}
```

### Sucesso com dados (Screenshot)
```json
{"status": "OK", "formato": "PNG", "dados": "iVBORw0KGgoAAAANSUhEUgAA..."}
```

### Erro
```json
{"status": "ERROR", "msg": "Comando desconhecido: XYZ"}
```

---

## Próximos comandos (backlog)
- `{"cmd": "MOUSE", "acao": "MOVE", "x": 100, "y": 200}`
- `{"cmd": "MOUSE", "acao": "CLICK"}`
- `{"cmd": "TECLADO", "texto": "hello world"}`
- `{"cmd": "LISTAR_PROCESSOS"}` → retorna JSON com lista de processos ativos
