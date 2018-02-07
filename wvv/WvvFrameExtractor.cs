using Microsoft.Graphics.Canvas;
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
    public class WvvFrameExtractor
    {
        public delegate void OnThumbnailExtractedHandler(WvvFrameExtractor sender, int frameIndex, ImageSource frameImage);

        private DependencyObject mOwnerView;
        private MediaPlayer mPlayer;
        private bool mIsVideoFrameServerEnabled = false;
        private double mOriginalSeekPosition = 0;

        private SoftwareBitmap mFrameServerDest;
        private double mSpan = 0;
        private double mOffset = 0;
        private int mFrame = 0;
        private int mFrameCount;
        private double mNextPosition = 0;
        private double mThumbnailHeight;
        private Size mThumbnailSize = new Size();
        private OnThumbnailExtractedHandler mOnExtracted;

        public WvvFrameExtractor(double thumbnailHeight, int frameCount)
        {
            mFrameCount = frameCount;
            mThumbnailHeight = thumbnailHeight;
        }

        public void Extract(MediaPlayer player, UIElement ownerView, OnThumbnailExtractedHandler extracted)
        {
            mPlayer = player;
            mOwnerView = ownerView;

            mIsVideoFrameServerEnabled = mPlayer.IsVideoFrameServerEnabled;
            mOriginalSeekPosition = mPlayer.PlaybackSession.Position.TotalMilliseconds;

            mPlayer.IsVideoFrameServerEnabled = true;
            mPlayer.PlaybackSession.SeekCompleted += PBS_SeekCompleted;
            mOnExtracted = extracted;

            double total = mPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
            mSpan = total / mFrameCount;
            mOffset = mSpan / 2;
            mFrame = 0;
            var videoSize = new Size(mPlayer.PlaybackSession.NaturalVideoWidth, mPlayer.PlaybackSession.NaturalVideoHeight);
            mThumbnailSize.Height = mThumbnailHeight;
            mThumbnailSize.Width = videoSize.Width * mThumbnailSize.Height / videoSize.Height;
            mNextPosition = mOffset;
            mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mNextPosition);
        }

        private void completed()
        {
            // Playerの状態を元に戻す
            mPlayer.PlaybackSession.SeekCompleted -= PBS_SeekCompleted;
            mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mOriginalSeekPosition);
            mPlayer.IsVideoFrameServerEnabled = mIsVideoFrameServerEnabled;

            // Last callback.
            mOnExtracted(this, -1, null);

            // メンバーをクリア
            mPlayer = null;
            mOnExtracted = null;
            mOwnerView = null;
            if (null != mFrameServerDest)
            {
                mFrameServerDest.Dispose();
                mFrameServerDest = null;
            }
        }

        /**
         * シークが完了したときの処理
         * - フレームサムネイルを作成
         */
        private async void PBS_SeekCompleted(MediaPlaybackSession session, object args)
        {
            Debug.WriteLine(string.Format("SeekCompleted : Position:{0}", session.Position));

            //var d = Math.Abs(session.Position.TotalMilliseconds - mNextPosition);
            //if (d > mSpan / 3)
            //{
            //    Debug.WriteLine("extracting: NextFrame:{0} NextPosition:{1} CurrentFrame:{2}", mFrame, mNextPosition, session.Position.TotalMilliseconds);
            //    return;
            //}
            //mFrame++;
            //mNextPosition = mOffset + mSpan * mFrame;

            await mOwnerView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var mediaPlayer = session.MediaPlayer;
                extractFrame(mediaPlayer);

                if (mFrame < mFrameCount)
                {

                    mFrame++;
                    mNextPosition = mOffset + mSpan * mFrame;
                    Debug.WriteLine(string.Format("...Seek to Frame:{0} / Position:{1}", mFrame, mNextPosition));
                    session.Position = TimeSpan.FromMilliseconds(mNextPosition);
                }
                else
                {
                    // Clean up
                    completed();
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
                    ds.DrawImage(inputBitmap);
                }
                catch (Exception e)
                {
                    // 無視する
                    Debug.WriteLine(e.ToString());
                }
                mOnExtracted(this, mFrame, canvasImageSrc);
            }
        }
    }
}


