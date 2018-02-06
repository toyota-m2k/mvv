using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Editing;
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
    public sealed partial class WvvTrimmingView : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        public StorageFile Source
        {
            get { return mSource; }
            set
            {
                SetSource(value);
            }
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

        public double TotalRange
        {
            get { return mTotalRange; }
            private set
            {
                if(mTotalRange!=value)
                {
                    mTotalRange = value;
                    notify("TotalRange");
                }
            }
        }
        private double mTotalRange = 100;

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
            this.InitializeComponent();
            this.DataContext = this;
            mSource = null;
            mComposition = new MediaComposition();
        }

        private async void LoadMediaSource()
        {
            Ready = false;
            mComposition.Clips.Clear();
            if (null != mSource)
            {
                var clip = await MediaClip.CreateFromFileAsync(mSource);
                mComposition.Clips.Add(clip);
                MediaStreamSource mediaStreamSource = mComposition.GeneratePreviewMediaStreamSource(
                        (int)400,
                        (int)400);

                var loader = new WvvMediaLoader(mPlayer);
                loader.Load(MediaSource.CreateFromMediaStreamSource(mediaStreamSource), (sender, mediaPlayer) =>
                {
                    TotalRange = sender.TotalRange;
                    VideoSize = sender.VideoSize;

                    var extractor = new WvvFrameExtractor(40, 30);
                    extractor.Extract(mediaPlayer, this, (sender2, index, image) =>
                    {
                        if(null!=image)
                        {
                            Frames.Add(image);
                        }
                        else
                        {
                            Ready = true;
                        }
                    });
                });
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if(null!=mPlayer)
            {
                mPlayer.Dispose();
                mPlayer = null;
            }
            mPlayer = new MediaPlayer();
            mPlayer.PlaybackSession.PlaybackStateChanged += PBS_StateChanged;
            mPlayerElement.SetMediaPlayer(mPlayer);

            LoadMediaSource();
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
            mSource = source;
            LoadMediaSource();
        }

        private void preview(double pos)
        {
            MediaStreamSource mediaStreamSource = mComposition.GeneratePreviewMediaStreamSource(
                    (int)mPlayerElement.ActualWidth,
                    (int)mPlayerElement.ActualHeight);
            var loader = new WvvMediaLoader(mPlayer);
            loader.Load(MediaSource.CreateFromMediaStreamSource(mediaStreamSource), (sender, player) =>
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
            });
        }

        private void OnTrimStartChanged(WvvTrimmingSlider sender, double position)
        {
            if(mComposition.Clips.Count!=1)
            {
                return;
            }
            var currentClip = mComposition.Clips[0];
            currentClip.TrimTimeFromStart = TimeSpan.FromMilliseconds(position);
            preview(0);
        }

        private void OnTrimEndChanged(WvvTrimmingSlider sender, double position)
        {
            if (mComposition.Clips.Count != 1)
            {
                return;
            }
            var currentClip = mComposition.Clips[0];
            currentClip.TrimTimeFromEnd = TimeSpan.FromMilliseconds(position);
            preview(TotalRange - position - currentClip.TrimTimeFromStart.TotalMilliseconds);
        }

        private void OnCurrentPositionChanged(WvvTrimmingSlider sender, double position)
        {
            mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(TotalRange - position);
            mPlayer.Play();
        }

    }
}
