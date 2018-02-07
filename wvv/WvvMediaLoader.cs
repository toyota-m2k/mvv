using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;

namespace wvv
{
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

        #region Private Fields
        private MediaPlayer mPlayer;
        private WeakReference<UIElement> mOwnerView = new WeakReference<UIElement>(null);
        private WeakReference<OnLoadedHandler> mLoaded = new WeakReference<OnLoadedHandler>(null);
        
        private UIElement OwnerView
        {
            get
            {
                UIElement v;
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
        #endregion

        #region Public API

        /**
         * コンストラクタ
         * 既存のPlayerを使うなら引数に渡す。nullなら、新規Playerを作成する
         */
        public WvvMediaLoader(MediaPlayer player=null)
        {
            if(null==player)
            {
                player = new MediaPlayer();
            }
            mPlayer = player;
        }

        /**
         * Load()メソッドに渡す完了通知ハンドラ型
         */
        public delegate void OnLoadedHandler(WvvMediaLoader sender, MediaPlayer player);

        /**
         * ソースをMediaPlayerにロードする
         */
        public void Load(MediaSource source, UIElement ownerView, OnLoadedHandler onLoaded)
        {
            Loaded = onLoaded;
            OwnerView = ownerView;
            mPlayer.MediaOpened += OnOpened;
            mPlayer.Source = source;
        }
        
        #endregion

        #region Private Methods

        /**
         * MediaOpenedイベントのハンドラ
         */
        private async void OnOpened(MediaPlayer sender, object args)
        {
            if(null!=OwnerView)
            {
                TotalRange = mPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
                VideoSize = new Size(mPlayer.PlaybackSession.NaturalVideoWidth, mPlayer.PlaybackSession.NaturalVideoHeight);
                await OwnerView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    sender.MediaOpened -= OnOpened;
                    Loaded?.Invoke(this, sender);
                    Loaded = null;
                    OwnerView = null;
                    mPlayer = null;
                });
            }
        }

        #endregion


    }
}
