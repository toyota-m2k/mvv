using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wvv
{
    /**
     * VideoPlayerの状態
     */
    public enum PlayerState
    {
        NONE,           // unloaded
        PAUSED,         // loaded and not-playing
        PLAYING,        // playing and loaded, of cource.
    }
    /**
     * VideoPlayerの状態変更通知イベントの型
     */
    public delegate void WvvPlayerStateChanged(IWvvVideoPlayer player, PlayerState state);

    public delegate void WvvPlayerWidthChanged(IWvvVideoPlayer player, double width);
    /**
     * VideoPlayerのi/f定義
     */
    public interface IWvvVideoPlayer
    {
        /**
         * シーク
         */
        double SeekPosition { get; set; }

        /**
         * 動画の再生/停止
         */
        bool IsPlaying { get; set; }

        /**
         * VideoPlayerの状態
         */
        PlayerState PlayerState { get; }

        /**
         * FullScreen / 通常モードの取得・切り替え
         */
        bool FullScreen { get; set; }

        /**
         * Playerビューの幅
         * ControlPanelは、この幅に合わせて伸縮する。
         */
        double PlayerWidth { get; }

        /**
         * Videoの総再生時間
         * MediaClipから取り出した値と、MediaPlaybackSessionから取り出した値が異なるようなので、Playerからもらうことにする。
         * ... と思ったが気のせいかも。
         */
        //double TotalRange { get; }

        /**
         * VideoPlayerの状態変更通知イベント
         */
        event WvvPlayerStateChanged PlayerStateChanged;

        /**
         * VideoPlayerのサイズ変更通知イベント
         */
        event WvvPlayerWidthChanged PlayerWidthChanged;
    }

    /**
     * マーク変更通知イベントの型
     */
    public delegate void WvvMarkerEvent(IWvvVideoControlPanel sender, double position, object requester);

    /**
     * VideoControlPanel の i/f 定義
     */
    public interface IWvvVideoControlPanel
    {
        /**
         * マーカーの追加/削除の通知イベント
         * 
         * マーカーの追加・削除は、WvvMoviePlayer/WvvMarkerViewのUI操作によって実行される場合と、
         * 外部から WvvMoviePlayer.AddMarker/RemoveMarker()を呼び出すことによって実行される場合がありえる。
         * これらを区別するためには、requester 引数を利用する。
         * 内部から呼び出された場合は、requesterに、WvvMoviePlayerまたはWvvMarkerViewのインスタンスがセットされる。
         */
        event WvvMarkerEvent MarkerAdded;
        event WvvMarkerEvent MarkerRemoved;
    }


    /**
     * 進捗通知用デリゲート型 （Transcoderで試用）
     */
    public delegate bool IWvvProgress<SenderType>(SenderType sender, double percent);
}
