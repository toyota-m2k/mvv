using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;

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
        private WeakReference<OnLoadedHandler> mLoaded = new WeakReference<OnLoadedHandler>(null);

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
        private OnLoadedHandler Loaded
        {
            get
            {
                OnLoadedHandler v;
                return mLoaded.TryGetTarget(out v) ? v : null;
            }
            set
            {
                mLoaded.SetTarget(value);
            }
        }
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
         * ソースをロードする
         */
        public Task<bool> LoadAsync(MediaSource source, DependencyObject ownerView)
        {
            Debug.WriteLine("LoadAsync: async operation started.");
            return Task.Run<bool>( async () => {
                var player = Player;
                if(null==player)
                {
                    return false;
                }
                using (var ev = new ManualResetEvent(false))
                {
                    await ownerView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        Load(source, ownerView, (s) =>
                        {
                            ev.Set();
                        });
                    });
                    try
                    {
                        ev.WaitOne();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                        await terminate(player);
                        //return false;
                    }
                }
                Debug.WriteLine("LoadAsync: async operation finished.");
                return Opened;
            });
        }

        /**
         * 終了処理 (成功時/エラー発生時の共通処理）
         */
        private async Task terminate(MediaPlayer mediaPlayer)
        {
            await OwnerView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
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
                        }
                        catch (Exception e)
                        {
                            // MediaOpenedが返ってきても、その後、プロパティを参照しようとすると Shutdown済み、みたいな例外が出ることがあって、
                            // このような場合は、ステータスも Closedになっているので、オープン失敗として扱う。
                            Debug.WriteLine(e);
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
                Debug.WriteLine("Loader(Opened): player={0}, session={1}", Player.CurrentState.ToString(), Player.PlaybackSession.PlaybackState.ToString());
                await terminate(sender);
            }
        }

        /**
         * MediaFailedイベントのハンドラ
         */
        private async void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine(args.ErrorMessage);
            Opened = false;
            await terminate(sender);
        }



        #endregion


    }
}
