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
                    _                => Error($"Unknown command: {cmd}")
                };
            }
            catch (Exception ex)
            {
                return Error($"Exception executing command: {ex.Message}");
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
                    return Ok($"Volume up: {GetVolumePercent()}%");

                case "VOLUME_DOWN":
                    ChangeVolume(-VOLUME_STEP);
                    return Ok($"Volume down: {GetVolumePercent()}%");

                case "MUTE":
                    ToggleMute();
                    return Ok("Mute toggled");

                case "PLAY_PAUSE":
                    SendMediaKey(VK_MEDIA_PLAY_PAUSE);
                    return Ok("Play/Pause");

                case "NEXT":
                    SendMediaKey(VK_MEDIA_NEXT_TRACK);
                    return Ok("Next track");

                case "PREV":
                    SendMediaKey(VK_MEDIA_PREV_TRACK);
                    return Ok("Previous track");

                // ── Skip forward +10 s ────────────────────────────────────
                // Sends Right arrow key — universally recognised as a short
                // skip in VLC (default 10 s), Windows Media Player, browser
                // video players and most desktop media apps.
                case "SKIP_FWD":
                    SendVirtualKey(VK_RIGHT);
                    return Ok("+10s");

                // ── Skip backward -10 s ───────────────────────────────────
                case "SKIP_BACK":
                    SendVirtualKey(VK_LEFT);
                    return Ok("-10s");

                default:
                    return Error($"Unknown media action: {acao}");
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

        // ─── SYSTEM ───────────────────────────────────────────────────────────

        private static string HandleSistema(JObject json)
        {
            string acao = json["acao"]?.ToString()?.ToUpper() ?? "";

            return acao switch
            {
                "SHUTDOWN" => RunShellCommand("shutdown /s /t 5"),
                "RESTART"  => RunShellCommand("shutdown /r /t 5"),
                "LOCK"     => LockWorkstation(),
                _          => Error($"Unknown system action: {acao}")
            };
        }

        private static string RunShellCommand(string command)
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                CreateNoWindow  = true,
                UseShellExecute = false
            });
            return Ok($"Command executed: {command}");
        }

        private static string LockWorkstation()
        {
            LockWorkStation();
            return Ok("Workstation locked");
        }

        // ─── SCREENSHOT ───────────────────────────────────────────────────────

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
            throw new Exception("JPEG encoder not found");
        }

        // ─── KILL PROCESS ─────────────────────────────────────────────────────

        private static string HandleKillProcess(JObject json)
        {
            string nome = json["nome"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(nome))
                return Error("Field 'nome' required for KILL_PROCESS");

            Process[] processos = Process.GetProcessesByName(
                nome.Replace(".exe", "", StringComparison.OrdinalIgnoreCase)
            );

            if (processos.Length == 0)
                return Error($"Process not found: {nome}");

            foreach (Process p in processos) p.Kill();
            return Ok($"{processos.Length} process(es) '{nome}' killed");
        }

        // ─── LIST PROCESSES ───────────────────────────────────────────────────

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
                        mem  = p.WorkingSet64 / 1024
                    });
                }
                catch { }
            }

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
                    return Ok("Mouse moved");

                case "LEFT_DOWN":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    return Ok("Left button down");

                case "LEFT_UP":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    return Ok("Left button up");

                case "LEFT_CLICK":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    return Ok("Left click");

                case "RIGHT_CLICK":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_RIGHTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_RIGHTUP);
                    return Ok("Right click");

                case "DOUBLE_CLICK":
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTDOWN);
                    SendMouseInput(0, 0, 0, MOUSEEVENTF_LEFTUP);
                    return Ok("Double click");

                case "SCROLL":
                    int delta = json["delta"]?.ToObject<int>() ?? 0;
                    SendMouseInput(0, 0, (uint)(delta * 120), MOUSEEVENTF_WHEEL);
                    return Ok("Scroll");

                default:
                    return Error($"Unknown mouse action: {acao}");
            }
        }

        private static void SendMouseInput(int dx, int dy, uint data, uint flags)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type          = INPUT_MOUSE;
            inputs[0].U.mi.dx       = dx;
            inputs[0].U.mi.dy       = dy;
            inputs[0].U.mi.mouseData = data;
            inputs[0].U.mi.dwFlags  = flags;
            SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        }

        // ─── KEYBOARD ─────────────────────────────────────────────────────────

        private static string HandleTeclado(JObject json)
        {
            string? texto = json["texto"]?.ToString();
            if (texto != null)
            {
                foreach (char c in texto) SendUnicodeChar(c);
                return Ok($"Text typed: {texto}");
            }

            string tecla = json["tecla"]?.ToString()?.ToUpper() ?? "";
            if (!TeclaEspecial.TryGetValue(tecla, out ushort vk))
                return Error($"Unknown key: {tecla}");

            SendVirtualKey(vk);
            return Ok($"Key sent: {tecla}");
        }

        private static void SendUnicodeChar(char c)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type         = INPUT_KEYBOARD;
            inputs[0].U.ki.wScan   = c;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;
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
            ["COPY"]      = 0x43,
        };

        // ─── WIN32 API ────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const ushort VK_LEFT           = 0x25;
        private const ushort VK_RIGHT          = 0x27;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x01;
        private const uint KEYEVENTF_KEYUP       = 0x02;
        private const uint KEYEVENTF_UNICODE      = 0x04;

        private const uint INPUT_MOUSE    = 0;
        private const uint INPUT_KEYBOARD = 1;

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
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
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
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFOEX
        {
            public int  cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private delegate bool MonitorEnumProc(
            IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        private const uint MONITORINFOF_PRIMARY = 1;
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
            var result = _monitorEnumResult;
            _monitorEnumResult = null;
            return result;
        }
    }
}