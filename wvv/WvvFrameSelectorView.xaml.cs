using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using wvv.utils;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    /**
     * 動画から、フレームを選択して、フレームサムネイル画像を取得するためのビュー
     * 構成コンポーネント
     * - WvvVideoPlayer
     * - WvvTrimmingSlider（単なるスライダーとして利用）
     * - WvvFrameListView
     */
    public sealed partial class WvvFrameSelectorView : UserControl, INotifyPropertyChanged
    {
        #region Privates

        private WvvFrameExtractor2 mExtractor;
        private MediaClip mClip;

        #endregion

        #region Initialization / Tremination

        public WvvFrameSelectorView()
        {
            mExtractor = new WvvFrameExtractor2(40, 30);
            this.DataContext = this;
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mPlayer.Error.PropertyChanged += Error_PropertyChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            mPlayer.Error.PropertyChanged -= Error_PropertyChanged;
            mExtractor.Cancel();
            Reset();
        }

        #endregion

        #region Event Handlers

        /**
         * スライダーのThumbが操作された
         */
        private void OnCurrentPositionChanged(WvvTrimmingSlider sender, double position, bool finalize)
        {
            mPlayer.SeekPosition = position;
            mFrameListView.TickPosition = sender.AbsoluteCurrentPosition / sender.TotalRange;
        }

        /**
         * スライダー上がタップされた
         */
        private void OnSliderTapped(WvvTrimmingSlider sender, double position, bool finalize)
        {
            sender.AbsoluteCurrentPosition = position;
            mPlayer.SeekPosition = position;
            mFrameListView.TickPosition = position / sender.TotalRange;
        }

        /**
         * VideoPlayerの状態が変化した
         */
        private void OnPlayerStateChanged(IWvvVideoPlayer player, PlayerState state)
        {
            Ready = state != PlayerState.NONE;
        }

        private void OnFrameListTapped(object sender, TappedRoutedEventArgs e)
        {
            double v = mFrameListView.GetTappedPosition(e);
            double pos = v * mTrimmingSlider.TotalRange;
            mTrimmingSlider.CurrentPosition = pos;
            mPlayer.SeekPosition = pos;
            mFrameListView.TickPosition = v;
        }

        /**
         * WvvVideoPlayer のエラー情報（WvvFrameSelectorViewと共有）が変化した場合の通知
         * （こちらのReady状態が変化する）
         */
        private void Error_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            notify("Ready");
        }

        #endregion

        #region Public APIs


        public void Reset()
        {
            Ready = false;
            Error.Reset();

            mFrameListView.Reset();
            mTrimmingSlider.Reset();

            mPlayer.Reset();
            mClip = null;
        }

        /**
         * SetSource()より前に、グルグルを回し始める。
         * この後、SetSource()を呼ばないと、いつまでもグルグルしたままになる。
         */
        public void Standby()
        {
            mPlayer.MovieLoading = true;
        }

        /**
         * ターゲットの動画ファイルをセットする
         */
        public async void SetSource(StorageFile source)
        {
            Reset();

            if(null==source)
            {
                return;
            }
            mPlayer.SetSource(source);


            try
            {
                mClip = await MediaClip.CreateFromFileAsync(source);
                mTrimmingSlider.TotalRange = mClip.OriginalDuration.TotalMilliseconds;
                await mExtractor.ExtractAsync(mClip, (s, i, img) =>
                {
                    mFrameListView.Frames[i] = img;
                },
                (s, img) =>
                {
                    mFrameListView.FrameListHeight = mExtractor.ThumbnailHeight;
                    for (int i = 0; i < mExtractor.FrameCount; i++)
                    {
                        mFrameListView.Frames.Add(img);
                    }
                });
            }
            catch ( Exception e)
            {
                CmLog.error("WvvFrameSelectorView.SetSource", e);
                Error.SetError(e);
            }
        }

        /**
         * サムネイルを取得する（した）シーク位置
         */
        public double ThumbmailPosition { get { return mTrimmingSlider.CurrentPosition; } }

        /**
         * 選択されたフレームを画像として取得
         */
        public async Task<BitmapImage> GetResultImage(int height)
        {
            try
            {
                var extractor = new WvvFrameExtractor2(height, 1);
                return await extractor.ExtractSingleFrameAsync(mClip, TimeSpan.FromMilliseconds(mTrimmingSlider.CurrentPosition));
            }
            catch (Exception e)
            {
                Error.SetError(e);
                CmLog.error("WvvFrameSelectorView.GetResultImage", e);
                return null;
            }
        }

        public async Task<ImageStream> GetResultImageStream(int height)
        {
            try
            {
                var extractor = new WvvFrameExtractor2(height, 1);
                return await extractor.ExtractSingleFrameStreamAsync(mClip, TimeSpan.FromMilliseconds(mTrimmingSlider.CurrentPosition));
            }
            catch (Exception e)
            {
                Error.SetError(e);
                CmLog.error("WvvFrameSelectorView.GetResultImageStream", e);
                return null;
            }
        }
        #endregion

        #region Properties

        //public Exception Error
        //{
        //    get { return mError; }
        //    set
        //    {
        //        mError = value;
        //        notify("Ready");   
        //    } 
        //}
        //private Exception mError = null;

        public WvvError Error
            {
            get => mPlayer.Error;
        }


        public bool Ready
        {
            get { return mReady && !Error.HasError; }
            set
            {
                if (value != mReady)
                {
                    mReady = value;
                    notify("Ready");
                }
            }
        }
        private bool mReady = false;

        #endregion

        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        #region Slider operation by Keyboard

        /**
         * スライダー上でのキー押下イベント
         */
        private void OnSliderKeyDown(object sender, KeyRoutedEventArgs e)
        {
            CmLog.debug("KeyDown");
            bool forward = true;
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Left:
                case Windows.System.VirtualKey.Down:
                    forward = false;
                    break;
                case Windows.System.VirtualKey.Right:
                case Windows.System.VirtualKey.Up:
                    break;
                default:
                    return; // これら以外のキーは無視
            }
            var t = mTrimmingSlider.TotalRange;
            var d =  t / 100;
            if(!forward)
            {
                d *= -1;
            }
            var pos = mTrimmingSlider.CurrentPosition + d;
            pos = Math.Max(0, Math.Min(t, pos));

            // Slider/Player/FrameList すべてにセット
            mTrimmingSlider.CurrentPosition = pos;
            mPlayer.SeekPosition = pos;
            mFrameListView.TickPosition = pos/t;

            // マウスでフォーカスをセットされたときは、このタイミングでフォーカス枠が表示される。
            VisualStateManager.GoToState(mSliderPanel, "Focused", false);
            e.Handled = true;

        }

        /**
         * スライダー上のタップ
         * タップされただけではフォーカスはセットされないので、自分でフォーカスをセットしなければならない。
         */
        private void OnSliderTapped(object sender, TappedRoutedEventArgs e)
        {
            mSliderPanel.Focus(FocusState.Pointer);
        }

        /**
         * スライダーがフォーカスを得た
         * 
         * キーボード操作でフォーカスを受け取った場合は、VisualStateをFocusedにする。
         * これも、自分でやらなければならないらしい。
         */
        private void OnSliderGotFocus(object sender, RoutedEventArgs e)
        {
            //CmLog.debug("GotFocus");
            if(((Control)sender).FocusState==FocusState.Keyboard)
            {
                VisualStateManager.GoToState(mSliderPanel, "Focused", false);
            }
        }

        /**
         * スライダーがフォーカスを失った
         * 
         * VisualStateをUnfocusedにする。
         */
        private void OnSliderLostFoucus(object sender, RoutedEventArgs e)
        {
            //CmLog.debug("LostFocus");
            VisualStateManager.GoToState(mSliderPanel, "Unfocused", false);
        }

        #endregion
    }
}
