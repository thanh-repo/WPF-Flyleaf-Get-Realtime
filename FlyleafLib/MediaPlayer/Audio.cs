﻿using System;
using System.Collections.ObjectModel;

using Vortice.Multimedia;
using Vortice.XAudio2;

using static Vortice.XAudio2.XAudio2;

using FlyleafLib.MediaFramework.MediaContext;
using FlyleafLib.MediaFramework.MediaFrame;
using FlyleafLib.MediaFramework.MediaStream;

using static FlyleafLib.Logger;

namespace FlyleafLib.MediaPlayer
{
    public class Audio : NotifyPropertyChanged
    {
        #region Properties
        /// <summary>
        /// Embedded Streams
        /// </summary>
        public ObservableCollection<AudioStream>
                        Streams         => decoder?.VideoDemuxer.AudioStreams; // TBR: We miss AudioDemuxer embedded streams

        /// <summary>
        /// Whether the input has audio and it is configured
        /// </summary>
        public bool     IsOpened        { get => isOpened;      internal set => Set(ref _IsOpened, value); }
        internal bool   _IsOpened, isOpened;

        public string   Codec           { get => codec;         internal set => Set(ref _Codec, value); }
        internal string _Codec, codec;

        ///// <summary>
        ///// Audio bitrate (Kbps)
        ///// </summary>
        public double   BitRate         { get => bitRate;       internal set => Set(ref _BitRate, value); }
        internal double _BitRate, bitRate;

        public int      Bits            { get => bits;          internal set => Set(ref _Bits, value); }
        internal int    _Bits, bits;

        public int      Channels        { get => channels;      internal set => Set(ref _Channels, value); }
        internal int    _Channels, channels;

        /// <summary>
        /// Audio player's channels out (currently 2 channels supported only)
        /// </summary>
        public int      ChannelsOut     { get; } = 2;

        public string   ChannelLayout   { get => channelLayout; internal set => Set(ref _ChannelLayout, value); }
        internal string _ChannelLayout, channelLayout;

        ///// <summary>
        ///// Total Dropped Frames
        ///// </summary>
        public int      FramesDropped   { get => framesDropped;     internal set => Set(ref _FramesDropped, value); }
        internal int    _FramesDropped, framesDropped;

        public int      FramesDisplayed { get => framesDisplayed;   internal set => Set(ref _FramesDisplayed, value); }
        internal int    _FramesDisplayed, framesDisplayed;

        public string   SampleFormat    { get => sampleFormat;  internal set => Set(ref _SampleFormat, value); }
        internal string _SampleFormat, sampleFormat;

        /// <summary>
        /// Audio sample rate (in/out)
        /// </summary>
        public int      SampleRate      { get => sampleRate;    internal set => Set(ref _SampleRate, value); }
        internal int    _SampleRate, sampleRate;

        /// <summary>
        /// Audio player's volume / amplifier (valid values 0 - no upper limit)
        /// </summary>
        public int Volume
        {
            get
            {
                lock (locker)
                    return sourceVoice == null || Mute ? _Volume : (int) ((decimal)sourceVoice.Volume * 100);
            }
            set
            {
                if (value > Config.Player.VolumeMax || value < 0)
                    return;

                if (value == 0)
                    Mute = true;
                else if (Mute)
                {
                    _Volume = value;
                    Mute = false;
                }
                else
                {
                    lock (locker)
                        if (sourceVoice != null)
                            sourceVoice.Volume = Math.Max(0, value / 100.0f);
                }

                Set(ref _Volume, value, false);
            }
        }
        int _Volume;

        /// <summary>
        /// Audio player's mute
        /// </summary>
        public bool Mute
        {
            get => mute;
            set
            {
                lock (locker)
                {
                    if (sourceVoice == null)
                        return;

                    if (value)
                        sourceVoice.Volume = 0;
                    else
                        sourceVoice.Volume = _Volume / 100.0f;
                }

                Set(ref mute, value, false);
            }
        }
        private bool mute = false;

        /// <summary>
        /// <para>Audio player's current device (available devices can be found on Engine.Audio.Devices)/></para>
        /// </summary>
        public string Device
        {
            get => _Device;
            set
            {
                if (value == null || _Device == value)
                    return; 

                _Device     = value;
                _DeviceId   = Engine.Audio.GetDeviceId(value);

                Initialize();

                Utils.UI(() => Raise(nameof(Device)));
            }
        }
        internal string _Device = Engine.Audio.DefaultDeviceName;
        internal void RaiseDevice() { Utils.UI(() => Raise(nameof(Device))); } // Required for Selected Items on the Devices observation list (as we clear it everytime)

        public string DeviceId
        {
            get => _DeviceId;
            set
            {
                if (value == null || _DeviceId == value)
                    return; 

                _DeviceId   = value;
                _Device     = Engine.Audio.GetDeviceName(value);

                Initialize();

                Utils.UI(() => Raise(nameof(DeviceId)));
            }
        }
        internal string _DeviceId = Engine.Audio.DefaultDeviceId;

        public int BuffersQueued {
            get
            {                
                lock (locker)
                {
                    if (sourceVoice == null)
                        return 0;

                    return sourceVoice.State.BuffersQueued;
                }
            }
        }
        #endregion

        #region Declaration
        Player                  player;
        Config                  Config => player.Config;
        DecoderContext          decoder => player?.decoder;

        Action                  uiAction;
        readonly object         locker = new object();

        IXAudio2                xaudio2;
        internal IXAudio2MasteringVoice  masteringVoice;
        IXAudio2SourceVoice     sourceVoice;
        WaveFormat              waveFormat = new WaveFormat(48000, 16, 2); // Output Audio Device
        #endregion
        public Audio(Player player)
        {
            this.player = player;

            uiAction = () =>
            {
                IsOpened        = IsOpened;
                Codec           = Codec;
                BitRate         = BitRate;
                Bits            = Bits;
                Channels        = Channels;
                ChannelLayout   = ChannelLayout;
                SampleFormat    = SampleFormat;
                SampleRate      = SampleRate;

                FramesDisplayed     = FramesDisplayed;
                FramesDropped       = FramesDropped;
            };

            Volume = Config.Player.VolumeMax / 2;
            Initialize();
        }

        internal void Initialize()
        {
            lock (locker)
            {
                if (Engine.Audio.Failed)
                {
                    Config.Audio.Enabled = false;
                    return;
                }

                sampleRate = decoder != null && decoder.AudioStream != null && decoder.AudioStream.SampleRate > 0 ? decoder.AudioStream.SampleRate : 48000;

                player.Log.Info($"Initialiazing audio ({Device} @ {SampleRate}Hz)");

                Dispose();
                try
                {
                    xaudio2 = XAudio2Create();
                    try
                    {
	                    masteringVoice = xaudio2.CreateMasteringVoice(0, 0, AudioStreamCategory.GameEffects, _Device == Engine.Audio.DefaultDeviceName ? null : Engine.Audio.GetDeviceId(_Device));
                    } catch (Exception) // Win 7/8 compatibility issue https://social.msdn.microsoft.com/Forums/en-US/4989237b-814c-4a7a-8a35-00714d36b327/xaudio2-how-to-get-device-id-for-mastering-voice?forum=windowspro-audiodevelopment
                    {
                        masteringVoice = xaudio2.CreateMasteringVoice(0, 0, AudioStreamCategory.GameEffects, _Device == Engine.Audio.DefaultDeviceName ? null : (@"\\?\swd#mmdevapi#" + Engine.Audio.GetDeviceId(_Device).ToLower() + @"#{e6327cad-dcec-4949-ae8a-991e976a79d2}")); 
                    }
                    sourceVoice = xaudio2.CreateSourceVoice(waveFormat, true);
                    sourceVoice.SetSourceSampleRate(SampleRate);
                    sourceVoice.Start();

                    masteringVoice.Volume = Config.Player.VolumeMax / 100.0f;
                    bool oldMute = mute;
                    Volume = _Volume;
                    Mute = oldMute;

                } catch (Exception e)
                {
                    player.Log.Info($"Audio initialization failed ({e.Message})");
                    Config.Audio.Enabled = false;
                }
            }
        }
        internal void Dispose()
        {
            lock (locker)
            {
                if (xaudio2 == null)
                    return;

                xaudio2.        Dispose();
                sourceVoice?.   Dispose();
                masteringVoice?.Dispose();
                xaudio2         = null;
                sourceVoice     = null;
                masteringVoice  = null;
            }
        }
        
        // TBR: Very rarely could crash the app on audio device change while playing? Requires two locks (Audio's locker and aFrame)
        // The process was terminated due to an internal error in the .NET Runtime at IP 00007FFA6725DA03 (00007FFA67090000) with exit code c0000005.
        [System.Security.SecurityCritical]
        internal void AddSamples(AudioFrame aFrame)
        {
            try
            {
                sourceVoice.SubmitSourceBuffer(new AudioBuffer(aFrame.dataPtr, aFrame.dataLen));
            } catch (Exception e) // Happens on audio device changed/removed
            {
                if (CanDebug) player.Log.Debug($"[Audio] Add samples failed ({e.Message})");
            }
        }
        internal void ClearBuffer()
        {
            lock (locker)
                sourceVoice?.FlushSourceBuffers();
        }

        internal void Reset()
        {
            codec           = null;
            bitRate         = 0;
            bits            = 0;
            channels        = 0;
            channelLayout   = null;
            sampleFormat    = null;
            isOpened        = false;

            ClearBuffer();
            player.UIAdd(uiAction);
        }
        internal void Refresh()
        {
            if (decoder.AudioStream == null) { Reset(); return; }

            codec           = decoder.AudioStream.Codec;
            bits            = decoder.AudioStream.Bits;
            channels        = decoder.AudioStream.Channels;
            channelLayout   = decoder.AudioStream.ChannelLayoutStr;
            sampleFormat    = decoder.AudioStream.SampleFormatStr;
            isOpened        =!decoder.AudioDecoder.Disposed;

            framesDisplayed = 0;
            framesDropped   = 0;

            if (SampleRate!= decoder.AudioStream.SampleRate)
                Initialize();

            player.UIAdd(uiAction);
        }
        internal void Enable()
        {
            bool wasPlaying = player.IsPlaying;

            decoder.OpenSuggestedAudio();

            player.ReSync(decoder.AudioStream, (int) (player.CurTime / 10000), true);

            Refresh();
            player.UIAll();

            if (wasPlaying || Config.Player.AutoPlay)
                player.Play();
        }
        internal void Disable()
        {
            if (!IsOpened)
                return;

            decoder.CloseAudio();

            player.aFrame = null;

            if (!player.Video.IsOpened)
            {
                player.canPlay = false;
                player.UIAdd(() => player.CanPlay = player.CanPlay);
            }

            Reset();
            player.UIAll();
        }

        public void DelayAdd()
        {
            Config.Audio.Delay += Config.Player.AudioDelayOffset;
        }
        public void DelayAdd2()
        {
            Config.Audio.Delay += Config.Player.AudioDelayOffset2;
        }
        public void DelayRemove()
        {
            Config.Audio.Delay -= Config.Player.AudioDelayOffset;
        }
        public void DelayRemove2()
        {
            Config.Audio.Delay -= Config.Player.AudioDelayOffset2;
        }
        public void Toggle()
        {
            Config.Audio.Enabled = !Config.Audio.Enabled;
        }
        public void ToggleMute()
        {
            Mute = !Mute;
        }
        public void VolumeUp()
        {
            if (Volume == Config.Player.VolumeMax) return;
            Volume = Math.Min(Volume + Config.Player.VolumeOffset, Config.Player.VolumeMax);
        }
        public void VolumeDown()
        {
            if (Volume == 0) return;
            Volume = Math.Max(Volume - Config.Player.VolumeOffset, 0);
        }
    }
}
