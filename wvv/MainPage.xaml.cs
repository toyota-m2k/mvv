using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace wvv
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaPlayer mMediaPlayer;
        SoftwareBitmap frameServerDest;
        CanvasImageSource canvasImageSource;
        const int FrameCount = 20;
        ObservableCollection<ImageSource> Frames = new ObservableCollection<ImageSource>();

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            mMediaPlayer = new MediaPlayer();
            videoPlayer.SetMediaPlayer(mMediaPlayer);
            DataContext = this;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            mMediaPlayer.Dispose();
            mMediaPlayer = null;
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            mMediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/sample3.mp4"));
            mMediaPlayer.VideoFrameAvailable += mediaPlayer_VideoFrameAvailable;
            mMediaPlayer.IsVideoFrameServerEnabled = true;
            mMediaPlayer.Play();
        }

        private double mSpan = 0;
        private int mFrame = 0;
        private MediaPlayer mFrameExtractor = null;

        private void Frames_Click(object sender, RoutedEventArgs e)
        {
            mSpan = 0;
            Frames.Clear();
            if (null == mFrameExtractor)
            {
                mFrameExtractor = new MediaPlayer();
                mFrameExtractor.PlaybackSession.SeekCompleted += PlaybackSession_SeekCompleted;
                mFrameExtractor.MediaOpened += MediaPlayer_MediaOpened;
                mFrameExtractor.IsVideoFrameServerEnabled = true;
            }
            var mediaPlayer = mFrameExtractor;
            // mediaPlayer.VideoFrameAvailable += mediaPlayer_VideoFrameAvailableToExtractFrames;
            mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/sample5.mp4"));


            //mediaPlayer.PlaybackSession.Position = new TimeSpan();
            // PlaybackSession_SeekCompleted(mediaPlayer.PlaybackSession, null);   // 最初の一発目
        }

        private void MediaPlayer_MediaOpened(MediaPlayer mediaPlayer, object args)
        {
            double total = mediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
            mSpan = total / FrameCount;
            mFrame = 0;
            Debug.WriteLine(string.Format("Extracting Frames ... Span={0} / Total={1}", mSpan, total));
            PlaybackSession_SeekCompleted(mediaPlayer.PlaybackSession, null);
        }

        private async void PlaybackSession_SeekCompleted(MediaPlaybackSession session, object args)
        {
            if(mSpan==0)
            {
                return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var mediaPlayer = session.MediaPlayer;
                await mediaPlayer_VideoFrameAvailableToExtractFrames(mediaPlayer, args);
                if (mFrame < FrameCount)
                {
                    mFrame++;
                    Debug.WriteLine(string.Format("...Seek to Frame:{0} / Position:{1}", mFrame, mSpan * mFrame));
                    session.Position = TimeSpan.FromMilliseconds(mSpan * mFrame);
                }
            });
        }

        private Size ThumbnailSize = new Size(100,100);
        private async Task mediaPlayer_VideoFrameAvailableToExtractFrames(MediaPlayer mediaPlayer, object args)
        {
            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height, BitmapAlphaMode.Ignore);
                var canvasImageSrc = new CanvasImageSource(canvasDevice, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height, DisplayInformation.GetForCurrentView().LogicalDpi);//96); 
                using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, softwareBitmap))
                using (CanvasDrawingSession ds = canvasImageSrc.CreateDrawingSession(Windows.UI.Colors.Black))
                {
                    try
                    {
                        Debug.WriteLine(string.Format("...Extract Position:{0}", mediaPlayer.PlaybackSession.Position));

                        mediaPlayer.CopyFrameToVideoSurface(inputBitmap);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                        return;
                    }
                    ds.DrawImage(inputBitmap);
                    Frames.Add(canvasImageSrc);
                    softwareBitmap.Dispose();
                }
            });
        }
            
        private async void mediaPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (frameServerDest == null)
                {
                    // FrameServerImage in this example is a XAML image control
                    frameServerDest = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)FrameServerImage.Width, (int)FrameServerImage.Height, BitmapAlphaMode.Ignore);
                }
                if (canvasImageSource == null)
                {
                    canvasImageSource = new CanvasImageSource(canvasDevice, (int)FrameServerImage.Width, (int)FrameServerImage.Height, DisplayInformation.GetForCurrentView().LogicalDpi);//96); 
                    FrameServerImage.Source = canvasImageSource;
                }

                using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, frameServerDest))
                using (CanvasDrawingSession ds = canvasImageSource.CreateDrawingSession(Windows.UI.Colors.Black))
                {

                    mMediaPlayer.CopyFrameToVideoSurface(inputBitmap);

                    var gaussianBlurEffect = new GaussianBlurEffect
                    {
                        Source = inputBitmap,
                        BlurAmount = 5f,
                        Optimization = EffectOptimization.Speed
                    };

                    //ds.DrawImage(gaussianBlurEffect);
                    ds.DrawImage(inputBitmap);

                }
            });
        }

    }
}
