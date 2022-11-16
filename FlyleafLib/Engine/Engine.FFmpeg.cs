﻿using System;
using System.Runtime.InteropServices;

using FFmpeg.AutoGen;
using static FFmpeg.AutoGen.ffmpeg;

namespace FlyleafLib
{
    public class FFmpegEngine
    {
        public string   Folder      { get; private set; }
        public string   Version     { get; private set; }
        public bool     IsVer5      { get; private set; }

        public static int AV_LOG_BUFFER_SIZE = 5 * 1024;

        internal FFmpegEngine()
        {
            try
            {
                Engine.Log.Info($"Loading FFmpeg libraries from '{Engine.Config.FFmpegPath}'");
                Folder  = Utils.GetFolderPath(Engine.Config.FFmpegPath);
                RootPath= Folder;
                uint ver= avformat_version();
                IsVer5  = ver >> 16 > 58;
                Version = $"{ver >> 16}.{ver >> 8 & 255}.{ver & 255}";

                if (Engine.Config.FFmpegDevices)
                    try { avdevice_register_all(); } catch { Engine.Log.Error("FFmpeg failed to load avdevices/avfilters/postproc"); }

                SetLogLevel();

                Engine.Log.Info($"FFmpeg Loaded (Location: {Folder}, Ver: {Version}, #Files: {LoadedLibraries.Count})");
            } catch (Exception e)
            {
                Engine.Log.Error($"Loading FFmpeg libraries '{Engine.Config.FFmpegPath}' failed\r\n{e.Message}\r\n{e.StackTrace}");
                throw new Exception($"Loading FFmpeg libraries '{Engine.Config.FFmpegPath}' failed");
            }
        }

        internal void SetLogLevel()
        {
            if (Engine.Config.FFmpegLogLevel != FFmpegLogLevel.Quiet)
            {
                av_log_set_level((int)Engine.Config.FFmpegLogLevel);
                av_log_set_callback(LogFFmpeg);
            }
            else
            {
                av_log_set_level((int)FFmpegLogLevel.Quiet);
                av_log_set_callback(null);
            }
        }

        internal unsafe static av_log_set_callback_callback LogFFmpeg = (p0, level, format, vl) =>
        {
            if (level > av_log_get_level()) return;

            var buffer = stackalloc byte[AV_LOG_BUFFER_SIZE];
            var printPrefix = 1;
            av_log_format_line2(p0, level, format, vl, buffer, AV_LOG_BUFFER_SIZE, &printPrefix);
            var line = Marshal.PtrToStringAnsi((IntPtr)buffer);

            Logger.Output($"{DateTime.Now.ToString(Engine.Config.LogDateTimeFormat)} | FFmpeg | {((FFmpegLogLevel)level).ToString().PadRight(7, ' ')} | {line.Trim()}");
        };

        internal unsafe static string ErrorCodeToMsg(int error)
        {
            byte* buffer = stackalloc byte[AV_LOG_BUFFER_SIZE];
            av_strerror(error, buffer, (ulong)AV_LOG_BUFFER_SIZE);
            return Marshal.PtrToStringAnsi((IntPtr)buffer);
        }
    }

    public enum FFmpegLogLevel
    {
        Quiet       =-0x08,
        SkipRepeated= 0x01,
        PrintLevel  = 0x02,
        Fatal       = 0x08,
        Error       = 0x10,
        Warning     = 0x18,
        Info        = 0x20,
        Verbose     = 0x28,
        Debug       = 0x30,
        Trace       = 0x38,
        MaxOffset   = 0x40,
    }
}
