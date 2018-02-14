using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    public class WvvFrameExtractor
    {
        /**
         * Extractの途中経過を返すデリゲート
         * コールバック形式のExtractの場合は、すべてのフレームの抽出が終わった後に、frameIndex=-1, frameImage=nullで打ち止めコールが返る。
         * Async形式の場合は、有効なフレームに関してのみコールバックする。
         */
        public delegate void OnThumbnailExtractedHandler(WvvFrameExtractor sender, int frameIndex, ImageSource frameImage);

        #region Private Properties

        private WeakReference<MediaPlayer> mPlayer = new WeakReference<MediaPlayer>(null);
        private WeakReference<DependencyObject> mOwnerView = new WeakReference<DependencyObject>(null);
        private WeakReference<OnThumbnailExtractedHandler> mOnExtracted = new WeakReference<OnThumbnailExtractedHandler>(null);

        private MediaPlayer Player
        {
            get
            {
                MediaPlayer v;
                return mPlayer.TryGetTarget(out v) ? v : null;
            }
            set
            {
                mPlayer.SetTarget(value);
            }
        }
        private DependencyObject OwnerView
        {
            get
            {
                DependencyObject v;
                return mOwnerView.TryGetTarget(out v) ? v : null;
            }
            set
            {
                mOwnerView.SetTarget(value);
            }
        }
        private OnThumbnailExtractedHandler OnExtracted
        {
            get
            {
                OnThumbnailExtractedHandler v;
                return mOnExtracted.TryGetTarget(out v) ? v : null;
            }
            set
            {
                mOnExtracted.SetTarget(value);
            }
        }

        #endregion

        #region Internal Fields

        // 元（Extract実行時）の状態を保存するフィールド
        private bool mIsVideoFrameServerEnabled = false;
        private double mOriginalSeekPosition = 0;

        // フレーム抽出操作用
        private bool mExtracting = false;
        private SoftwareBitmap mFrameServerDest;
        private double mSpan = 0;
        private double mOffset = 0;
        private int mFrame = 0;
        private int mFrameCount;
        private double mNextPosition = 0;
        private double mThumbnailHeight;
        private Size mThumbnailSize = new Size();

        #endregion

        #region Public APIs

        /**
         * playerに設定されている動画からフレーム画像を抽出する。
         * 
         * @param thumbnailHeight サムネイル画像の高さ
         * @param frameCount フレーム数
         * @param   player      Sourceを設定してMediaOpenedの状態になっているMediaPlayerオブジェクト
         * @param   ownerView   Dispatcherを供給するビュー
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public static IAsyncOperation<bool> ExtractAsync(double thumbnailHeight, int frameCount, MediaPlayer player, DependencyObject ownerView, OnThumbnailExtractedHandler extracted)
        {
            var extractor = new WvvFrameExtractor(thumbnailHeight, frameCount);
            return extractor.ExtractAsync(player, ownerView, extracted);
        }

        public bool IsCompleted { get => mFrame >= mFrameCount; }
        /**
         * コンストラクタ
         * 
         * @param thumbnailHeight サムネイル画像の高さ
         * @param frameCount フレーム数
         */
        public WvvFrameExtractor(double thumbnailHeight, int frameCount)
        {
            mFrameCount = frameCount;
            mThumbnailHeight = thumbnailHeight;
        }

        /**
         * フレームの抽出処理を開始
         * 
         * @param   player      Sourceを設定してMediaOpenedの状態になっているMediaPlayerオブジェクト
         * @param   ownerView   Dispatcherを供給するビュー
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public void Extract(MediaPlayer player, DependencyObject ownerView, OnThumbnailExtractedHandler extracted)
        {
            mExtracting = true;

            Player = player;
            OwnerView = ownerView;
            OnExtracted = extracted;

            Debug.WriteLine("Extract: player={0}, session={1}", player.CurrentState.ToString(), player.PlaybackSession.PlaybackState.ToString());

            mIsVideoFrameServerEnabled = player.IsVideoFrameServerEnabled;
            mOriginalSeekPosition = player.PlaybackSession.Position.TotalMilliseconds;

            player.IsVideoFrameServerEnabled = true;
            player.PlaybackSession.SeekCompleted += PBS_SeekCompleted;

            double total = player.PlaybackSession.NaturalDuration.TotalMilliseconds;
            mSpan = total / mFrameCount;
            mOffset = mSpan / 2;
            mFrame = 0;
            var videoSize = new Size(player.PlaybackSession.NaturalVideoWidth, player.PlaybackSession.NaturalVideoHeight);
            mThumbnailSize.Height = mThumbnailHeight;
            mThumbnailSize.Width = videoSize.Width * mThumbnailSize.Height / videoSize.Height;
            mNextPosition = mOffset;

            //if(!player.PlaybackSession.CanSeek)
            //{
            //    Debug.WriteLine("Cannot Seek.");
            //}
            //var ranges = player.PlaybackSession.GetSeekableRanges();
            //if(null==ranges||ranges.Count==0)
            //{
            //    Debug.WriteLine("No Seekable Ranges.");
            //}
            //else
            //{
            //    bool seekable = false;
            //    foreach(var r in ranges)
            //    {
            //        if(r.Start.TotalMilliseconds<=mNextPosition && mNextPosition<=r.End.TotalMilliseconds)
            //        {
            //            seekable = true;
            //            break;
            //        }
            //    }
            //    if(!seekable)
            //    {
            //        Debug.WriteLine("Bad Seekable Range.");
            //    }
            //}

            player.PlaybackSession.Position = TimeSpan.FromMilliseconds(mNextPosition);
        }

        /**
         * フレームの抽出処理（async版）
         * 
         * @param   player      Sourceを設定してMediaOpenedの状態になっているMediaPlayerオブジェクト
         * @param   ownerView   Dispatcherを供給するビュー
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public IAsyncOperation<bool> ExtractAsync(MediaPlayer player, DependencyObject ownerView, OnThumbnailExtractedHandler extracted)
        {
            Debug.WriteLine("ExtractAsync: async operation started.");
            return AsyncInfo.Run<bool>((token) =>
            {
                return Task.Run<bool>(async () =>
                {
                    using (var ev = new ManualResetEvent(false))
                    {
                        await ownerView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            Extract(player, ownerView, (s, index, image) =>
                            {
                                if (index < 0)
                                {
                                    // Finished
                                    ev.Set();
                                }
                                else
                                {
                                    extracted(this, index, image);
                                }
                            });
                        });
                        try
                        {
                            while(!ev.WaitOne(100))
                            {
                                if(token.IsCancellationRequested)
                                {
                                    Cancel();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                        Debug.WriteLine("ExtractAsync: async operation finished.");
                        return IsCompleted;
                    }
                });
            });
        }

        /**
         * フレーム取得操作をキャンセルする。
         */
        public async void Cancel()
        {
            await OwnerView?.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (mExtracting)
                {
                    finish();
                }
            });
        }
        
        #endregion

        #region Private Methods

        /**
         * 処理完了
         */
        private void finish()
        {
            mExtracting = false;

            // Playerの状態を元に戻す
            try
            {
                Player.PlaybackSession.SeekCompleted -= PBS_SeekCompleted;
                Player.PlaybackSession.Position = TimeSpan.FromMilliseconds(mOriginalSeekPosition);
                Player.IsVideoFrameServerEnabled = mIsVideoFrameServerEnabled;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            // Last callback.
            OnExtracted?.Invoke(this, -1, null);

            // メンバーをクリア
            Player = null;
            OnExtracted = null;
            OwnerView = null;
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
            Debug.WriteLine("SeekCompleted: player={0}, session={1}", Player.CurrentState.ToString(), Player.PlaybackSession.PlaybackState.ToString());

            await OwnerView?.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    if (!mExtracting)
                    {
                        return;
                    }

                    var mediaPlayer = session.MediaPlayer;
                    extractFrame(mediaPlayer);

                    if (mFrame < mFrameCount)
                    {

                        mFrame++;
                        mNextPosition = mOffset + mSpan * mFrame;
                        Debug.WriteLine(string.Format("...Seek to Frame:{0} / Position:{1}", mFrame, mNextPosition));
                        session.Position = TimeSpan.FromMilliseconds(mNextPosition);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }

                // Clean up
                finish();
            });
        }

        /**
         * フレームを画像に取り出してコールバックする
         */
        private void extractFrame(MediaPlayer mediaPlayer)
        {
            Debug.WriteLine("extractFrame: player={0}, session={1}", Player.CurrentState.ToString(), Player.PlaybackSession.PlaybackState.ToString());

            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();
            var canvasImageSrc = new CanvasImageSource(canvasDevice, (int)mThumbnailSize.Width, (int)mThumbnailSize.Height, 96/*DisplayInformation.GetForCurrentView().LogicalDpi*/);
            using (SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)mThumbnailSize.Width, (int)mThumbnailSize.Height, BitmapAlphaMode.Ignore))
            using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, softwareBitmap))
            using (CanvasDrawingSession ds = canvasImageSrc.CreateDrawingSession(Windows.UI.Colors.Black))
            {
                Debug.WriteLine(string.Format("...Extract Position:{0} (State={1})", mediaPlayer.PlaybackSession.Position, mediaPlayer.PlaybackSession.PlaybackState.ToString()));
                mediaPlayer.CopyFrameToVideoSurface(inputBitmap);
                ds.DrawImage(inputBitmap);
                OnExtracted?.Invoke(this, mFrame, canvasImageSrc);
            }
        }

        #endregion
    }
}


