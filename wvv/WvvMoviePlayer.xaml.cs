using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
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
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
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
    public sealed partial class WvvMoviePlayer : UserControl
    {
        private class PlayerContext : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void notify(string propName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }

            private bool mMoviePrepared = false;
            public bool MoviePrepared
            {
                get { return mMoviePrepared; }
                set
                {
                    if(mMoviePrepared != value)
                    {
                        mMoviePrepared = value;
                        notify("MoviePrepared");
                    }
                }
            }

            private Size mPlayerSize = new Size(300, 300);
            public Size PlayerSize
            {
                get { return mPlayerSize; }
                set
                {
                    if(mPlayerSize != value)
                    {
                        mPlayerSize = value;
                        notify("PlayerSize");
                    }
                }
            }

            private bool mPlaying = false;
            public bool IsPlaying
            {
                get { return mPlaying; }
                set
                {
                   if(mPlaying!=value)
                    {
                        mPlaying = value;
                        notify("IsPlaying");
                    }
                }
            }

            public bool mShowingFrames = false;
            public bool ShowingFrames
            {
                get { return mShowingFrames; }
                set
                {
                    if (mShowingFrames != value)
                    {
                        mShowingFrames = value;
                        notify("ShowingFrames");
                    }
                }
            }

            public ObservableCollection<ImageSource> Frames
            {
                get;
            } = new ObservableCollection<ImageSource>();

            private double mTotalRange = 100;

            public double TotalRange
            {
                get { return mTotalRange; }
                set
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
            public double SmallChange
            {
                get { return mTotalRange / 100; }
            }
            public double LargeChange
            {
                get { return mTotalRange / 20; }
            }
        }

        public WvvMoviePlayer()
        {
            this.InitializeComponent();
            this.DataContext = new PlayerContext();
        }

        private MediaPlayer MediaPlayer
        {
            get { return mMoviePlayer.MediaPlayer; }
        }
        private MediaPlaybackSession PlaybackSession
        {
            get { return mMoviePlayer.MediaPlayer.PlaybackSession; }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mMoviePlayer.SetMediaPlayer(new MediaPlayer());
            PlaybackSession.SeekCompleted += PBS_SeekCompleted;
            PlaybackSession.PositionChanged += PBS_PositionChanged;
            PlaybackSession.PlaybackStateChanged+= PBS_PlaybackStateChanged;
            MediaPlayer.MediaOpened += MP_MediaOpened;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MediaPlayer.PlaybackSession.SeekCompleted -= PBS_SeekCompleted;
            PlaybackSession.PositionChanged -= PBS_PositionChanged;
            PlaybackSession.PlaybackStateChanged -= PBS_PlaybackStateChanged;
            MediaPlayer.MediaOpened -= MP_MediaOpened;
            MediaPlayer.Dispose();
        }

        int mFrameCount = 20;
        double mReqPosition = 0;
        double mSpan;
        double mOffset;
        int mFrame;
        Size mVideoSize = new Size(0,0);
        Size mThumbnailSize = new Size(44, 44);

        private void MP_MediaOpened(MediaPlayer mediaPlayer, object args)
        {
            double total = mediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
            mSpan = total / (mFrameCount + 1);
            mOffset = mSpan / 2;
            mFrame = 0;
            mVideoSize.Width = mediaPlayer.PlaybackSession.NaturalVideoWidth;
            mVideoSize.Height = mediaPlayer.PlaybackSession.NaturalVideoHeight;
            mThumbnailSize.Width = mVideoSize.Width * mThumbnailSize.Height / mVideoSize.Height;
            mediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mOffset);
        }

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
                }
                CTX.Frames.Add(canvasImageSrc);
            }
        }

        private async void PBS_SeekCompleted(MediaPlaybackSession session, object args)
        {
            if (mSpan == 0 || !session.MediaPlayer.IsVideoFrameServerEnabled)
            {
                    return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var mediaPlayer = session.MediaPlayer;
                //if (mFrame==0)
                //{
                //    mediaPlayer.StepForwardOneFrame();
                //}
                //else if(mFrame==FrameCount)
                //{
                //    mediaPlayer.StepBackwardOneFrame();
                //}

                extractFrame(mediaPlayer);

                if (mFrame < mFrameCount)
                {
                    mFrame++;
                    session.Position = TimeSpan.FromMilliseconds(mOffset + mSpan * mFrame);
                }
                else
                {
                    // OK, Movie is ready now!
                    PlaybackSession.Position = TimeSpan.FromMilliseconds(0);
                    MediaPlayer.IsVideoFrameServerEnabled = false;
                    mMoviePlayer.Height = 300;
                    mMoviePlayer.Width = mVideoSize.Width * mMoviePlayer.Height / mVideoSize.Height;
                    mSlider.Value = 0;
                    CTX.TotalRange = session.NaturalDuration.TotalMilliseconds;
                    CTX.MoviePrepared = true;
                }
            });
        }

        private async void PBS_PlaybackStateChanged(MediaPlaybackSession session, object args)
        {
            if (session.MediaPlayer.IsVideoFrameServerEnabled)
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

        private async void PBS_PositionChanged(MediaPlaybackSession session, object args)
        {
            if (session.MediaPlayer.IsVideoFrameServerEnabled)
            {
                return;
            }
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                mReqPosition = session.Position.TotalMilliseconds;
                mSlider.Value = mReqPosition;
                var border = VisualTreeHelper.GetChild(mFrameListView, 0) as Border;
                if (null != border)
                {
                    var scrollViewer = border.Child as ScrollViewer;
                    double offset = (scrollViewer.ExtentWidth - scrollViewer.ViewportWidth) * mReqPosition / CTX.TotalRange;
                    Debug.WriteLine("Scroll from {0} to {1}", scrollViewer.HorizontalOffset, offset);
                    scrollViewer.ChangeView(offset, null, null);
                }
            });
        }


        private PlayerContext CTX
        {
            get { return (PlayerContext)DataContext; }
        }

        private void OnButtoPlayStop(object sender, RoutedEventArgs e)
        {
            //CTX.IsPlaying = !CTX.IsPlaying;
            if(CTX.MoviePrepared)
            {
                if(CTX.IsPlaying)
                {
                    MediaPlayer.Pause();
                }
                else
                {
                    MediaPlayer.Play();
                }
            }
        }

        private void OnShowHideFrameList(object sender, RoutedEventArgs e)
        {
            CTX.ShowingFrames = !CTX.ShowingFrames;
        }

        public void SetSource(MediaSource source)
        {
            CTX.Frames.Clear();
            CTX.IsPlaying = false;
            CTX.MoviePrepared = false;

            mMoviePlayer.MediaPlayer.IsVideoFrameServerEnabled = true;
            mMoviePlayer.MediaPlayer.Source = source;       // MediaPlayerが動画ファイルを読み込んだら MP_MediaOpened が呼ばれる。
        }

        private void OnFullScreen(object sender, RoutedEventArgs e)
        {
            mMoviePlayer.AreTransportControlsEnabled = true;
            mMoviePlayer.IsFullWindow = true;

        }

        private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var slider = sender as Slider;
            if (null != slider && mReqPosition!=slider.Value)
            {
                PlaybackSession.Position = TimeSpan.FromMilliseconds(slider.Value);
            }
        }

        private void OnPlayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(!mMoviePlayer.IsFullWindow)
            {
                mMoviePlayer.AreTransportControlsEnabled = false;
            }
        }
    }
}
