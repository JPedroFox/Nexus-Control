using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RemoteServer
{
    public static class CommandExecutor
    {
        private const float VOLUME_STEP = 0.05f;

        public static string Execute(JObject json)
        {
            try
            {
                string cmd = json["cmd"]?.ToString()?.ToUpper() ?? "";

                return cmd switch
                {
                    "MEDIA"          => HandleMedia(json),
                    "SISTEMA"        => HandleSistema(json),
                    "SCREENSHOT"     => HandleScreenshot(),
                    "KILL_PROCESS"   => HandleKillProcess(json),
                    "LIST_PROCESSES" => HandleListProcesses(),
                    "MOUSE"          => HandleMouse(json),
                    "TECLADO"        => HandleTeclado(json),
                    _                => Error($"Comando desconhecido: {cmd}")
                };
            }
            catch (Exception ex)
            {
                return Error($"Exceção ao executar comando: {ex.Message}");
            }
        }

        // ─── MEDIA ────────────────────────────────────────────────────────────

        private static string HandleMedia(JObject json)
        {
            string acao = json["acao"]?.ToString()?.ToUpper() ?? "";

            switch (acao)
            {
                case "VOLUME_UP":
                    ChangeVolume(+VOLUME_STEP);
                    return Ok($"Volume aumentado para {GetVolumePercent()}%");

                case "VOLUME_DOWN":
                    ChangeVolume(-VOLUME_STEP);
                    return Ok($"Volume diminuído para {GetVolumePercent()}%");

                case "MUTE":
                    ToggleMute();
                    return Ok("Mute alternado");

                case "PLAY_PAUSE":
                    SendMediaKey(VK_MEDIA_PLAY_PAUSE);
                    return Ok("Play/Pause enviado");

                case "NEXT":
                    SendMediaKey(VK_MEDIA_NEXT_TRACK);
                    return Ok("Próxima faixa");

                case "PREV":
                    SendMediaKey(VK_MEDIA_PREV_TRACK);
                    return Ok("Faixa anterior");

                default:
                    return Error($"Ação de mídia desconhecida: {acao}");
            }
        }

        private static void ChangeVolume(float delta)
        {
            using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            float current = device.AudioEndpointVolume.MasterVolumeLevelScalar;
            float newVol  = Math.Clamp(current + delta, 0f, 1f);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = newVol;
        }

        private static int GetVolumePercent()
        {
            using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
        }

        private static void ToggleMute()
        {
            using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
        }

        // ─── SISTEMA ──────────────────────────────────────────────────────────

        private static string HandleSistema(JObject json)
        {
            string acao = json["acao"]?.ToString()?.ToUpper() ?? "";

            return acao switch
            {
                "SHUTDOWN" => RunShellCommand("shutdown /s /t 5"),
                "RESTART"  => RunShellCommand("shutdown /r /t 5"),
                "LOCK"     => LockWorkstation(),
                _          => Error($"Ação de sistema desconhecida: {acao}")
            };
        }

        private static string RunShellCommand(string command)
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                CreateNoWindow  = true,
                UseShellExecute = false
            });
            return Ok($"Comando executado: {command}");
        }

        private static string LockWorkstation()
        {
            LockWorkStation();
            return Ok("Estação de trabalho bloqueada");
        }

        // ─── SCREENSHOT ───────────────────────────────────────────────────────

        /// <summary>
        /// Captura cada monitor via Win32 EnumDisplayMonitors + GetMonitorInfo,
        /// que retorna bounds físicos reais independente de DPI scaling.
        ///
        /// Screen.AllScreens sofre virtualização DPI no processo — com primário
        /// em 125%, os bounds do secundário ficam errados. A API Win32 direta
        /// não passa por essa camada de tradução.
        /// </summary>
        private static string HandleScreenshot()
        {
            ImageCodecInfo    jpegEncoder   = GetJpegEncoder();
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 75L);

            List<(RECT bounds, bool primary)> monitors = GetPhysicalMonitorBounds();
            var capturas = new List<object>();

            for (int i = 0; i < monitors.Count; i++)
            {
                RECT r   = monitors[i].bounds;
                int  w   = r.right  - r.left;
                int  h   = r.bottom - r.top;
                bool pri = monitors[i].primary;

                using Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(r.left, r.top, 0, 0,
                        new Size(w, h), CopyPixelOperation.SourceCopy);
                }

                using MemoryStream ms = new MemoryStream();
                bmp.Save(ms, jpegEncoder, encoderParams);

                capturas.Add(new
                {
                    monitor  = i + 1,
                    primaria = pri,
                    largura  = w,
                    altura   = h,
                    dados    = Convert.ToBase64String(ms.ToArray())
                });
            }

            return JsonConvert.SerializeObject(new
            {
                status   = "OK",
                telas    = capturas.Count,
                capturas = capturas
            });
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                if (codec.MimeType == "image/jpeg") return codec;
            throw new Exception("Encoder JPEG não encontrado");
        }

        // ─── KILL PROCESS ─────────────────────────────────────────────────────

        private static string HandleKillProcess(JObject json)
        {
            string nome = json["nome"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(nome))
                return Error("Campo 'nome' obrigatório para KILL_PROCESS");

            Process[] processos = Process.GetProcessesByName(
                nome.Replace(".exe", "", StringComparison.OrdinalIgnoreCase)
            );

            if (processos.Length == 0)
                return Error($"Processo não encontrado: {nome}");

            foreach (Process p in processos) p.Kill();
            return Ok($"{processos.Length} processo(s) '{nome}' encerrado(s)");
        }

        // ─── LIST PROCESSES ───────────────────────────────────────────────────────

        private static string HandleListProcesses()
        {
            Process[] processos = Process.GetProcesses();

            var lista = new List<object>();
            foreach (Process p in processos)
            {
                try
                {
                    lista.Add(new
                    {
                        pid  = p.Id,
                        nome = p.ProcessName,
                        mem_mb = p.WorkingSet64 / 1024 / 1024  // MB
                    });
                }
                catch
                {
                    // Alguns processos do sistema negam acesso — ignora silenciosamente
                }
            }

            // Ordena por nome para facilitar leitura no celular
            lista.Sort((a, b) =>
                string.Compare(
                    ((dynamic)a).nome,
                    ((dynamic)b).nome,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            return JsonConvert.SerializeObject(new
            {
                status    = "OK",
                total     = lista.Count,
                processos = lista
            });
        }

        // ─── HELPERS ──────────────────────────────────────────────────────────

        private static string Ok(string msg) =>
            JsonConvert.SerializeObject(new { status = "OK", msg });

        private static string Error(string msg) =>
            JsonConvert.SerializeObject(new { status = "ERROR", msg });

        // ─── MOUSE ────────────────────────────────────────────────────────────

        private static string HandleMouse(JObject json)
        {
            string acao = json["acao"]?.ToString()?.ToUpper() ?? "";

            switch (acao)
            {
                case "MOVE":
                    int dx = json["dx"]?.ToObject<int>() ?? 0;
                    int dy = json["dy"]?.ToObject<int>() ?? 0;
                    SendMouseInput(dx, dy, 0, MOUSEEVENTF_MOVE);
                    return Ok("Mouse movido");

                case "LEFT_DOWN":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    return Ok("Botão esquerdo pressionado");

                case "LEFT_UP":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    return Ok("Botão esquerdo solto");

                case "LEFT_CLICK":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    return Ok("Click esquerdo");

                case "RIGHT_CLICK":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_RIGHTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_RIGHTUP);
                    return Ok("Click direito");

                case "DOUBLE_CLICK":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    return Ok("Duplo click");

                case "SCROLL":
                    int delta = json["delta"]?.ToObject<int>() ?? 0;
                    // WHEEL_DELTA = 120 por notch padrão
                    SendMouseInput(0, 0, (uint)(delta * 120), MOUSEEVENTF_WHEEL);
                    return Ok("Scroll enviado");

                default:
                    return Error($"Ação de mouse desconhecida: {acao}");
            }
        }

        private static void SendMouseInput(int dx, int dy, uint data, uint flags)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dx       = dx;
            inputs[0].U.mi.dy       = dy;
            inputs[0].U.mi.mouseData = data;
            inputs[0].U.mi.dwFlags  = flags;
            SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        }

        // ─── TECLADO ──────────────────────────────────────────────────────────

        private static string HandleTeclado(JObject json)
        {
            // Modo texto: digita uma string caractere a caractere via Unicode
            string? texto = json["texto"]?.ToString();
            if (texto != null)
            {
                foreach (char c in texto)
                    SendUnicodeChar(c);
                return Ok($"Texto digitado: {texto}");
            }

            // Modo tecla especial: usa virtual key code
            string tecla = json["tecla"]?.ToString()?.ToUpper() ?? "";
            if (!TeclaEspecial.TryGetValue(tecla, out ushort vk))
                return Error($"Tecla desconhecida: {tecla}");

            SendVirtualKey(vk);
            return Ok($"Tecla enviada: {tecla}");
        }

        private static void SendUnicodeChar(char c)
        {
            INPUT[] inputs = new INPUT[2];

            // Key down
            inputs[0].type         = INPUT_KEYBOARD;
            inputs[0].U.ki.wScan   = c;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;

            // Key up
            inputs[1].type         = INPUT_KEYBOARD;
            inputs[1].U.ki.wScan   = c;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }

        private static void SendVirtualKey(ushort vk)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type       = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk   = vk;
            inputs[1].type       = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk   = vk;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }

        // Mapa de teclas especiais
        private static readonly Dictionary<string, ushort> TeclaEspecial = new()
        {
            ["ENTER"]     = 0x0D,
            ["BACKSPACE"] = 0x08,
            ["TAB"]       = 0x09,
            ["ESC"]       = 0x1B,
            ["SPACE"]     = 0x20,
            ["UP"]        = 0x26,
            ["DOWN"]      = 0x28,
            ["LEFT"]      = 0x25,
            ["RIGHT"]     = 0x27,
            ["HOME"]      = 0x24,
            ["END"]       = 0x23,
            ["DELETE"]    = 0x2E,
            ["PAGEUP"]    = 0x21,
            ["PAGEDOWN"]  = 0x22,
            ["WIN"]       = 0x5B,
            ["COPY"]      = 0x43, // Ctrl+C precisaria de combo — simplificado
        };

        // ─── WIN32 API ────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private const byte VK_MEDIA_PLAY_PAUSE   = 0xB3;
        private const byte VK_MEDIA_NEXT_TRACK   = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK   = 0xB1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x01;
        private const uint KEYEVENTF_KEYUP       = 0x02;
        private const uint KEYEVENTF_UNICODE     = 0x04;

        // SendInput: tipo de evento
        private const uint INPUT_MOUSE    = 0;
        private const uint INPUT_KEYBOARD = 1;

        // Mouse flags
        private const uint MOUSEEVENTF_MOVE      = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP   = 0x0010;
        private const uint MOUSEEVENTF_WHEEL     = 0x0800;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int    dx, dy;
            public uint   mouseData;
            public uint   dwFlags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint   dwFlags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT  mi;
            [FieldOffset(0)] public KEYBDINPUT  ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint       type;
            public InputUnion U;
        }

        private static void SendMediaKey(byte key)
        {
            keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }

        // ─── WIN32: ENUM MONITORS ─────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFOEX
        {
            public int  cbSize;
            public RECT rcMonitor; // bounds físicos no desktop virtual
            public RECT rcWork;    // área útil (sem taskbar)
            public uint dwFlags;   // 1 = monitor primário
        }

        // Delegate nomeado — lambda com parâmetro ref não compila em C# < 14 (CS9202)
        private delegate bool MonitorEnumProc(
            IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        private const uint MONITORINFOF_PRIMARY = 1;

        // Acumulador estático para o callback síncrono de EnumDisplayMonitors
        private static List<(RECT bounds, bool primary)>? _monitorEnumResult;

        private static bool MonitorEnumCallback(
            IntPtr hMon, IntPtr hdcMon, ref RECT lprcMon, IntPtr data)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMon, ref info))
                _monitorEnumResult!.Add((info.rcMonitor, (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            return true;
        }

        private static List<(RECT bounds, bool primary)> GetPhysicalMonitorBounds()
        {
            _monitorEnumResult = new List<(RECT, bool)>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
            List<(RECT, bool)> result = _monitorEnumResult;
            _monitorEnumResult = null;
            return result;
        }
    }
}