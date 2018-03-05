using System;
using System.ComponentModel;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using wvv.utils;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    /**
     * 動画プレーヤー
     * - MediaPlayerElement/MediaPlayer のカプセル化
     * - WvvVideoCotrolPanel との接続
     * - 動画読み込み時のグルグル表示
     * - 動画ソースとして、StorageFile / Uri / IWvvCache をサポート
     */
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
                if (null != Player)
                {
                    if (mPlayerState == PlayerState.PLAYING)
                    {
                        Player.Pause();
                    }
                    else
                    {
                        Player.Play();
                    }
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

        public object Source
        {
            set
            {
                if (value is StorageFile)
                {
                    SetSource((StorageFile)value);
                }
                else if (value is Uri)
                {
                    SetSource((Uri)value);
                }
                else
                {
                    var err = new ArgumentException("invalid source type.");
                    Error.SetError(err);
                    throw err;
                }
            }
        }

        /**
         * Videoの総再生時間
         * MediaClipから取り出した値と、MediaPlaybackSessionから取り出した値が異なるようなので、Playerからもらうことにする。
         * 。。。気のせい
         */
        //public double TotalRange
        //{
        //    get
        //    {
        //        return PlayerState != PlayerState.NONE ? Session.NaturalDuration.TotalMilliseconds : 0;
        //    }
        //}

        /**
         * VideoPlayerの状態変更通知イベント
         */
        public event WvvPlayerStateChanged PlayerStateChanged;

        /**
         * VideoPlayerのサイズ変更通知イベント
         */
        public event WvvPlayerValueChanged PlayerWidthChanged;

        /**
         * TotalRange / VideoSize が取得できた
         */
        public event WvvPlayerInitialized PlayerInitialized;

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


        /**
         * 動画ロード中フラグ（ぐるぐる表示用）
         */
        private bool mMovieLoading = false;
        public bool MovieLoading
        {
            get { return mMovieLoading; }
            set
            {
                if(value != mMovieLoading)
                {
                    mMovieLoading = value;
                    notify("MovieLoading");
                }
            }
        }

        /**
         * エラー
         */
        public WvvError Error { get; } = new WvvError();

        /**
         * 動画の読み込みが完了したら、自動的に再生を開始する場合は true にする。
         * Loaded前にセットしておかないと有効にならない。
         */
        public bool AutoStart { get; set; } = false;

        /**
         * ミュート
         */
        private bool mIsMuted = false;
        public bool IsMuted
        {
            get => mIsMuted;
            set
            {
                mIsMuted = value;
                if(null!=mInternalPlayer)
                {
                    mInternalPlayer.IsMuted = mIsMuted;
                }
            }
        }
        

        #endregion

        #region Privates

        private MediaPlayer mInternalPlayer = null;
        private MediaSource mTempSource;
        private long mFullWindowListenerToken = 0;

        /**
         * MediaPlayerElementに接続されたMediaPlayer 
         */
        private MediaPlayer Player
        {
            get { return mPlayerElement?.MediaPlayer; }
        }

        private MediaPlaybackSession Session
        {
            get { return mInternalPlayer?.PlaybackSession; }
        }

        /**
         * 初期化時の処理
         */
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if(null==mInternalPlayer)
            {
                mInternalPlayer = new MediaPlayer();
                mInternalPlayer.AutoPlay = AutoStart;
                mInternalPlayer.IsMuted = IsMuted;
            }
            mPlayerElement.SetMediaPlayer(mInternalPlayer);
            Player.IsVideoFrameServerEnabled = false;
            Session.PlaybackStateChanged += PBS_PlaybackStateChanged;
            mFullWindowListenerToken = mPlayerElement.RegisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, MPE_FullWindowChanged);
            mInternalPlayer.MediaFailed += MB_Failed;

            if (null != mTempSource)
            {
                SetSourceInternal(mTempSource);
            }
        }

        /**
         * 終了時の処理
         */
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Session.PlaybackStateChanged -= PBS_PlaybackStateChanged;
            mPlayerElement.UnregisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, mFullWindowListenerToken);
            mInternalPlayer.MediaFailed -= MB_Failed;
            mPlayerElement.SetMediaPlayer(null);
            Reset();
        }

        /**
         * リソース解放
         */
        public void Dispose()
        {
            if(null!=mInternalPlayer)
            {
                mInternalPlayer.Pause();
                mInternalPlayer.Source = null;
                mPlayerElement.SetMediaPlayer(null);
                mInternalPlayer.Dispose();
                mInternalPlayer = null;
            }
                if (null != mCache)
                {
                    mCache.Release();
                    mCache = null;
                }
            }


        /**
         * 最大化する・元に戻す を検知するためのイベントハンドラ
         */
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

        /**
         * MediaPlayerの状態監視
         */
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

        /**
         * MediaPlayerでエラーが発生したときの処理
         */
        private async void MB_Failed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                CmLog.debug("WvvVideoPlayer.MB_Failed: MediaFailed");
                if (null != args.ErrorMessage && args.ErrorMessage.Length > 0)
                {
                    CmLog.debug(args.ErrorMessage);
                    Error.SetError(args.ErrorMessage);
                }
                if (null != args.ExtendedErrorCode)
                {
                    CmLog.error(args.ExtendedErrorCode.Message);
                    Error.SetError(args.ExtendedErrorCode);
                }
            });
        }


        /**
         * プレーヤー上のタップ（再生・停止のトグル）
         */
        private void OnPlayerTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            IsPlaying = !IsPlaying;
            e.Handled = true;
        }


        #endregion

        #region Public APIs

        IWvvCache mCache = null;

        /**
         * URIをキャッシュしないでソースにセット
         * 
         * キャッシュは使わず、URLを直接読み込む。キャッシュを使う場合は、呼び出し元でIWvvCacheインスタンスを用意して、SetCache(IWvvCache)を呼ぶ。
         */
        public void SetSource(Uri uri)
        {
            Reset();
            if (null != uri)
            {
                SetSourceInternal(MediaSource.CreateFromUri(uri));
            }
        }

        /**
         * StorageFileをソースにセット
         */
        public void SetSource(StorageFile file)
        {
            Reset();
            if (null != file)
            {
                SetSourceInternal(MediaSource.CreateFromStorageFile(file));
            }
        }

        /**
         * キャッシュオブジェクトをソースとしてセット
         */
        public void SetSource(IWvvCache cache)
        {
            Reset();
            if (null == cache)
            {
                return;
            }

            var file = cache.CacheFile;
            if (null != file)
            {
                // キャッシュファイルがあればそれを使用
                mCache = cache;
                mCache.AddRef();
                SetSourceInternal(MediaSource.CreateFromStorageFile(file));
            }
            else
            {
                // なければ直接URIをロード
                SetSourceInternal(MediaSource.CreateFromUri(cache.URI));
            }
        }


        /**
         * Playerの状態を再初期化する
         */
        public void Reset()
        {
            if (null != mCache)
            {
                mCache.Release();
                mCache = null;
            }
            mTempSource = null;
            if (null != mInternalPlayer)
            {
                mInternalPlayer.Pause();
                mInternalPlayer.Source = null;
            }
            Error.Reset();
            PlayerState = PlayerState.NONE;
        }

        /**
         * MediaSourceをセット
         */
        private async void SetSourceInternal(MediaSource source)
        {
            if (null==mInternalPlayer)
            {
                mTempSource = source;
                return;
            }
            if (source==null)
            {
                return;
            }
            MovieLoading = true;
            var loader = await WvvMediaLoader.LoadAsync(mInternalPlayer, source, this);
            if (loader.Opened)
            {
                VideoSize = loader.VideoSize;
                mInternalPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(0);
                PlayerInitialized?.Invoke(this, loader.TotalRange, VideoSize);
            }
            else
            {
                Error.CopyFrom(loader.Error);
            }
            MovieLoading = false;
        }

        #endregion

    }
}
