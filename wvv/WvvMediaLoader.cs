using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;

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
            get
            {
                return mPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds;
            }
        }
        /**
         * 動画のサイズ（OnLoadedHandler内でのみ利用可能）
         */
        public Size VideoSize
        {
            get
            {
                return new Size(mPlayer.PlaybackSession.NaturalVideoWidth, mPlayer.PlaybackSession.NaturalVideoHeight);
            }
        }
        #endregion

        #region Private Fields
        private MediaPlayer mPlayer;
        private OnLoadedHandler mLoaded;

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
        public void Load(MediaSource source, OnLoadedHandler onLoaded)
        {
            mLoaded = onLoaded;
            mPlayer.MediaOpened += OnOpened;
            mPlayer.Source = source;
        }

        #region Private Methods

        /**
         * MediaOpenedイベントのハンドラ
         */
        private void OnOpened(MediaPlayer sender, object args)
        {
            sender.MediaOpened -= OnOpened;
            mLoaded(this, sender);
            mLoaded = null;
            mPlayer = null;
        }

        #endregion


    }
}
