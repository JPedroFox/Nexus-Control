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
                    "MEDIA"        => HandleMedia(json),
                    "SISTEMA"      => HandleSistema(json),
                    "SCREENSHOT"   => HandleScreenshot(),
                    "KILL_PROCESS" => HandleKillProcess(json),
                    _              => Error($"Comando desconhecido: {cmd}")
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

        // ─── HELPERS ──────────────────────────────────────────────────────────

        private static string Ok(string msg) =>
            JsonConvert.SerializeObject(new { status = "OK", msg });

        private static string Error(string msg) =>
            JsonConvert.SerializeObject(new { status = "ERROR", msg });

        // ─── WIN32 API ────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        private const byte VK_MEDIA_PLAY_PAUSE   = 0xB3;
        private const byte VK_MEDIA_NEXT_TRACK   = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK   = 0xB1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x01;
        private const uint KEYEVENTF_KEYUP       = 0x02;

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