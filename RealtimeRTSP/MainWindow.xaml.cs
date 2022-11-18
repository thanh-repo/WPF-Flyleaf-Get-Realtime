using FlyleafLib;
using FlyleafLib.MediaPlayer;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace RealtimeRTSP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _TimeRtsp;
        public string TimeRtsp { get { return _TimeRtsp; } set { _TimeRtsp = value; OnPropertyChanged(); } }
        public Player Player { get; set; }
        public Player Player1 { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            Engine.Start(DefaultEngineConfig());
            Player = new Player(PlayerDefaultConfig());
            Player.OpenAsync("rtsp://admin:Password1!@192.168.219.111:554/0/onvif/profile2/media.smp");
            Player1 = new Player(PlayerDefaultConfig());
            Player1.OpenAsync("rtsp://rtsp.stream/pattern");
            Player.OpenCompleted += Player_OpenCompleted;
            DataContext = this;
        }
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private void Player_OpenCompleted(object? sender, OpenCompletedArgs e)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    DateTime TimeNow = epoch.AddTicks((Player.MainDemuxer.StartTimestamp * 10) + Player.MainDemuxer.CurTime).AddHours(9);
                    TimeRtsp = TimeNow.ToString("yyyy-MM-dd, HH:mm:ss.ffffff");
                }
            });
        }

        public static DateTime ToDateTimeForEpochMSec(double microseconds)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
            long ticks = (long)(microseconds * ticksPerMicrosecond);
            DateTime tempDate = epoch.AddTicks(ticks);
            return tempDate;
        }

        private EngineConfig DefaultEngineConfig()
        {
            EngineConfig engineConfig = new EngineConfig();

            engineConfig.PluginsPath = ":Plugins";
            engineConfig.FFmpegPath = ":FFmpeg";
            engineConfig.HighPerformaceTimers = false;
            engineConfig.UIRefresh = true;
            engineConfig.UIRefreshInterval = 100;
            return engineConfig;
        }

        public static Config PlayerDefaultConfig()
        {
            Config config = new();
            config.Audio.Enabled = false;
            //config.Player.mouse = false;
            //config.Player.KeyBindings.Enabled = false;
            config.Player.LowLatencyMaxVideoPackets = 0;
            config.Video.GPUAdapter = "";
            return config;
        }

        #region [ PropertyChanged ]
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
