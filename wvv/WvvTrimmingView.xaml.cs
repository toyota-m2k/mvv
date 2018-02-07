using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Playback;
using Windows.Media.Transcoding;
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
    public interface IWvvSaveAs
    {
        IAsyncOperationWithProgress<TranscodeFailureReason, double> SaveToFile(StorageFile toFile);
    }

    public sealed partial class WvvTrimmingView : UserControl, INotifyPropertyChanged, IWvvSaveAs
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        public IAsyncOperationWithProgress<TranscodeFailureReason, double> SaveToFile(StorageFile toFile)
        {
            return mComposition.RenderToFileAsync(toFile);
        }


        private StorageFile mSource;

        /**
         * フレームリスト
         */
        public ObservableCollection<ImageSource> Frames
        {
            get;
        } = new ObservableCollection<ImageSource>();

        /**
         */
        public bool Ready
        {
            get { return mReady; }
            private set
            {
                if(value!=mReady)
                {
                    mReady = value;
                    notify("Ready");
                }
            }
        }
        private bool mReady = false;

        public bool IsPlaying
        {
            get { return mIsPlaying; }
            private set
            {
                if (value != mIsPlaying)
                {
                    mIsPlaying = value;
                    notify("IsPlaying");
                }
            }
        }
        private bool mIsPlaying = false;

        //public double TotalRange
        //{
        //    get { return mTotalRange; }
        //    private set
        //    {
        //        if(mTotalRange!=value)
        //        {
        //            mTotalRange = value;
        //            notify("TotalRange");
        //        }
        //    }
        //}
        //private double mTotalRange = 100;

        public double TotalRange
        {
            get { return mTrimmingSlider.TotalRange; }
            set { mTrimmingSlider.TotalRange = value; }
        }

        private Size VideoSize
        {
            get { return mVideoSize; }
            set
            {
                if(mVideoSize != value)
                {
                    mVideoSize = value;
                    adjustPlayerSize(mVideoSize.Width, mVideoSize.Height);
                }
            }
        }
        private Size mVideoSize;


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

        private MediaPlayer mPlayer;
        private MediaComposition mComposition;

        public WvvTrimmingView()
        {
            this.DataContext = this;
            this.InitializeComponent();
            mSource = null;
            mComposition = new MediaComposition();
        }

        private MediaSource mOriginalSource;

        private async void LoadMediaSource(StorageFile source)
        {
            Ready = false;
            mComposition.Clips.Clear();
            if (null != source)
            {
                mOriginalSource = MediaSource.CreateFromStorageFile(source);
                var clip = await MediaClip.CreateFromFileAsync(source);
                mComposition.Clips.Add(clip);

                var loader = new WvvMediaLoader(mPlayer);
                loader.Load(mOriginalSource, this, (sender, mediaPlayer) =>
                {
                    TotalRange = sender.TotalRange;
                    VideoSize = sender.VideoSize;

                    var extractor = new WvvFrameExtractor(40, 20);
                    extractor.Extract(mediaPlayer, this, (sender2, index, image) =>
                    {
                        if (null != image)
                        {
                            Debug.WriteLine("Frame Extracted : {0}", index);
                            Frames.Add(image);
                        }
                        else
                        {
                            Debug.WriteLine("Frame Extracted : Finalized.");
                            Ready = true;
                        }
                    });
                });
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mPlayer = new MediaPlayer();
            mPlayer.PlaybackSession.PlaybackStateChanged += PBS_StateChanged;
            mPlayerElement.SetMediaPlayer(mPlayer);

            if(null!=mSource)
            {
                var s = mSource;
                mSource = null;
                LoadMediaSource(s);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            mPlayerElement.SetMediaPlayer(null);
            mPlayer.PlaybackSession.PlaybackStateChanged -= PBS_StateChanged;
            mPlayer.Dispose();
            mPlayer = null;
        }

        private async void PBS_StateChanged(MediaPlaybackSession session, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (session.PlaybackState)
                {
                    case MediaPlaybackState.None:
                    case MediaPlaybackState.Buffering:
                    case MediaPlaybackState.Opening:
                    case MediaPlaybackState.Paused:
                    default:
                        IsPlaying = false;
                        break;
                    case MediaPlaybackState.Playing:
                        IsPlaying = true;
                        break;
                }
            });
        }

        public void SetSource(StorageFile source)
        {
            if (null != mPlayer)
            {
                LoadMediaSource(source);
            }
            else
            {
                mSource = source;
            }
        }

        private void preview(double pos, bool force)
        {
            if(mBusy && !force)
            {
                return;
            }
            mBusy = true;
            MediaStreamSource mediaStreamSource = mComposition.GeneratePreviewMediaStreamSource(
                    (int)mPlayerElement.ActualWidth,
                    (int)mPlayerElement.ActualHeight);
            var loader = new WvvMediaLoader(mPlayer);
            loader.Load(MediaSource.CreateFromMediaStreamSource(mediaStreamSource), this, (sender, player) =>
            {
                if (pos < 0)
                {
                    pos = 0;
                }
                else if (pos > sender.TotalRange)
                {
                    pos = sender.TotalRange;
                }
                mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(pos);
                mBusy = false;
            });
        }

        private bool mBusy = false;
        bool mPreviewing = false;
        private void startPreview(bool play)
        {
            if(mPreviewing)
            {
                if(!IsPlaying)
                {
                    mPlayer.Play();
                }
                return;
            }
            mPreviewing = true;
            MediaStreamSource mediaStreamSource = mComposition.GeneratePreviewMediaStreamSource(
                    (int)mPlayerElement.ActualWidth,
                    (int)mPlayerElement.ActualHeight);
            var loader = new WvvMediaLoader(mPlayer);
            loader.Load(MediaSource.CreateFromMediaStreamSource(mediaStreamSource), this, (sender, player) =>
            {
                if (mPreviewing)
                {
                    mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mTrimmingSlider.CurrentPosition);
                    if (play)
                    {
                        mPlayer.Play();
                    }
                }
            });
        }

        enum PositionOf{ START, END, CURRENT };

        private double seekPosition(PositionOf seekTo)
        {
            double pos;
            switch (seekTo)
            {
                case PositionOf.START:
                    pos = mTrimmingSlider.TrimStart;
                    break;
                case PositionOf.END:
                    pos = mTrimmingSlider.TotalRange - mTrimmingSlider.TrimEnd;
                    break;
                case PositionOf.CURRENT:
                default:
                    pos = mTrimmingSlider.AbsoluteCurrentPosition;
                    break;
            }
            return pos;
        }

        private void stopPreview(PositionOf seekTo)
        {
            if(IsPlaying)
            {
                mPlayer.Pause();
            }
            if(!mPreviewing)
            {
                mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(seekPosition(seekTo));
                return;
            }
            mPreviewing = false;
            var loader = new WvvMediaLoader(mPlayer);
            loader.Load(mOriginalSource, this, (sender, player) =>
            {
                if(!mPreviewing)
                {
                    mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(seekPosition(seekTo));
                }
            });

        }

        private void OnTrimStartChanged(WvvTrimmingSlider sender, double position, bool finalize)
        {
            if(mComposition.Clips.Count!=1)
            {
                return;
            }
            var currentClip = mComposition.Clips[0];
            currentClip.TrimTimeFromStart = TimeSpan.FromMilliseconds(position);
            stopPreview(PositionOf.START);
        }

        private void OnTrimEndChanged(WvvTrimmingSlider sender, double position, bool finalize)
        {
            if (mComposition.Clips.Count != 1)
            {
                return;
            }
            var currentClip = mComposition.Clips[0];
            currentClip.TrimTimeFromEnd = TimeSpan.FromMilliseconds(position);
            stopPreview(PositionOf.END);
        }

        private void OnCurrentPositionChanged(WvvTrimmingSlider sender, double position, bool finalize)
        {
            mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(TotalRange - position);
            
            stopPreview(PositionOf.CURRENT);
        }

        private void OnPlay(object sender, TappedRoutedEventArgs e)
        {
            if(IsPlaying)
            {
                mPlayer.Pause();
            }
            else
            {
                startPreview(true);
            }
        }
    }
}
