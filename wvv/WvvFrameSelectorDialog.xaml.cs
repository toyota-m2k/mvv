using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    /**
     * フレーム選択ダイアログ
     */
    public sealed partial class WvvFrameSelectorDialog : UserControl, WvvDialog.IWvvDialogContent, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        #region Delegates

        /**
         * サムネイルが取得できたときのコールバック型
         * @param dlg   ダイアログインスタンス
         * @param stream 画像データの入ったストリーム・・・受け取った側でDispose()すること。
         */
        public delegate void WvvFrameSelectedHandler(WvvFrameSelectorDialog dlg, double position, IRandomAccessStream stream);

        #endregion

        #region Initialize / Terminate

        /**
         * ダイアログを表示する。
         */
        public static async Task<bool> Show(MediaSource source, FrameworkElement anchor, WvvFrameSelectedHandler onSelected)
        {
            if (null==onSelected)
            {
                return false;
            }

            var content = new WvvFrameSelectorDialog(source, onSelected);
            await WvvDialog.Show(content, anchor);
            return true;
        }

        /**
         * コンストラクタ
         */
        private WvvFrameSelectorDialog(MediaSource source, WvvFrameSelectedHandler onSelected)
        {
            mOnSelected = onSelected;
            mSource = source;
            this.InitializeComponent();
            this.DataContext = this;
        }

        /**
         * コンポーネントの初期化
         */
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mPlayerElement.SetMediaPlayer(new MediaPlayer());
            mPlayerElement.MediaPlayer.MediaOpened += MP_MediaOpened;
            mPlayerElement.MediaPlayer.PlaybackSession.PlaybackStateChanged += MPB_StateChanged;
            mPlayerElement.MediaPlayer.Source = mSource;

        }

        /**
         * 解放処理
         */
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            mPlayerElement.MediaPlayer.MediaOpened -= MP_MediaOpened;
            mPlayerElement.MediaPlayer.PlaybackSession.PlaybackStateChanged -= MPB_StateChanged;
            mPlayerElement.MediaPlayer.Dispose();
        }

        #endregion

        #region Media Player Event Handlers

        /**
         * 動画ファイルが開かれた
         */
        private async void MP_MediaOpened(MediaPlayer sender, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                TotalRange = mPlayerElement.MediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
                adjustPlayerSize(mPlayerElement.MediaPlayer.PlaybackSession.NaturalVideoWidth, mPlayerElement.MediaPlayer.PlaybackSession.NaturalVideoHeight);

                Ready = true;
            });
        }

        /**
         * MediaPlayerの状態監視
         */
        private async void MPB_StateChanged(MediaPlaybackSession session, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (session.PlaybackState)
                {
                    case MediaPlaybackState.Paused:
                        FrameAvailable = true;
                        mCreateThumbnailAction?.Invoke();
                        break;
                    case MediaPlaybackState.Playing:
                    case MediaPlaybackState.None:
                    case MediaPlaybackState.Buffering:
                    case MediaPlaybackState.Opening:
                    default:
                        FrameAvailable = false;
                        break;
                }
            });
        }

        #endregion

        #region Private Fields

        // 動画ソース
        private MediaSource mSource;

        // 選択完了時のコールバック
        private WvvFrameSelectedHandler mOnSelected;
        
        // ダイアログクラスの参照を保持するためのフィールド
        WeakReference<WvvDialog> mDialog = new WeakReference<WvvDialog>(null);
        
        // ダイアログを閉じるときのフラグ
        bool mClosing = false;
        
        // FrameAvailable==false時のサムネイル作成リトライ用アクション
        Action mCreateThumbnailAction = null;
        
        // サムネイルを２回作成しないようにするためのフラグ
        private bool mDoneOnce = false;

        #endregion

        #region Binding Properties

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
            }
        }
        private double mTotalRange = 100;

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

        /**
         * 動画ファイルがオープンされたらTrueになる
         */
        public bool Ready
        {
            get { return mReady; }
            set
            {
                if (mReady != value)
                {
                    mReady = value;
                    notify("Ready");
                    notify("FrameAvailable");
                }
            }
        }
        private bool mReady = false;

        /**
         * 動画のサムネイルが取得可能になったらTrueになる
         */
        public bool FrameAvailable
        {
            get { return mReady && mAvailable; }
            set
            {
                if (value != mAvailable)
                {
                    mAvailable = value;
                    notify("FrameAvailable");
                }
            }
        }
        private bool mAvailable = false;

        /**
         * プレイヤーのサイズ（xamlレンダリング用）
         */
        public Size PlayerSize
        {
            get; private set;
        }

        /**
         * 動画の実サイズに合わせてプレイヤーのサイズを調整する
         */
        private void adjustPlayerSize(double mw, double mh)
        {
            PlayerSize = calcFittingSize(mw, mh, mPlayerContainer.ActualWidth, mPlayerContainer.ActualHeight);
            notify("PlayerSize");
        }

        /**
         * 指定サイズ(cw,ch)内に収まる動画サイズを計算
         */
        private Size calcFittingSize(double mw, double mh, double cw, double ch)
        {
            Size size = new Size();
            if (mw < mh)
            {
                size.Height = ch;
                size.Width = mw * ch / mh;
            }
            else
            {
                size.Width = cw;
                size.Height = mh * cw / mw;
            }
            return size;
        }

        #endregion

        #region Event Handlers

        /**
         * カレント位置のサムネイルを作成する。
         */
        private async void createThumbnailCore()
        {
            if(mDoneOnce)
            {
                return;
            }
            mDoneOnce = true;

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();

                // Debug.WriteLine(mPlayerElement.MediaPlayer.PlaybackSession.Position);

                double mw = mPlayerElement.MediaPlayer.PlaybackSession.NaturalVideoWidth, mh = mPlayerElement.MediaPlayer.PlaybackSession.NaturalVideoHeight;
                var limit = Math.Min(Math.Max(mh, mw), 1024);
                var playerSize = calcFittingSize(mw, mh, limit, limit);
                var canvasImageSource = new CanvasImageSource(canvasDevice, (int)playerSize.Width, (int)playerSize.Height, DisplayInformation.GetForCurrentView().LogicalDpi);//96); 

                using (var frameServerDest = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)playerSize.Width, (int)playerSize.Height, BitmapAlphaMode.Ignore))
                using (CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, frameServerDest))
                {
                    mPlayerElement.MediaPlayer.CopyFrameToVideoSurface(canvasBitmap);
                    // ↑これで、frameServerDest に描画されるのかと思ったが、このframeServerDestは、単に、空のCanvasBitmapを作るための金型であって、
                    // 実際の描画は、CanvasBitmap（IDirect3DSurface)に対してのみ行われ、frameServerDestはからBitmapを作っても、黒い画像しか取り出せない。
                    // このため、有効な画像を取り出すには、CangasBitmapから softwareBitmapを生成してエンコードする必要がある。

                    using (var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(canvasBitmap))
                    {
                        var stream = new InMemoryRandomAccessStream();
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                        encoder.SetSoftwareBitmap(softwareBitmap);

                        try
                        {
                            await encoder.FlushAsync();
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                            stream.Dispose();
                            stream = null;
                        }

                        if (null != mOnSelected)
                        {
                            mOnSelected(this, mPlayerElement.MediaPlayer.PlaybackSession.Position.TotalMilliseconds, stream);
                            closeDialog();
                        }
                        else
                        {
                            stream.Dispose();
                        }
                    }
                }
            });
        }

        /**
         * ダイアログを閉じる
         */
        private void closeDialog()
        {
            mClosing = true;
            WvvDialog dlg;
            if (mDialog.TryGetTarget(out dlg))
            {
                dlg.Close();
            }
        }

        /**
         * カレントポジションのサムネイルを作成する。
         */
        private void createCurrentThumbnail()
        {
            mCreateThumbnailAction = null;
            if (FrameAvailable)
            {
                createThumbnailCore();
            }
            else
            {
                mCreateThumbnailAction = createCurrentThumbnail;
            }
        }


        /**
         * 確定して閉じる
         */
        private void OnCloseTapped(object sender, TappedRoutedEventArgs e)
        {
            createCurrentThumbnail();
        }

        /**
         * キャンセルして閉じる
         */
        private void OnCancelTapped(object sender, TappedRoutedEventArgs e)
        {
            closeDialog();
        }

        public void Opening(WvvDialog dlg)
        {
        }

        /**
         * ダイアログが開いたところで dlg オブジェクトをメンバーに覚えておく
         */
        public void Opened(WvvDialog dlg)
        {
            mDialog.SetTarget(dlg);
        }

        /**
         * 明示的に閉じる操作が行われたときだけ閉じておｋ（trueを返す）
         */
        public bool Closing(WvvDialog dlg)
        {
            return mClosing;
        }

        /**
         * ダイアログが閉じられたら、保持しているWvvDialogをクリア
         */
        public void Closed(WvvDialog dlg)
        {
            mDialog.SetTarget(null);
        }

        private void OnSliderDragStarted(object sender, DragStartedEventArgs e)
        {

        }

        private void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
        {

        }

        private void OnSliderPointerPressed(object sender, PointerRoutedEventArgs e)
        {

        }

        private void OnSliderPointerReleased(object sender, PointerRoutedEventArgs e)
        {

        }

        private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (Ready)
            {
                mPlayerElement.MediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mSlider.Value);
            }
        }

        private void OnSliderWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var elem = sender as UIElement;
            if (null != elem)
            {
                int delta = e.GetCurrentPoint(elem).Properties.MouseWheelDelta;
                if (0 != delta)
                {
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
        #endregion

    }
}
