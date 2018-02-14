using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
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
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            mExtractor.Cancel();
            mClip = null;
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

        #endregion

        #region Public APIs

        /**
         * ターゲットの動画ファイルをセットする
         */
        public async void SetSource(StorageFile source)
        {
            Ready = false;
            Error = null;

            mPlayer.SetSource(source);

            mFrameListView.Reset();
            mTrimmingSlider.Reset();

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
                Debug.WriteLine(e);
                Error = e;
            }
        }

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
                Debug.WriteLine(e);
                return null;
            }
        }
        #endregion

        #region Properties

        public Exception Error
        {
            get { return mError; }
            set
            {
                mError = value;
                notify("Ready");   
            } 
        }
        private Exception mError = null;


        public bool Ready
        {
            get { return mReady && null==Error; }
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
    }
}
