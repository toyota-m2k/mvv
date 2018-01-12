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
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
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
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            mPlayer.Stop();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            mMediaPlayer = new MediaPlayer();
            videoPlayer.SetMediaPlayer(mMediaPlayer);
            App.Current.Suspending += OnSuspending;
            DataContext = this;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            App.Current.Suspending -= OnSuspending;
            if (null!=mMediaPlayer)
            {
                mMediaPlayer.Dispose();
                mMediaPlayer = null;
            }

            if (null!=mInvisibleMediaPlayer)
            {
                mInvisibleMediaPlayer.Dispose();
                mInvisibleMediaPlayer = null;
            }
        }
        StorageFile mVideoFile = null;
        delegate void PlayAction(object sender, RoutedEventArgs e);

        private async Task pickAndPlay(PlayAction action)
        {
            // Create and open the file picker
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            openPicker.FileTypeFilter.Add(".mp4");
            openPicker.FileTypeFilter.Add(".mkv");
            openPicker.FileTypeFilter.Add(".avi");

            mVideoFile = await openPicker.PickSingleFileAsync();
            if (null != mVideoFile && null != action)
            {
                action(null, null);
            }
        }


        private async void PickFile_Click(object sender, RoutedEventArgs e)
        {
            await pickAndPlay(null);
        }


        /**
         * 単純に、MediaPlayerで動画を再生する
         */
        #region Simple MediaPlayer

        MediaPlayer mMediaPlayer;

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (null == mVideoFile)
            {
                var v = pickAndPlay(Play_Click);
                return;
            }
            mMediaPlayer.Source = MediaSource.CreateFromStorageFile(mVideoFile);
            //mMediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/sample3.mp4"));
            mMediaPlayer.Play();
        }

        #endregion

        #region MediaPlayerからフレームを取り出して自前で描画

        private MediaPlayer mInvisibleMediaPlayer = null;
        private SoftwareBitmap frameServerDest;
        private CanvasImageSource canvasImageSource;


        private void Play2_Click(object sender, RoutedEventArgs e)
        {
            if (null == mVideoFile)
            {
                var v = pickAndPlay(Play2_Click);
                return;
            }
            if(null==mInvisibleMediaPlayer)
            {
                mInvisibleMediaPlayer = new MediaPlayer();
            }
            mInvisibleMediaPlayer.Source = MediaSource.CreateFromStorageFile(mVideoFile);
            mInvisibleMediaPlayer.VideoFrameAvailable += mediaPlayer_VideoFrameAvailable;
            mInvisibleMediaPlayer.IsVideoFrameServerEnabled = true;
            mInvisibleMediaPlayer.Play();
        }
        
        private async void mediaPlayer_VideoFrameAvailable(MediaPlayer mediaPlayer, object args)
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

                    mediaPlayer.CopyFrameToVideoSurface(inputBitmap);

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

        #endregion

        #region フレーム抽出

        ObservableCollection<ImageSource> Frames = new ObservableCollection<ImageSource>();

        private Size mThumbnailSize = new Size(45, 45);        // サムネイルのサイズ
        private const int FrameCount = 20;                      // 取得するフレーム数（動画全体をこの数で分割して、それぞれの位置のフレームを取り出す）
        private double mOffset = 0;
        private double mSpan = 0;                               // 分割された動画の１区間のスパン
        private int mFrame = 0;                                 // 現在取得中のフレーム番号（０～FrameCount）
        private MediaPlayer mFrameExtractor = null;

        private void Frames_Click(object sender, RoutedEventArgs e)
        {
            if (null == mVideoFile)
            {
                var v = pickAndPlay(Frames_Click);
                return;
            }
            mSpan = 0;
            mOffset = 0;
            Frames.Clear();
            if (null == mFrameExtractor)
            {
                mFrameExtractor = new MediaPlayer();
                mFrameExtractor.PlaybackSession.SeekCompleted += PlaybackSession_SeekCompleted;
                mFrameExtractor.MediaOpened += MediaPlayer_MediaOpened;
                mFrameExtractor.IsVideoFrameServerEnabled = true;
            }
            var mediaPlayer = mFrameExtractor;
            //mediaPlayer.VideoFrameAvailable += extractor_VideoFrameAvailable;
            mediaPlayer.Source = MediaSource.CreateFromStorageFile(mVideoFile);


            //mediaPlayer.PlaybackSession.Position = new TimeSpan();
            // PlaybackSession_SeekCompleted(mediaPlayer.PlaybackSession, null);   // 最初の一発目
        }

        // FrameAvailable の後で、SeekCompletedが呼ばれるので、こちらは処理不要
        //private void extractor_VideoFrameAvailable(MediaPlayer sender, object args)
        //{
        //    Debug.WriteLine(string.Format("Frame Available: Position: {0}", sender.PlaybackSession.Position));
        //}
        private Size mVideoSize = new Size(0, 0);
        private void MediaPlayer_MediaOpened(MediaPlayer mediaPlayer, object args)
        {
            Debug.WriteLine(string.Format("MediaOpened"));

            double total = mediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
            mSpan = total / (FrameCount + 1);
            mOffset = mSpan / 2;
            mFrame = 0;
            mVideoSize.Width = mediaPlayer.PlaybackSession.NaturalVideoWidth;
            mVideoSize.Height = mediaPlayer.PlaybackSession.NaturalVideoHeight;
            mThumbnailSize.Width = mVideoSize.Width * mThumbnailSize.Height / mVideoSize.Height;
            Debug.WriteLine(string.Format("Extracting Frames ... Span={0} / Total={1}", mSpan, total));
            mediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mOffset);
            // PlaybackSession_SeekCompleted(mediaPlayer.PlaybackSession, null);
        }

        private async void PlaybackSession_SeekCompleted(MediaPlaybackSession session, object args)
        {
            Debug.WriteLine(string.Format("SeekCompleted : Position:{0}", session.Position));

            if (mSpan==0)
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

                if (mFrame < FrameCount)
                {
                    mFrame++;
                    Debug.WriteLine(string.Format("...Seek to Frame:{0} / Position:{1}", mFrame, mOffset + mSpan * mFrame));
                    session.Position = TimeSpan.FromMilliseconds(mOffset + mSpan * mFrame);
                }
                else
                {
                    mFrameExtractor.Dispose();
                    mFrameExtractor = null;
                }
            });
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
                    Debug.WriteLine(string.Format("...Extract Position:{0} (State={1})", mediaPlayer.PlaybackSession.Position, mediaPlayer.PlaybackSession.PlaybackState.ToString()));

                    mediaPlayer.CopyFrameToVideoSurface(inputBitmap);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    return;
                }
                ds.DrawImage(inputBitmap);
                Frames.Add(canvasImageSrc);
            }
        }
#if false
        private async Task mediaPlayer_VideoFrameAvailableToExtractFrames(MediaPlayer mediaPlayer, object args)
        {
            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var canvasImageSrc = new CanvasImageSource(canvasDevice, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height, DisplayInformation.GetForCurrentView().LogicalDpi);//96); 
                using (SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height, BitmapAlphaMode.Ignore))
                using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, softwareBitmap))
                using (CanvasDrawingSession ds = canvasImageSrc.CreateDrawingSession(Windows.UI.Colors.Black))
                {
                    try
                    {
                        Debug.WriteLine(string.Format("...Extract Position:{0} (State={1})", mediaPlayer.PlaybackSession.Position, mediaPlayer.PlaybackSession.PlaybackState.ToString()));

                        mediaPlayer.CopyFrameToVideoSurface(inputBitmap);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                        return;
                    }
                    ds.DrawImage(inputBitmap);
                    Frames.Add(canvasImageSrc);
                }
            });
        }
#endif

        #endregion

        private void Play3_Click(object sender, RoutedEventArgs e)
        {
            if (null == mVideoFile)
            {
                var v = pickAndPlay(Play3_Click);
                return;
            }
           mPlayer.SetSource(MediaSource.CreateFromStorageFile(mVideoFile));

        }
    }
}
