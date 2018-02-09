using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    public sealed partial class WvvVideoPlayer : UserControl, INotifyPropertyChanged, IWvvVideoPlayer, IDisposable
    {
        /**
         * 動画再生ウィンドウのサイズ計算方法
         */
        public enum LayoutMode
        {
            Width,       // 指定された幅になるように高さを調整
            Height,      // 指定された高さになるよう幅を調整
            Inside,      // 指定された矩形に収まるよう幅、または、高さを調整
            Fit          // 指定された矩形にリサイズする
        }

        public WvvVideoPlayer()
        {
            this.DataContext = this;
            this.InitializeComponent();
        }
        
        #region IWvvVideoPlayer i/f implementation

        /**
         * シーク
         */
        public double SeekPosition
        {
            get
            {
                var ss = Session;
                if (null != ss)
                {
                    return ss.Position.TotalMilliseconds;
                }
                return 0;
            }
            set
            {
                var ss = Session;
                if(null!=ss)
                {
                    ss.Position = TimeSpan.FromMilliseconds(value);
                }
            }
        }

        /**
         * 動画の再生/停止
         */
        public bool IsPlaying
        {
            get { return mPlayerState == PlayerState.PLAYING; }
            set
            {
                if(mPlayerState == PlayerState.PLAYING)
                {
                    Player.Pause();
                }
                else
                {
                    Player.Play();
                }
            }
        }

        /**
         * VideoPlayerの状態
         */
        private PlayerState mPlayerState = PlayerState.NONE;
        public PlayerState PlayerState
        {
            get { return mPlayerState; }
            private set
            {
                if(value!=mPlayerState)
                {
                    mPlayerState = value;
                    PlayerStateChanged?.Invoke(this, mPlayerState);
                }
            }
        }

        /**
         * FullScreen / 通常モードの取得・切り替え
         */
        public bool FullScreen
        {
            get
            {
                return mPlayerElement.IsFullWindow;
            }
            set
            {
                mPlayerElement.IsFullWindow = value;
            }
        }


        /**
         * Playerビューの幅
         * ControlPanelは、この幅に合わせて伸縮する。
         */
        public double PlayerWidth
        {
            get { return PlayerSize.Width; }
        }

        /**
         * Videoの総再生時間
         * MediaClipから取り出した値と、MediaPlaybackSessionから取り出した値が異なるようなので、Playerからもらうことにする。
         */
        public double TotalRange
        {
            get
            {
                return PlayerState != PlayerState.NONE ? Session.NaturalDuration.TotalMilliseconds : 0;
            }
        }

        /**
         * VideoPlayerの状態変更通知イベント
         */
        public event WvvPlayerStateChanged PlayerStateChanged;

        /**
         * VideoPlayerのサイズ変更通知イベント
         */
        public event WvvPlayerWidthChanged PlayerWidthChanged;

        #endregion

        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        #region Bindings
        /**
         * 動画再生ウィンドウのサイズ計算方法
         */
        public LayoutMode PlayerLayout
        {
            get { return mPlayerLayout; }
            set
            {
                if (mPlayerLayout != value)
                {
                    mPlayerLayout = value;
                    updatePlayerSize();
                }
            }
        }
        private LayoutMode mPlayerLayout = LayoutMode.Fit;

        /**
         * プレイヤーの配置サイズのヒント
         */
        public Size LayoutSize
        {
            get { return mLayoutSize; }
            set
            {
                if (value != mLayoutSize)
                {
                    mLayoutSize = value;
                    updatePlayerSize();
                }
            }
        }
        private Size mLayoutSize = new Size(300, 300);

        /**
         * 動画再生ウィンドウのサイズ
         */
        public Size PlayerSize
        {
            get
            {
                return mPlayerSize;
            }
        }
        private Size mPlayerSize = new Size(300, 300);

        /**
         * PlayerLayout/LayoutSize/VideoSize が変更されたときに、PlayerSizeを更新する。
         */
        private void updatePlayerSize()
        {
            Size size = fitSize(PlayerLayout);
            if (size != mPlayerSize)
            {
                bool widthChanged = size.Width != mPlayerSize.Width;
                mPlayerSize = size;
                notify("PlayerSize");
                if (widthChanged)
                {
                    PlayerWidthChanged?.Invoke(this, size.Width);
                }
            }
        }

        /**
         * PlayerLayout/LayoutSize/VideoSize から実際のサイズを決定する。
         */
        private Size fitSize(LayoutMode mode)
        {
            switch (mode)
            {
                case LayoutMode.Fit:
                    return mLayoutSize;
                case LayoutMode.Width:
                    return new Size(mLayoutSize.Width, mVideoSize.Height * mLayoutSize.Width / mVideoSize.Width);
                case LayoutMode.Height:
                    return new Size(mVideoSize.Width * mLayoutSize.Height / mVideoSize.Height, mLayoutSize.Height);
                case LayoutMode.Inside:
                default:
                    {
                        double rw = mLayoutSize.Width / mVideoSize.Width;
                        double rh = mLayoutSize.Height / mVideoSize.Height;
                        if (rw < rh)
                        {
                            return new Size(mLayoutSize.Width, mVideoSize.Height * rw);
                        }
                        else
                        {
                            return new Size(mVideoSize.Width * rh, mLayoutSize.Height);
                        }
                    }
            }
        }

        /**
         * 動画のNatural Size
         */
        public Size VideoSize
        {
            get { return mVideoSize; }
            private set
            {
                if (mVideoSize != value)
                {
                    mVideoSize = value;
                    updatePlayerSize();
                }
            }
        }
        private Size mVideoSize = new Size(300, 300);


        #endregion

        #region Privates

        private MediaPlayer mInternalPlayer = null;
        private StorageFile mTempSource;

        private MediaPlayer Player
        {
            get { return mPlayerElement?.MediaPlayer; }
        }

        private MediaPlaybackSession Session
        {
            get { return mInternalPlayer?.PlaybackSession; }
        }

        private long mFullWindowListenerToken = 0;
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if(null==mInternalPlayer)
            {
                mInternalPlayer = new MediaPlayer();
            }
            mPlayerElement.SetMediaPlayer(mInternalPlayer);
            Player.IsVideoFrameServerEnabled = false;
            Session.PlaybackStateChanged += PBS_PlaybackStateChanged;
            mFullWindowListenerToken = mPlayerElement.RegisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, MPE_FullWindowChanged);

            if (null != mTempSource)
            {
                SetSource(mTempSource);
            }
        }

        private void MPE_FullWindowChanged(DependencyObject sender, DependencyProperty dp)
        {
            var mpe = sender as MediaPlayerElement;
            if (null != mpe)
            {
                if (!mpe.IsFullWindow)
                {
                    mPlayerElement.AreTransportControlsEnabled = false;
                }
                else
                {
                    mPlayerElement.AreTransportControlsEnabled = true;
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Player.Pause();
            Session.PlaybackStateChanged -= PBS_PlaybackStateChanged;
            mPlayerElement.UnregisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, mFullWindowListenerToken);
            mPlayerElement.SetMediaPlayer(null);
            mInternalPlayer.Source = null;
        }

        public void Dispose()
        {
            if(null!=mInternalPlayer)
            {
                mPlayerElement.SetMediaPlayer(null);
                mInternalPlayer.Dispose();
                mInternalPlayer = null;
            }
        }

        private async void PBS_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.Buffering:
                    default:
                        return;
                    case MediaPlaybackState.None:
                    case MediaPlaybackState.Opening:
                        PlayerState = PlayerState.NONE;
                        return;
                    case MediaPlaybackState.Paused:
                        PlayerState = PlayerState.PAUSED;
                        return;
                    case MediaPlaybackState.Playing:
                        PlayerState = PlayerState.PLAYING;
                        return;
                }
            });
        }
        #endregion

        #region Public APIs

        public async void SetSource(StorageFile source)
        {
            if(null==mInternalPlayer)
            {
                mTempSource = source;
                return;
            }
            mTempSource = null;

            PlayerState = PlayerState.NONE;
            if(source==null)
            {
                mInternalPlayer.Source = null;
                return;
            }
            var loader = await WvvMediaLoader.LoadAsync(mInternalPlayer, MediaSource.CreateFromStorageFile(source), this);
            if(loader.Opened)
            {
                VideoSize = new Size(mInternalPlayer.PlaybackSession.NaturalVideoWidth, mInternalPlayer.PlaybackSession.NaturalVideoHeight);
            }
        }

        #endregion
    }
}
