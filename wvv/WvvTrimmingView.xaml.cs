using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using wvv.utils;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    /**
     * トリミング結果をファイルに保存するためのi/f
     */
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

        //#region IWvvSaveAs i/f

        /**
         * トリミング結果をファイルに保存
         */
        public IAsyncOperationWithProgress<TranscodeFailureReason, double> SaveToFile(StorageFile toFile)
        {
            return mComposition.RenderToFileAsync(toFile);
        }

        //#endregion

        #region Private Fields / Properties

        // 動画ソースファイル (OnLoaded前にセットされる場合にのみ使用される）
        private StorageFile mSource;

        // 再生中のトラッキングサムを移動させるためのタイマー
        private DispatcherTimer TrackingTimer { get; set; }

        // 動画表示用コントロール
        private MediaPlayer mPlayer;

        // 動画編集用
        private MediaComposition mComposition;

        // 動画の元ファイルを保持するソースオブジェクト（トラッキングモード用）
        private MediaSource mOriginalSource;

        // トラッキングモード(false)とプレビューモード(true)を区別するフラグ
        bool mPreviewing = false;

        // 動画の総再生時間
        private double TotalRange
        {
            get { return mTrimmingSlider.TotalRange; }
            set { mTrimmingSlider.TotalRange = value; }
        }

        /**
         * プレイヤーの配置サイズのヒント
         */
        public Size LayoutSize
        {
            get { return mLayoutSize; }
            set
            {
                if (value != mLayoutSize)
                {
                    mLayoutSize = value;
                    updatePlayerSize();
                }
            }
        }
        private Size mLayoutSize = new Size(300, 300);

        /**
         * 動画のサイズ
         */
        private Size VideoSize
        {
            get { return mVideoSize; }
            set
            {
                if (mVideoSize != value)
                {
                    mVideoSize = value;
                    updatePlayerSize();
                }
            }
        }
        private Size mVideoSize = new Size(300, 300);

        /**
         * 動画の実サイズに合わせてプレイヤーのサイズを調整する
         */
        private void updatePlayerSize()
        {
            PlayerSize = calcFittingSize(mVideoSize.Width, mVideoSize.Height, mLayoutSize.Width, mLayoutSize.Height);
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

        private WvvFrameExtractor2 mExtractor;

        public int ThumbnailHeight { get; set; } = 40;
        public int ThumbnailCount { get; set; } = 30;

        #endregion

        #region Bindings

        /**
         * フレームリスト
         */
        public ObservableCollection<ImageSource> Frames
        {
            get { return mFrameListView?.Frames; }
        }

        /**
         * 動画の再生は可能か？
         */
#if true
        public bool Ready
        {
            get { return mReady; }
            private set
            {
                if (value != mReady)
                {
                    mReady = value;
                    notify("Ready");
                }
            }
        }
        private bool mReady = false;
#else

        public static readonly DependencyProperty ReadyProperty =
               DependencyProperty.Register("Ready",
                                           typeof(bool),
                                           typeof(WvvTrimmingView),
                                           new PropertyMetadata(false, (d,e)=>
                                           {
                                               Debug.WriteLine("DP-Ready:" + ((WvvTrimmingView)d).Ready.ToString());
                                           }));

        // 2. CLI用プロパティを提供するラッパー
        public bool Ready
        {
            get { return (bool)GetValue(ReadyProperty); }
            set { SetValue(ReadyProperty, value); }
        }
#endif
        /**
         * 動画再生中か？
         */
        public bool IsPlaying
        {
            get { return mIsPlaying; }
            private set
            {
                if (value != mIsPlaying)
                {
                    mIsPlaying = value;

                    if (value)
                    {
                        TrackingTimer.Start();
                    }
                    else
                    {
                        TrackingTimer.Stop();
                    }
                    notify("IsPlaying");
                }
            }
        }
        private bool mIsPlaying = false;


        /**
         * プレイヤーのサイズ（xamlレンダリング用）
         */
        public Size PlayerSize
        {
            get; private set;
        }

        /**
         * エンコード中か？
         */
        public bool Encoding
        {
            get { return mEncoding; }
            set
            {
                if (mEncoding != value)
                {
                    mEncoding = value;
                    notify("Encoding");
                }
            }
        }
        private bool mEncoding = false;

        public int EncodingProgress
        {
            get { return mEncodingProgress; }
            set
            {
                if (mEncodingProgress != value)
                {
                    mEncodingProgress = value;
                    notify("EncodingProgress");
                }
            }
        }
        private int mEncodingProgress = 0;

        #endregion

        #region Public Properties

        /**
         * トリミングされているか？
         */
        public bool IsTrimmed
        {
            get
            {
                return mTrimmingSlider.TrimStart > 0 || mTrimmingSlider.TrimEnd > 0;
            }
        }

        /**
         * トリミング位置を変更したときに、CurrentPositionをゼロ(==TrimStart)にリセットする場合は true
         */
        public static bool ResetCurrentPositionOnTrimmed = true;

        #endregion

        #region Initialization / Tremination

        /**
         * コンストラクタ
         */
        public WvvTrimmingView()
        {
            this.DataContext = this;
            this.InitializeComponent();
            mSource = null;
            mComposition = new MediaComposition();
            mExtractor = new WvvFrameExtractor2(ThumbnailHeight, ThumbnailCount);
        }

        /**
         * 動画ファイルをロードして、フレームサムネイルを抽出する。
         */
        private async void LoadMediaSource(StorageFile source)
        {
            Ready = false;

            //
            mPlayer.Source = null;
            mPreviewing = false;
            mTrimmingSlider.Reset();
            mFrameListView.Reset();
            mExtractor.Cancel();
            mFrameListView.ShowCurrentTick = false;

            mComposition.Clips.Clear();
            if (null != source)
            {
                mOriginalSource = MediaSource.CreateFromStorageFile(source);
                var loader = await WvvMediaLoader.LoadAsync(mPlayer, mOriginalSource, this);
                if(loader.Opened)
                {
                    TotalRange = loader.TotalRange;
                    VideoSize = loader.VideoSize;
#if false
                    if (await WvvFrameExtractor.ExtractAsync(40, 30, mPlayer, this, (s, index, image) =>
                     {
                         Debug.WriteLine("Frame Extracted : {0}", index);
                         Frames.Add(image);
                     })) {
                        Debug.WriteLine("Frame Extracted : Finalized.");
                        Ready = true;
                        var clip = await MediaClip.CreateFromFileAsync(source);
                        mComposition.Clips.Add(clip);
                    }
#else
                    try
                    {
                        var clip = await MediaClip.CreateFromFileAsync(source);
                        mComposition.Clips.Add(clip.Clone());
                        Ready = true;
                        await mExtractor.ExtractAsync(clip, (s, i, img) =>
                        {
                            mFrameListView.Frames[i] = img;

                        },
                        (s, img) =>
                        {
                            mFrameListView.FrameListHeight = mExtractor.ThumbnailHeight;
                            for (int i = 0; i < ThumbnailCount; i++)
                            {
                                mFrameListView.Frames.Add(img);
                            }
                            mFrameListView.ShowCurrentTick = true;
                        });
                    }
                    catch (Exception e)
                    {
                        CmLog.error(e, "WvvTrimmingView.LoadMediaSource: Error");
                    }

                        //if (await WvvFrameExtractor2.ExtractAsync(40, 30, clip, (s, index, image) =>
                        //   {
                        //       Debug.WriteLine("Frame Extracted : {0}", index);
                        //       Frames.Add(image);
                        //   }))
                        //{
                        //    Ready = true;
                        //    mComposition.Clips.Add(clip);
                        //    mFrameListView.ShowCurrentTick = showTick;
                        //}
                        //else
                        //{
                        //    // Error!
                        //    // what can i do?
                        //}
#endif

                    }
            }
        }

        /**
         * ビューがロードされたときの初期化処理
         */
        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            TrackingTimer = new DispatcherTimer();
            TrackingTimer.Interval = TimeSpan.FromMilliseconds(10);
            TrackingTimer.Tick += (s, a) =>
            {
                if (mPreviewing)
                {
                    if (null != mPlayer)
                    {
                        mTrimmingSlider.CurrentPosition = mPlayer.PlaybackSession.Position.TotalMilliseconds;
                        mFrameListView.TickPosition = mTrimmingSlider.AbsoluteCurrentPosition / mTrimmingSlider.TotalRange;
                    }
                }
            };

            mPlayer = new MediaPlayer();
            mPlayer.PlaybackSession.PlaybackStateChanged += PBS_StateChanged;
            mPlayerElement.SetMediaPlayer(mPlayer);

            if (null!=mSource)
            {
                var s = mSource;
                mSource = null;
                LoadMediaSource(s);
            }
        }

        /**
         * ビューがアンロードロードされたときのクリーンアップ処理
         */
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            TrackingTimer.Stop();

            Ready = false;
            mPlayerElement.SetMediaPlayer(null);
            mPlayer.Pause();
            mPlayer.PlaybackSession.PlaybackStateChanged -= PBS_StateChanged;
            mPlayer.Dispose();
            mPlayer = null;
        }

        #endregion

        #region Media Player Events

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

#endregion

        #region Public Methods

        /**
         * ソースファイル（mp4限定）をセットする。
         */
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

        #region IWvvSaveAs i/f

        public delegate void TrimmingCompleted(WvvTrimmingView sender, bool succeeded);
        private IAsyncOperationWithProgress<TranscodeFailureReason, double> mTrimmingTask;

        /**
         * トリミング結果をファイルに保存（コールバック版）
         */
        public void SaveAs(StorageFile toFile, TrimmingCompleted completed)
        {
            if(mComposition.Clips.Count==0)
            {
                completed(this, false);
                return;
            }

            IsPlaying = false;
            Encoding = true;
            mTrimmingTask = mComposition.RenderToFileAsync(toFile, MediaTrimmingPreference.Precise, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p));
            mTrimmingTask.Progress = async (ai, pi) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    EncodingProgress = (int)Math.Ceiling(pi);
                });
            };
            mTrimmingTask.Completed = async (info, status) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    completed(this, status== AsyncStatus.Completed);
                    Encoding = false;
                });
            };
        }

        /**
         * トリミング結果をファイルに保存（Async版）
         */
        public async Task<bool> SaveAsAsync(StorageFile toFile)
        {
            if (mComposition.Clips.Count == 0)
            {
                return false;
            }

            bool result = false;
            IsPlaying = false;
            Encoding = true;
            try
            {
                mTrimmingTask = mComposition.RenderToFileAsync(toFile, MediaTrimmingPreference.Precise, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p));
                mTrimmingTask.Progress = async (ai, pi) =>
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        EncodingProgress = (int)Math.Ceiling(pi);
                    });
                };

                //mTrimmingTask.Completed = async (info, status) =>
                //{
                //    result = status == AsyncStatus.Completed;
                //    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                //    {
                //        Encoding = false;
                //    });
                //};
                //await mTrimmingTask;                  // これだとInvalidOperationExceptionが出る（Completedハンドラを指定しているとawaitできないようだ。
                //await Task.Run(() => mTrimmingTask);  // これだとちゃんと待たない

                var err = await mTrimmingTask;
                if(err == TranscodeFailureReason.None)
                {
                    result = true;
                }
                mTrimmingTask = null;
                Encoding = false;
                Debug.WriteLine("Transcode result={0}", err.ToString());
                return result;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

        #endregion


        #endregion

        #region Preview / Trimming Mode

        /**
         * プレビューモードを開始する。
         * 
         * トリミングモード：
         *  MediaPlayerのソースに、オリジナルのソース（StorageFileから生成したもの）をセットした状態。
         *  トリミング操作は、常にこのモードで行い、再生は行わない。
         * プレビューモード:
         *  MediaPlayerのソースに、MediaComposition から生成したストリームを指定し、トリミング後の動画を再生テストするモード
         *  トリミング操作を行うと、自動的にこのモードはキャンセルされ、全体表示モードに戻る。
         */
        private async Task startPreview(bool play)
        {
            if (mPreviewing)
            {
                if (!IsPlaying)
                {
                    mPlayer.Play();
                }
                return;
            }
            if (mTrimmingSlider.TrimmedRange < 100   || mComposition.Clips.Count != 1)
            {
                return;
            }
            mComposition.Clips[0].TrimTimeFromStart = TimeSpan.FromMilliseconds(mTrimmingSlider.TrimStart);
            mComposition.Clips[0].TrimTimeFromEnd = TimeSpan.FromMilliseconds(mTrimmingSlider.TrimEnd);

            try
            {
                mPreviewing = true;
                MediaStreamSource mediaStreamSource = mComposition.GeneratePreviewMediaStreamSource(
                        (int)mPlayerElement.ActualWidth,
                        (int)mPlayerElement.ActualHeight);

                var loader = await WvvMediaLoader.LoadAsync(mPlayer, MediaSource.CreateFromMediaStreamSource(mediaStreamSource), this);
                if (null != loader)
                {
                    if (mPreviewing)
                    {
                        mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mTrimmingSlider.CurrentPosition);
                        if (play)
                        {
                            mPlayer.Play();
                        }
                    }
                }
                else
                {
                    mPreviewing = false;
                }

            }
            catch (Exception e)
            {
                CmLog.error(e, "WvvTrimmingView.startPreview: Error");
                mPlayer.Pause();
                mPreviewing = false;
            }

        }

        enum PositionOf{ START, END, CURRENT };

        /**
         * トリミングモードでのシーク位置を取得
         * 
         * ちなみに、プレビューモードでは、mTrimmingSlider.CurrentPositionとシーク位置が一致する。
         */
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

        /**
         * プレビューモードを終了して、トリミングモードに戻る。
         */
        private async Task stopPreview(PositionOf seekTo)
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
            var loader = await WvvMediaLoader.LoadAsync(mPlayer, mOriginalSource, this);
            if(null!=loader)
            {
                if(!mPreviewing)
                {
                    mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(seekPosition(seekTo));
                }
            }
        }

        #endregion

        #region Trimming Slider Handling

        /**
         * TrimStartが操作された
         */
        private async void OnTrimStartChanged(WvvTrimmingSlider sender, double position, bool finalize)
        {
            if(mComposition.Clips.Count!=1)
            {
                return;
            }
            //var currentClip = mComposition.Clips[0];
            //currentClip.TrimTimeFromStart = TimeSpan.FromMilliseconds(position);
            await stopPreview(PositionOf.START);

            if (ResetCurrentPositionOnTrimmed)
            {
                mTrimmingSlider.CurrentPosition = 0;
            }
            mFrameListView.LeftTrim = position / sender.TotalRange;
        }

        /**
         * TrimEndが操作された
         */
        private async void OnTrimEndChanged(WvvTrimmingSlider sender, double position, bool finalize)
        {
            if (mComposition.Clips.Count != 1)
            {
                return;
            }
            //var currentClip = mComposition.Clips[0];
            //currentClip.TrimTimeFromEnd = TimeSpan.FromMilliseconds(position);
            await stopPreview(PositionOf.END);

            if (ResetCurrentPositionOnTrimmed)
            {
                mTrimmingSlider.CurrentPosition = 0;
            }
            mFrameListView.RightTrim = position / sender.TotalRange;
        }

        /**
         * CurrentPositionが操作された
         */
        private void OnCurrentPositionChanged(WvvTrimmingSlider sender, double position, bool finalize)
        {
            if (mPreviewing)
            {
                mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(sender.CurrentPosition);
            }
            else
            {
                mPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(sender.AbsoluteCurrentPosition);
            }
            mFrameListView.TickPosition = sender.AbsoluteCurrentPosition / sender.TotalRange;
        }

        /**
         * 再生ボタン押下時の処理
         * 
         * トリミングモードなら、プレビューモードに変えて再生を開始
         * プレビューモードなら、再生/停止をトグル
         */
        private async void OnPlay(object sender, TappedRoutedEventArgs e)
        {
            if(mComposition.Clips.Count!=1)
            {
                return;
            }

            if(IsPlaying)
            {
                mPlayer.Pause();
            }
            else
            {
                await startPreview(true);
            }
        }

        #endregion
    }
}
