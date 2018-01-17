﻿using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    public sealed partial class WvvMoviePlayer : UserControl, INotifyPropertyChanged
    {
        #region Constants
        /**
         * 動画再生ウィンドウのサイズ計算方法
         */
        public enum LayoutMode {
            Width,       // 指定された幅になるように高さを調整
            Height,      // 指定された高さになるよう幅を調整
            Inside       // 指定された矩形に収まるよう幅、または、高さを調整
        }
        #endregion

        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        #region Binding / DataContext

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
                    notify("PlayerSize");
                }
            }
        }
        private LayoutMode mPlayerLayout = LayoutMode.Height;

        public Size LayoutSize
        {
            get { return mLayoutSize; }
            set
            {
                if (value != mLayoutSize)
                {
                    mLayoutSize = value;
                    notify("PlayerSize");
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
                return fitSize(mPlayerLayout);
            }
        }

        private Size fitSize(LayoutMode mode)
        {
            switch (mode)
            {
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
         * SetSource()直後はfalseで、その後、フレームの列挙が終了し、再生可能な状態になれば trueにセットされる。
         */
        public bool MoviePrepared
        {
            get { return mMoviePrepared; }
            private set
            {
                if (mMoviePrepared != value)
                {
                    mMoviePrepared = value;
                    notify("MoviePrepared");
                }
            }
        }
        private bool mMoviePrepared = false;

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
                    notify("PlayerSize");
                }
            }
        }
        private Size mVideoSize = new Size(0, 0);

        /**
         * カスタム描画モード
         *  true: 自前で描画
         *  false: MediaPlayerに任せる
         */
        public bool CustomDrawing
        {
            get { return mCustomDrawing; }
            set
            {
                if (value != mCustomDrawing)
                {
                    mCustomDrawing = value;
                    if (null != mMoviePlayer && null != mMoviePlayer.MediaPlayer)
                    {
                        mMoviePlayer.MediaPlayer.IsVideoFrameServerEnabled = value;
                    }
                    if (!value && IsPlaying)
                    {
                        TruckingTimer.Start();
                    }
                    else if(null!= mTruckingTimer)
                    {
                        mTruckingTimer.Stop();
                    }
                    notify("CustomDrawing");
                }
            }
        }
        private bool mCustomDrawing = true;


        /**
         * 再生中:true / 停止中(or初期化中）:false
         */
        public bool IsPlaying
        {
            get { return mPlaying; }
            private set
            {
                if (mPlaying != value)
                {
                    mPlaying = value;
                    notify("IsPlaying");
                    if(!CustomDrawing)
                    {
                        if(value)
                        {
                            TruckingTimer.Start();
                        }
                        else 
                        {
                            TruckingTimer.Stop();
                        }
                    }
                }
            }
        }
        private bool mPlaying = false;
        private DispatcherTimer mTruckingTimer = null;
        private DispatcherTimer TruckingTimer
        {
            get
            {
                if(null==mTruckingTimer)
                {
                    mTruckingTimer = new DispatcherTimer();
                    mTruckingTimer.Interval = TimeSpan.FromMilliseconds(10);
                    mTruckingTimer.Tick += (sender, e) =>
                    {
                        updateSliderPosition(PlaybackSession.Position.TotalMilliseconds);
                    };
                }
                return mTruckingTimer;
            }
        }

        /**
         * フレーム一覧の表示状態
         */
        public bool ShowingFrames
        {
            get { return mShowingFrames; }
            set
            {
                if (mShowingFrames != value)
                {
                    mShowingFrames = value;
                    notify("ShowingFrames");
                    notify("ThumbMode");
                }
            }
        }
        public bool mShowingFrames = true;

        /**
         * フレームサムネイルの大小
         */
        public bool LargeThumbnail
        {
            get { return mLargeThumbnail; }
            set
            {
                if (mLargeThumbnail != value)
                {
                    mLargeThumbnail = value;
                    notify("LargeThumbnail");
                    notify("ThumbMode");
                    remakeThumbnails();
                }
            }
        }
        private bool mLargeThumbnail = false;

        /**
         * Slider Thumbのモード
         * ShowingFramesとLargeThumbnailを組み合わせてバインドできればよいのだが、
         * DataTriggerBehaviorで、これを実現する方法がわからないので、１つのプロパティにしておく。
         */
        public string ThumbMode
        {
            get
            {
                if (!ShowingFrames)
                {
                    return "MIN";
                }
                else
                {
                    if (LargeThumbnail)
                    {
                        return "MAX";
                    }
                    else
                    {
                        return "NOR";
                    }
                }
            }
        }

        /**
         * フレームリスト
         */
        public ObservableCollection<ImageSource> Frames
        {
            get;
        } = new ObservableCollection<ImageSource>();


        /**
         * 動画の総再生時間
         */
        public double TotalRange
        {
            get { return mTotalRange; }
            private set
            {
                if (mTotalRange != value)
                {
                    mTotalRange = value;
                    notify("TotalRange");
                    notify("SmallChange");
                    notify("LargeChange");
                }
                mMarkerView.TotalRange = value;
            }
        }
        private double mTotalRange = 100;

        private double ThumbnailHeight
        {
            get
            {
                return (mLargeThumbnail) ? 63 : 44;
            }
        }

        /**
         * 矢印キーによるスライダーの移動量
         */
        public double SmallChange
        {
            get { return mTotalRange / 100; }
        }
        /**
         * スライダーの移動量（大きいやつ・・・操作方法は知らない）
         */
        public double LargeChange
        {
            get { return mTotalRange / 20; }
        }

        public IMediaPlaybackSource Source
        {
            get
            {
                return MediaPlayer?.Source ?? mTempSource;
            }
            set
            {
                if(null==MediaPlayer)
                {
                    mTempSource = value;
                }
                else
                {
                    SetSource(value);
                }
            }
        }
        IMediaPlaybackSource mTempSource = null;

        #endregion

        #region Internal Accessor

        private MediaPlayer MediaPlayer
        {
            get { return mMoviePlayer?.MediaPlayer; }
        }
        private MediaPlaybackSession PlaybackSession
        {
            get { return mMoviePlayer?.MediaPlayer?.PlaybackSession; }
        }
        private WvvMoviePlayer CTX
        {
            get { return this; }
        }

        #endregion

        #region Fields

        long mFullWindowListenerToken = 0;

        SoftwareBitmap mFrameServerDest = null;
        CanvasImageSource mCanvasImageSource = null;

        bool mGettingFrame = false;
        int mFrameCount = 20;
        double mReqPosition = 0;
        double mSpan = 0;
        double mOffset = 0;
        int mFrame = 0;
        Size mThumbnailSize = new Size(44, 44);
        bool mCustomDrawingBackup = false;
        bool mPauseTemporary = false;

        #endregion

        #region Initialization / Termination

        public WvvMoviePlayer()
        {
            this.InitializeComponent();
            this.DataContext = this;
        }

        /**
         * 初期化
         */
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mMoviePlayer.SetMediaPlayer(new MediaPlayer());
            MediaPlayer.IsVideoFrameServerEnabled = CTX.CustomDrawing;

            MediaPlayer.MediaOpened += MP_MediaOpened;
            MediaPlayer.VideoFrameAvailable += MP_FrameAvailable;
            //PlaybackSession.PositionChanged += PBS_PositionChanged;
            PlaybackSession.SeekCompleted += PBS_SeekCompletedForExtractFrames;
            PlaybackSession.PlaybackStateChanged += PBS_PlaybackStateChanged;
            mFullWindowListenerToken = mMoviePlayer.RegisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, MPE_FullWindowChanged);

            mMarkerView.MarkerSelected += MV_MarkerSelected;
            mMarkerView.MarkerAdded += MV_MarkerAdded;
            mMarkerView.MarkerRemoved += MV_MarkerRemoved;
            if (null!=mTempSource)
            {
                SetSource(mTempSource);
            }
        }

        /**
         * 後始末
         */
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Pause();

            MediaPlayer.MediaOpened -= MP_MediaOpened;
            MediaPlayer.VideoFrameAvailable -= MP_FrameAvailable;
            //PlaybackSession.PositionChanged -= PBS_PositionChanged;
            PlaybackSession.SeekCompleted -= PBS_SeekCompletedForExtractFrames;
            PlaybackSession.PlaybackStateChanged -= PBS_PlaybackStateChanged;
            mMoviePlayer.UnregisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, mFullWindowListenerToken);

            mMarkerView.MarkerSelected -= MV_MarkerSelected;
            mMarkerView.MarkerAdded -= MV_MarkerAdded;
            mMarkerView.MarkerRemoved -= MV_MarkerRemoved;

            MediaPlayer.Dispose();
            if (null != mFrameServerDest)
            {
                mFrameServerDest.Dispose();
                mFrameServerDest = null;
                mCanvasImageSource = null;
            }
        }

        #endregion

        #region MediaPlayer Event Listener

        /**
         * CustomDrawingモード時のフレーム描画処理
         */
        private async void MP_FrameAvailable(MediaPlayer mediaPlayer, object args)
        {
            if (mGettingFrame)
            {
                return;
            }

            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (mFrameServerDest == null)
                {
                    // FrameServerImage in this example is a XAML image control
                    var playerSize = CTX.PlayerSize;
                    mFrameServerDest = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)playerSize.Width, (int)playerSize.Height, BitmapAlphaMode.Ignore);
                    mCanvasImageSource = new CanvasImageSource(canvasDevice, (int)playerSize.Width, (int)playerSize.Height, DisplayInformation.GetForCurrentView().LogicalDpi);//96); 
                    mFrameImage.Source = mCanvasImageSource;
                }
                Debug.WriteLine("Frame: {0}", mediaPlayer.PlaybackSession.Position);
                updateSliderPosition(mediaPlayer.PlaybackSession.Position.TotalMilliseconds);

                using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, mFrameServerDest))
                using (CanvasDrawingSession ds = mCanvasImageSource.CreateDrawingSession(Windows.UI.Colors.Black))
                {

                    MediaPlayer.CopyFrameToVideoSurface(inputBitmap);
                    if (null != CustomDraw)
                    {
                        CustomDraw(this, ds, inputBitmap);
                    }
                    else
                    {
                        ds.DrawImage(inputBitmap);
                    }
                }
            });
        }

        /**
         * 動画ファイルがオープンされたときの処理
         * - サイズ取得
         * - フレームサムネイルの取得準備
         */
        private async void MP_MediaOpened(MediaPlayer mediaPlayer, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                beginExtractFrames();
            });
        }
        #endregion

        #region PlaybackSession Event Listeners

        /**
         * シークが完了したときの処理
         * - フレームサムネイルを作成
         */
        private async void PBS_SeekCompletedForExtractFrames(MediaPlaybackSession session, object args)
        {
            if (mSpan == 0 || !mGettingFrame)
            {
                return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var mediaPlayer = session.MediaPlayer;
                extractFrame(mediaPlayer);

                if (mFrame < mFrameCount)
                {
                    mFrame++;
                    session.Position = TimeSpan.FromMilliseconds(mOffset + mSpan * mFrame);
                }
                else
                {
                    // OK, Movie is ready now!
                    mGettingFrame = false;
                    PlaybackSession.Position = TimeSpan.FromMilliseconds(0);
                    MediaPlayer.IsVideoFrameServerEnabled = CTX.CustomDrawing;
                    mSlider.Value = 0;
                    CTX.TotalRange = session.NaturalDuration.TotalMilliseconds;
                    CTX.MoviePrepared = true;
                }
            });
        }
        /**
         * 再生状態が変化した
         * - CTX.IsPlaying の更新
         */
        private async void PBS_PlaybackStateChanged(MediaPlaybackSession session, object args)
        {
            if (mGettingFrame)
            {
                return;
            }
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (session.PlaybackState)
                {
                    case MediaPlaybackState.None:
                    case MediaPlaybackState.Buffering:
                    case MediaPlaybackState.Opening:
                        CTX.MoviePrepared = false;
                        CTX.IsPlaying = false;
                        break;
                    case MediaPlaybackState.Paused:
                    default:
                        CTX.IsPlaying = false;
                        break;
                    case MediaPlaybackState.Playing:
                        CTX.IsPlaying = true;
                        break;
                }
            });
        }

        /**
         * 再生位置が変化した
         * - トラッカーの位置調整(MediaPlayerで描画するモードのときのみ)
         * 
         * この方法だと、イベントが少なすぎて、トラッカーが飛び飛びに動いてぎこちない。
         * そこで、この方法はやめて、タイマー(DispatcherTimer)を使って、自前でトラッカーを動かすように変更。
         */
        //private async void PBS_PositionChanged(MediaPlaybackSession session, object args)
        //{
        //    if (mGettingFrame || CTX.CustomDrawing)
        //    {
        //        return;
        //    }
        //    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
        //    {
        //        updateSliderPosition(session.Position.TotalMilliseconds);
        //    });
        //}

        #endregion

        #region MediaPlayerElement Property Observer

        /**
         * フルスクリーンモードの変更通知
         * - 通常モードに戻ったときに、フルスクリーンモード用の一時設定を元に戻す
         */
        private void MPE_FullWindowChanged(DependencyObject sender, DependencyProperty dp)
        {
            var mpe = sender as MediaPlayerElement;
            if (null != mpe)
            {
                if (!mpe.IsFullWindow)
                {
                    CTX.CustomDrawing = mCustomDrawingBackup;
                    mMoviePlayer.MediaPlayer.IsVideoFrameServerEnabled = CTX.CustomDrawing;
                    mMoviePlayer.AreTransportControlsEnabled = false;
                }
            }
        }
        #endregion

        #region UI Event Handlers

        /**
         * 再生/停止ボタン
         */
        private void OnButtoPlayStop(object sender, RoutedEventArgs e)
        {
            //CTX.IsPlaying = !CTX.IsPlaying;
            if (CTX.MoviePrepared)
            {
                if (CTX.IsPlaying)
                {
                    MediaPlayer.Pause();
                }
                else
                {
                    MediaPlayer.Play();
                }
            }
        }

        /**
         * フレームリスト表示/非表示切替ボタン
         */
        private void OnShowHideFrameList(object sender, RoutedEventArgs e)
        {
            // CTX.ShowingFrames = !CTX.ShowingFrames;
            if(!CTX.ShowingFrames)
            {
                CTX.ShowingFrames = true;
            }
            else
            {
                if(CTX.LargeThumbnail)
                {
                    CTX.LargeThumbnail = false;
                    CTX.ShowingFrames = false;
                }
                else
                {
                    CTX.LargeThumbnail = true;
                }
            }
        }

        /**
         * フルスクリーンモード切替ボタン
         */
        private void OnFullScreen(object sender, RoutedEventArgs e)
        {
            mMoviePlayer.MediaPlayer.IsVideoFrameServerEnabled = false;
            mMoviePlayer.AreTransportControlsEnabled = true;
            mCustomDrawingBackup = CTX.CustomDrawing;
            CTX.CustomDrawing = false;
            mMoviePlayer.IsFullWindow = true;
        }

        /**
         * マーカー追加ボタンクリック
         */
        private void OnAddMarker(object sender, RoutedEventArgs e)
        {
            mMarkerView.AddMarker(PlaybackSession.Position.TotalMilliseconds, mMarkerView);
        }

        /**
         * 次のマーカー位置へシーク
         */
        private void OnNextMarker(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            mMarkerView.NextMark(mSlider.Value, mMarkerView);
        }

        /**
         * 前のマーカー位置へシーク
         */
        private void OnPrevMarker(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            mMarkerView.PrevMark(mSlider.Value, mMarkerView);
        }

        /**
         * スライダーのトラッカー操作
         */
        private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var slider = sender as Slider;
            scrollFrameThumbnails(slider.Value);
            if (null != slider && mReqPosition != slider.Value)
            {
                PlaybackSession.Position = TimeSpan.FromMilliseconds(slider.Value);
            }
        }

        /**
         * ホイールによるスライダーの操作にも対応しておこう。
         */
        private void OnSliderWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var elem = sender as UIElement;
            if (null != elem)
            {
                int delta = e.GetCurrentPoint(elem).Properties.MouseWheelDelta;
                if (0 != delta)
                {
                    //Debug.WriteLine("Pointer: WheelChanged", delta);
                    delta = (delta > 0) ? 1 : -1;
                    double v = mSlider.Value + delta * mSlider.SmallChange;
                    if (v < mSlider.Minimum)
                    {
                        v = mSlider.Minimum;
                    }
                    else if (v > mSlider.Maximum)
                    {
                        v = mSlider.Maximum;
                    }
                    mSlider.Value = v;
                }
            }
        }

        /**
         * スライダーのThumbのドラッグが開始された
         */
        private void OnSliderDragStarted(object sender, DragStartedEventArgs e)
        {
            //Debug.WriteLine("Slider: DragStarted");
            pauseOnStartTracking();
        }

        /**
         * スライダーのThumbのドラッグが終わった
         */
        private void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
        {
            //Debug.WriteLine("Slider: DragCompleted");
            restartOnEndTracking();
        }

        /**
         * スライダーのThumb以外の部分がクリックされた
         */
        private void OnSliderPointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //Debug.WriteLine("Slider: Pressed");
            pauseOnStartTracking();
        }

        /**
         * スライダーのThumb以外の部分で、マウス/タッチがリリースされた。
         * 実際には、Thumb上でのReleasedでも呼ばれるみたい。この場合、Pressedは呼ばれず、Releasedだけが呼ばれる・・・なんか気持ち悪い。
         */
        private void OnSliderPointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //Debug.WriteLine("Slider: Released");
            restartOnEndTracking();
        }

        /**
         * マーカー削除通知
         */
        private void MV_MarkerRemoved(double value, object clientData)
        {
            MarkerRemoved?.Invoke(this, value, clientData);
        }

        /**
         * マーカー追加通知
         */
        private void MV_MarkerAdded(double value, object clientData)
        {
            MarkerAdded?.Invoke(this, value, clientData);
        }

        /**
         * マーカー選択通知
         */
        private void MV_MarkerSelected(double value, object clientData)
        {
            mSlider.Value = value;
        }

        #endregion

        #region Privates
        /**
         * 動画ファイルのオープンに成功したのち、フレームサムネイルの抽出を開始する。
         */
        private void beginExtractFrames()
        {
            if (null != mFrameServerDest)
            {
                mFrameServerDest.Dispose();
                mFrameServerDest = null;
                mCanvasImageSource = null;
            }
            double total = MediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
            mSpan = total / (mFrameCount + 1);
            mOffset = mSpan / 2;
            mFrame = 0;
            var videoSize = new Size(MediaPlayer.PlaybackSession.NaturalVideoWidth, MediaPlayer.PlaybackSession.NaturalVideoHeight);
            CTX.VideoSize = videoSize;
            mThumbnailSize.Height = ThumbnailHeight;
            mThumbnailSize.Width = videoSize.Width * mThumbnailSize.Height / videoSize.Height;
            MediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mOffset);
        }

        /**
         * １フレーム抽出
         */
        private void extractFrame(MediaPlayer mediaPlayer)
        {
            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();
            var canvasImageSrc = new CanvasImageSource(canvasDevice, (int)mThumbnailSize.Width, (int)mThumbnailSize.Height, DisplayInformation.GetForCurrentView().LogicalDpi);//96); 
            using (SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)mThumbnailSize.Width, (int)mThumbnailSize.Height, BitmapAlphaMode.Ignore))
            using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, softwareBitmap))
            using (CanvasDrawingSession ds = canvasImageSrc.CreateDrawingSession(Windows.UI.Colors.Black))
            {
                try
                {
                    mediaPlayer.CopyFrameToVideoSurface(inputBitmap);
                    ds.DrawImage(inputBitmap);
                }
                catch (Exception e)
                {
                    // 無視する
                    Debug.WriteLine(e.ToString());
                }
                CTX.Frames.Add(canvasImageSrc);
            }
        }

        /**
         * スライダー位置の更新
         */
        private void updateSliderPosition(double pos)
        {
            mReqPosition = pos;
            mSlider.Value = pos;
        }

        /**
         * スライダー位置に合わせてサムネイルビューをスクロールする
         */
        private void scrollFrameThumbnails(double pos)
        {
            var border = VisualTreeHelper.GetChild(mFrameListView, 0) as Border;
            if (null != border)
            {
                var scrollViewer = border.Child as ScrollViewer;
                double offset = (scrollViewer.ExtentWidth - scrollViewer.ViewportWidth) * pos / CTX.TotalRange;
                // Debug.WriteLine("Scroll from {0} to {1}", scrollViewer.HorizontalOffset, offset);*
                scrollViewer.ChangeView(offset, null, null);
            }
        }

        /**
         * （ファイルは再読み込みしないで）サムネイルサイズが変わったときなどにサムネイルを再作成する。
         */
        private void remakeThumbnails()
        {
            if (!MoviePrepared)
            {
                return;
            }
            Stop();
            MediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(0);

            CTX.Frames.Clear();
            CTX.IsPlaying = false;
            mPauseTemporary = false;
            mGettingFrame = true;

            MediaPlayer.IsVideoFrameServerEnabled = true;
            beginExtractFrames();
        }

        /**
         * スライダー操作中に動画の再生を一時的に停止する
         */
        private void pauseOnStartTracking()
        {
            mPauseTemporary = IsPlaying;
            if (mPauseTemporary)
            {
                MediaPlayer.Pause();
            }
        }

        /**
         * スライダー操作が終わったときに（必要なら）動画の再生を再開する。
         */
        private void restartOnEndTracking()
        {
            if (mPauseTemporary)
            {
                mPauseTemporary = false;
                MediaPlayer.Play();
            }
        }
        #endregion

        #region Events

        /**
         * CustomDrawing モードのときの描画用イベント
         * デフォルトの処理は、
         * 
         *  ds.DrawImage(frame);
         * 
         * だが、このイベントをハンドリングすればフィルター処理などを行うことが可能。
         * 
         * 例）
         *  wvvMoviePlayer.CustomDrawing = true;
         *  wvvMoviePlayer.CustomDraw += (sender, ds, frame) => {
         *  var gaussianBlurEffect = new GaussianBlurEffect {
         *          Source = frame,
         *          BlurAmount = 5f,
         *          Optimization = EffectOptimization.Speed
         *      };
         *      ds.DrawImage(gaussianBlurEffect);
         *  }
         * 
         */
        public delegate bool CustomDrawHandler(WvvMoviePlayer sender, CanvasDrawingSession ds, ICanvasImage frame);
        public event CustomDrawHandler CustomDraw;

        /**
         * マーカーの追加/削除の通知イベント
         * 
         * マーカーの追加・削除は、WvvMoviePlayer/WvvMarkerViewのUI操作によって実行される場合と、
         * 外部から WvvMoviePlayer.AddMarker/RemoveMarker()を呼び出すことによって実行される場合がありえる。
         * これらを区別するためには、requester 引数を利用する。
         * 内部から呼び出された場合は、requesterに、WvvMoviePlayerまたはWvvMarkerViewのインスタンスがセットされる。
         */
        public delegate void MarkerEvent(WvvMoviePlayer sender, double position, object requester);
        public event MarkerEvent MarkerAdded;
        public event MarkerEvent MarkerRemoved;

        #endregion

        #region Methods

        /**
         * ソースをセットする
         */
        public void SetSource(IMediaPlaybackSource source)
        {
            CTX.Frames.Clear();
            CTX.IsPlaying = false;
            CTX.MoviePrepared = false;
            mPauseTemporary = false;
            mGettingFrame = true;
            mMarkerView.Clear();
            mMoviePlayer.MediaPlayer.IsVideoFrameServerEnabled = true;
            mMoviePlayer.MediaPlayer.Source = source;       // MediaPlayerが動画ファイルを読み込んだら MP_MediaOpened が呼ばれる。
        }

        /**
         * 再生を開始
         */
        public void Start()
        {
            if (CTX.MoviePrepared)
            {
                if (!CTX.IsPlaying)
                {
                    MediaPlayer.Play();
                }
            }
        }

        /**
         * 再生中にアプリを終了すると例外（COMException:サスペンドされたアプリから動画再生を継続しようとした）がでるので、
         * Application.OnSuspending()のタイミングでStopを呼び出すこと。
         */
        public void Stop()
        {
            MediaPlayer.Pause();
        }

        /**
         * マーカーを追加
         */
        public void AddMarker(double position, object requester)
        {
            mMarkerView.AddMarker(position, requester);
        }

        /**
         * マーカーを削除
         */
        public void RemoveMarker(double position, object requester)
        {
            mMarkerView.RemoveMarker(position, requester);
        }

        #endregion

    }
}
