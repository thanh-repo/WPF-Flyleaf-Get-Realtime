﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Vortice.DXGI;

namespace FlyleafLib
{
    public class VideoEngine
    {
        /// <summary>
        /// List of Video Capture Devices
        /// </summary>
        public ObservableCollection<string>
                                CapDevices  { get; private set; } = new ObservableCollection<string>();

        /// <summary>
        /// List of GPU Adpaters <see cref="Config.VideoConfig.GPUAdapter"/>
        /// </summary>
        public Dictionary<long, GPUAdapter>
                                GPUAdapters { get; private set; }

        /// <summary>
        /// List of GPU Outputs from default GPU Adapter
        /// </summary>
        public List<GPUOutput>  Screens     { get; private set; } = new List<GPUOutput>();

        internal IDXGIFactory2 Factory;

        internal VideoEngine()
        {
            if (DXGI.CreateDXGIFactory1(out Factory).Failure)
                throw new InvalidOperationException("Cannot create IDXGIFactory1");

            GPUAdapters = GetAdapters();
        }

        private Dictionary<long, GPUAdapter> GetAdapters()
        {
            Dictionary<long, GPUAdapter> adapters = new Dictionary<long, GPUAdapter>();
            
            string dump = "";

            for (int i=0; Factory.EnumAdapters1(i, out IDXGIAdapter1 adapter).Success; i++)
            {
                bool hasOutput = false;

                List<GPUOutput> outputs = new List<GPUOutput>();

                int maxHeight = 0;
                for (int o=0; adapter.EnumOutputs(o, out IDXGIOutput output).Success; o++)
                {
                    GPUOutput gpout = new GPUOutput()
                    {
                        Id        = GPUOutput.GPUOutputIdGenerator++,
                        DeviceName= output.Description.DeviceName,
                        Left      = output.Description.DesktopCoordinates.Left,
                        Top       = output.Description.DesktopCoordinates.Top,
                        Right     = output.Description.DesktopCoordinates.Right,
                        Bottom    = output.Description.DesktopCoordinates.Bottom,
                        IsAttached= output.Description.AttachedToDesktop,
                        Rotation  = (int)output.Description.Rotation
                    };

                    if (maxHeight < gpout.Height)
                        maxHeight = gpout.Height;

                    outputs.Add(gpout);

                    if (gpout.IsAttached)
                        hasOutput = true;

                    output.Dispose();
                }

                if (Screens.Count == 0 && outputs.Count > 0)
                    Screens = outputs;

                adapters[adapter.Description1.Luid] = new GPUAdapter()
                {
                    SystemMemory    = adapter.Description1.DedicatedSystemMemory,
                    VideoMemory     = adapter.Description1.DedicatedVideoMemory,
                    SharedMemory    = adapter.Description1.SharedSystemMemory,
                    Vendor          = VendorIdStr(adapter.Description1.VendorId),
                    Description     = adapter.Description1.Description,
                    Id              = adapter.Description1.DeviceId,
                    Luid            = adapter.Description1.Luid,
                    MaxHeight       = maxHeight,
                    HasOutput       = hasOutput,
                    Outputs         = outputs
                };

                dump += $"[#{i+1}] {adapters[adapter.Description1.Luid]}\r\n";

                adapter.Dispose();
            }

            Engine.Log.Info($"GPU Adapters\r\n{dump}");

            return adapters;
        }

        public GPUOutput GetScreenFromPosition(int top, int left)
        {
            foreach(var screen in Screens)
            {
                if (top >= screen.Top && top <= screen.Bottom && left >= screen.Left && left <= screen.Right)
                    return screen;
            }

            return null;
        }

        private static string VendorIdStr(int vendorId)
        {
            switch (vendorId)
            {
                case 0x1002:
                    return "ATI";
                case 0x10DE:
                    return "NVIDIA";
                case 0x1106:
                    return "VIA";
                case 0x8086:
                    return "Intel";
                case 0x5333:
                    return "S3 Graphics";
                case 0x4D4F4351:
                    return "Qualcomm";
                default:
                    return "Unknown";
            }
        }
    }
}
