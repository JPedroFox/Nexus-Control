# Nexus Control — Communication Protocol

> **Transport:** raw TCP socket, UTF-8, newline-delimited JSON (`\n` after every message).  
> **Default port:** `8888`  
> **Direction notation:** `App →` = Android sends to server; `← Server` = server replies.

---

## 1. Connection Handshake

Immediately after a TCP connection is accepted, the server pushes one greeting line.  
The app **must silently discard** any message whose `status` is `"CONNECTED"`.

```
← Server  {"status":"CONNECTED","server":"Nexus Control v1.0"}
```

---

## 2. General Response Envelope

Every command produces exactly one response line.

| Field    | Type   | Always present | Description                        |
|----------|--------|----------------|------------------------------------|
| `status` | string | ✅             | `"OK"` or `"ERROR"`                |
| `msg`    | string | most commands  | Human-readable result or error text |

```json
{"status":"OK",  "msg":"Volume up: 55%"}
{"status":"ERROR","msg":"Process not found: notepad.exe"}
{"status":"ERROR","msg":"JSON inválido"}
```

Exception: `SCREENSHOT` and `LIST_PROCESSES` return extended envelopes — see sections 5 and 6.

---

## 3. Media Commands

**`cmd` = `"MEDIA"`** — controls audio and media playback.

| `acao`        | Effect on server                                           |
|---------------|------------------------------------------------------------|
| `VOLUME_UP`   | Master volume +5 % (NAudio CoreAudio)                      |
| `VOLUME_DOWN` | Master volume −5 %                                         |
| `MUTE`        | Toggle mute on default audio endpoint                      |
| `PLAY_PAUSE`  | Sends `VK_MEDIA_PLAY_PAUSE` (0xB3)                         |
| `NEXT`        | Sends `VK_MEDIA_NEXT_TRACK` (0xB0)                         |
| `PREV`        | Sends `VK_MEDIA_PREV_TRACK` (0xB1)                         |
| `SKIP_FWD`    | Sends `VK_RIGHT` (0x27) — +10 s in VLC/browser players    |
| `SKIP_BACK`   | Sends `VK_LEFT`  (0x25) — −10 s in VLC/browser players    |

### Request
```json
{"cmd":"MEDIA","acao":"VOLUME_UP"}
```

### Response
```json
{"status":"OK","msg":"Volume up: 60%"}
{"status":"OK","msg":"Mute toggled"}
{"status":"OK","msg":"+10s"}
```

---

## 4. System Commands

**`cmd` = `"SISTEMA"`** — OS-level power actions.

| `acao`     | Effect on server                              |
|------------|-----------------------------------------------|
| `SHUTDOWN` | `shutdown /s /t 5` — shuts down in 5 s        |
| `RESTART`  | `shutdown /r /t 5` — restarts in 5 s          |
| `LOCK`     | `LockWorkStation()` — locks the session        |

### Request
```json
{"cmd":"SISTEMA","acao":"LOCK"}
```

### Response
```json
{"status":"OK","msg":"Workstation locked"}
{"status":"OK","msg":"Command executed: shutdown /s /t 5"}
```

---

## 5. Screenshot

**`cmd` = `"SCREENSHOT"`** — captures all physical monitors (DPI-aware).

### Request
```json
{"cmd":"SCREENSHOT"}
```

### Response
The response is a single JSON line containing an array of captures, one entry per monitor.  
Images are JPEG (quality 75), Base64-encoded.

```json
{
  "status": "OK",
  "telas": 2,
  "capturas": [
    {
      "monitor":  1,
      "primaria": true,
      "largura":  1920,
      "altura":   1080,
      "dados":    "<base64-encoded JPEG>"
    },
    {
      "monitor":  2,
      "primaria": false,
      "largura":  2560,
      "altura":   1440,
      "dados":    "<base64-encoded JPEG>"
    }
  ]
}
```

| Field             | Type    | Description                            |
|-------------------|---------|----------------------------------------|
| `telas`           | int     | Total number of monitors captured      |
| `capturas[].monitor`  | int | 1-based monitor index                  |
| `capturas[].primaria` | bool | `true` for the primary monitor        |
| `capturas[].largura`  | int | Physical pixel width                   |
| `capturas[].altura`   | int | Physical pixel height                  |
| `capturas[].dados`    | string | Base64 JPEG image data               |

> ⚠️ This response can be several megabytes for multi-monitor setups.  
> The app uses a 1 MB read buffer (`BufferedReader` with `1024*1024` bytes) to handle it.  
> Detection on the app side: presence of the key `"capturas"` in the JSON line.

---

## 6. Process List

**`cmd` = `"LIST_PROCESSES"`** — returns all running Windows processes with memory info.

### Request
```json
{"cmd":"LIST_PROCESSES"}
```

### Response
```json
{
  "status": "OK",
  "processos": [
    {
      "nome":   "chrome",
      "pid":    4821,
      "mem_mb": 312,
      "janela": "New Tab - Google Chrome"
    },
    {
      "nome":   "notepad",
      "pid":    9104,
      "mem_mb": 8,
      "janela": ""
    }
  ]
}
```

| Field              | Type   | Description                                    |
|--------------------|--------|------------------------------------------------|
| `processos[].nome` | string | Process name (without `.exe`)                  |
| `processos[].pid`  | int    | Process ID                                     |
| `processos[].mem_mb` | long | Working set memory in MB                     |
| `processos[].janela` | string | Main window title, empty if none            |

> Detection on the app side: presence of the key `"processos"` in the JSON line.

---

## 7. Kill Process

**`cmd` = `"KILL_PROCESS"`** — forcibly terminates all processes matching the given name.

### Request
```json
{"cmd":"KILL_PROCESS","nome":"notepad.exe"}
```

The `.exe` suffix is optional and stripped before lookup.

### Response
```json
{"status":"OK",  "msg":"2 process(es) 'notepad.exe' killed"}
{"status":"ERROR","msg":"Process not found: notepad.exe"}
{"status":"ERROR","msg":"Field 'nome' required for KILL_PROCESS"}
```

---

## 8. Mouse Commands

**`cmd` = `"MOUSE"`**

### 8.1 Move (relative delta)
```json
{"cmd":"MOUSE","acao":"MOVE","dx":15,"dy":-8}
```
`dx` / `dy` are integers in pixels. The server uses `MOUSEEVENTF_MOVE` (relative).

### 8.2 Clicks
```json
{"cmd":"MOUSE","acao":"LEFT_CLICK"}
{"cmd":"MOUSE","acao":"RIGHT_CLICK"}
{"cmd":"MOUSE","acao":"DOUBLE_CLICK"}
```

### 8.3 Left button down / up (for drag)
```json
{"cmd":"MOUSE","acao":"LEFT_DOWN"}
{"cmd":"MOUSE","acao":"LEFT_UP"}
```

### 8.4 Scroll
```json
{"cmd":"MOUSE","acao":"SCROLL","delta":1}
```
`delta` is an integer. Positive = scroll up, negative = scroll down.  
The server multiplies by 120 (one Windows scroll notch = `WHEEL_DELTA`).

### Responses
```json
{"status":"OK","msg":"Left click"}
{"status":"OK","msg":"Scroll"}
{"status":"ERROR","msg":"Unknown mouse action: SPIN"}
```

---

## 9. Keyboard Commands

**`cmd` = `"TECLADO"`**

Two mutually exclusive modes per message — use `texto` **or** `tecla`, never both.

### 9.1 Unicode text input
Sends each character as a `KEYEVENTF_UNICODE` key event (supports any Unicode character).

```json
{"cmd":"TECLADO","texto":"Hello World!"}
```

`"` and `\` inside `texto` must be JSON-escaped by the sender (`CommandBuilder.keyboardText` handles this).

### 9.2 Special key
```json
{"cmd":"TECLADO","tecla":"ENTER"}
```

Supported `tecla` values and their virtual key codes:

| `tecla`      | VK code | `tecla`    | VK code |
|--------------|---------|------------|---------|
| `ENTER`      | 0x0D    | `HOME`     | 0x24    |
| `BACKSPACE`  | 0x08    | `END`      | 0x23    |
| `TAB`        | 0x09    | `DELETE`   | 0x2E    |
| `ESC`        | 0x1B    | `PAGEUP`   | 0x21    |
| `SPACE`      | 0x20    | `PAGEDOWN` | 0x22    |
| `UP`         | 0x26    | `WIN`      | 0x5B    |
| `DOWN`       | 0x28    | `COPY`     | 0x43    |
| `LEFT`       | 0x25    |            |         |
| `RIGHT`      | 0x27    |            |         |

### Responses
```json
{"status":"OK","msg":"Text typed: Hello World!"}
{"status":"OK","msg":"Key sent: ENTER"}
{"status":"ERROR","msg":"Unknown key: F5"}
```

---

## 10. Error Cases

| Scenario                        | Response                                          |
|---------------------------------|---------------------------------------------------|
| Malformed / non-JSON line       | `{"status":"ERROR","msg":"JSON inválido"}`         |
| Unknown `cmd` value             | `{"status":"ERROR","msg":"Unknown command: XYZ"}`  |
| Unknown `acao` in a command     | `{"status":"ERROR","msg":"Unknown media action: XYZ"}` |
| Internal exception in executor  | `{"status":"ERROR","msg":"Exception executing command: <detail>"}` |

---

## 11. Reconnection Behaviour (App Side)

The app (`SocketClient`) implements automatic reconnection on connection loss:

| Parameter        | Value                                 |
|------------------|---------------------------------------|
| Connect timeout  | 5 000 ms                              |
| Max reconnects   | 5 attempts                            |
| Retry delay      | `3 000 ms × attempt_number` (backoff) |

Reconnection is cancelled when the user explicitly disconnects (`disconnect()` sets `shouldReconnect = false`).

---

## 12. Full Message Flow Example

```
App connects on TCP :8888
← Server  {"status":"CONNECTED","server":"Nexus Control v1.0"}

App →     {"cmd":"MEDIA","acao":"VOLUME_UP"}
← Server  {"status":"OK","msg":"Volume up: 55%"}

App →     {"cmd":"SCREENSHOT"}
← Server  {"status":"OK","telas":1,"capturas":[{"monitor":1,"primaria":true,"largura":1920,"altura":1080,"dados":"..."}]}

App →     {"cmd":"LIST_PROCESSES"}
← Server  {"status":"OK","processos":[{"nome":"chrome","pid":4821,"mem_mb":312,"janela":"New Tab - Google Chrome"},...]}

App →     {"cmd":"KILL_PROCESS","nome":"notepad"}
← Server  {"status":"OK","msg":"1 process(es) 'notepad' killed"}

App →     {"cmd":"MOUSE","acao":"MOVE","dx":10,"dy":5}
← Server  {"status":"OK","msg":"Mouse moved"}

App →     {"cmd":"TECLADO","texto":"Hello"}
← Server  {"status":"OK","msg":"Text typed: Hello"}

App closes TCP connection
```
