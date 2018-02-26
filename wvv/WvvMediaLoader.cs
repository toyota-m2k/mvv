using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using wvv.utils;

namespace wvv
{
    /**
     * MediaPlayerへの動画読み込みと、MediaOpenedイベント発行までの待機をサポートするクラス。
     * 
     * 基本的な使い方
     * 
     * var loader = await WvvMediaLoader(player, source, view);
     * if(loader.Opened) {
     *    // 動画がオープンされた --> 次の処理
     * } else {
     *    // エラー
     * }
     */
    public class WvvMediaLoader
    {
        #region Properties

        /**
         * 動画の総再生時間（OnLoadedHandler内でのみ利用可能）
         */
        public double TotalRange
        {
            //get
            //{
            //    return mPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
            //}
            get; private set;
        }
        /**
         * 動画のサイズ（OnLoadedHandler内でのみ利用可能）
         */
        public Size VideoSize
        {
            //get
            //{
            //    return new Size(mPlayer.PlaybackSession.NaturalVideoWidth, mPlayer.PlaybackSession.NaturalVideoHeight);
            //}
            get; private set;
        }
        #endregion

        #region Private Fields & Properties

        private WeakReference<MediaPlayer> mPlayer = new WeakReference<MediaPlayer>(null);
        private WeakReference<DependencyObject> mOwnerView = new WeakReference<DependencyObject>(null);

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
        private OnLoadedHandler Loaded { get; set; } = null;
        private bool Loading { get; set; } = false;

        #endregion

        #region Public Properties

        public bool Opened { get; set; } = false;
        
        #endregion

        #region Public API

        /**
         * 非同期にソースをロードし、結果をコールバックする
         */
        public static void Load(MediaPlayer player, MediaSource source, DependencyObject ownerView, OnLoadedHandler onLoaded)
        {
            var loader = new WvvMediaLoader(player);
            loader.Load(source, ownerView, onLoaded);
        }

        /**
         * 非同期にソースをロードする
         */
        public static async Task<WvvMediaLoader> LoadAsync(MediaPlayer player, MediaSource source, DependencyObject ownerView)
        {
            var loader = new WvvMediaLoader(player);
            await loader.LoadAsync(source, ownerView);
            return loader;
        }

        /**
         * コンストラクタ
         * @param player 動画をロードするMediaPlayer
         */
        public WvvMediaLoader(MediaPlayer player)
        {
            Player = player;
            Opened = false;
        }

        /**
         * Load()メソッドに渡す完了通知ハンドラ型
         */
        public delegate void OnLoadedHandler(WvvMediaLoader loader);

        /**
         * ソースをMediaPlayerにロードする
         */
        public void Load(MediaSource source, DependencyObject ownerView, OnLoadedHandler onLoaded)
        {
            Opened = false;
            Loading = true;
            Loaded = onLoaded;
            OwnerView = ownerView;
            var player = Player;
            Player.MediaOpened += OnOpened;
            Player.MediaFailed += OnFailed;
            Player.Source = source;
        }

        /**
         * ソースをMediaPlayerにロードする(非同期版）
         */
        public Task<bool> LoadAsync(MediaSource source, DependencyObject ownerView)
        {
            CmLog.debug("WvvMediaLoader.LoadAsync: async operation started...");

            var task = new TaskCompletionSource<bool>();
            Load(source, ownerView, (loader) =>
            {
                CmLog.debug("WvvMediaLoader.LoadAsync: ... async operation finished.");
                task.TrySetResult(Opened);
            });
            return task.Task;
        }

        /**
         * 終了処理 (成功時/エラー発生時の共通処理）
         */
        private async Task terminate(MediaPlayer mediaPlayer)
        {
            CmLog.debug("WvvMediaLoader.terminate: (Loading={0})", Loading);

            await OwnerView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                CmLog.debug("WvvMediaLoader.terminate (inner): Loading={0}, hasCallback={0}", Loading, Loaded != null);
                if (Loading)
                {
                    Loading = false;
                    mediaPlayer.MediaOpened -= OnOpened;
                    mediaPlayer.MediaFailed -= OnFailed;
                    if (Opened)
                    {
                        try
                        {
                            TotalRange = Player.PlaybackSession.NaturalDuration.TotalMilliseconds;
                            VideoSize = new Size(Player.PlaybackSession.NaturalVideoWidth, Player.PlaybackSession.NaturalVideoHeight);
                            CmLog.debug("WvvMediaLoader.terminate (inner): Loaded");
                        }
                        catch (Exception e)
                        {
                            // MediaOpenedが返ってきても、その後、プロパティを参照しようとすると Shutdown済み、みたいな例外が出ることがあって、
                            // このような場合は、ステータスも Closedになっているので、オープン失敗として扱う。
                            CmLog.error(e, "WvvMediaLoader.terminate (inner): Error");
                            Opened = false;
                            mediaPlayer.Source = null;
                        }
                    }
                    else
                    {
                        mediaPlayer.Source = null;
                    }
                    Loaded?.Invoke(this);
                    Loaded = null;
                    OwnerView = null;
                }
            });
        }

        #endregion

        #region Media Player Events

        /**
         * MediaOpenedイベントのハンドラ
         */
        private async void OnOpened(MediaPlayer sender, object args)
        {
            Opened = true;
            if(null!=OwnerView)
            {
                CmLog.debug("WvvMediaLoader.OnOpened: MediaOpened");
                await terminate(sender);
            }
        }

        /**
         * MediaFailedイベントのハンドラ
         */
        private async void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            CmLog.debug("WvvMediaLoader.OnFailed: MediaFailed");
            if (null != args.ErrorMessage && args.ErrorMessage.Length > 0)
            {
                CmLog.debug(args.ErrorMessage);
            }
            if (null != args.ExtendedErrorCode)
            {
                CmLog.error(args.ExtendedErrorCode.Message);
            }

            Opened = false;
            await terminate(sender);
        }

        #endregion


    }
}
