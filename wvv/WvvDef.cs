using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace wvv
{
    #region VideoPlayer/ControllerPanel

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

    public delegate void WvvPlayerValueChanged(IWvvVideoPlayer player, double value);

    public delegate void WvvPlayerInitialized(IWvvVideoPlayer player, double totalRange, Size videoSize);

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

        ///**
        // * Videoの総再生時間
        // * MediaClipより前にPlayerが再生可能になることがあるので、PlayerからもTotalRangeが取得できるようにしておく。
        // */
        //double TotalRange { get; }

        ///**
        // * 動画の表示サイズ
        // */
        //Size VideoSize { get; }

        /**
         * VideoPlayerの状態変更通知イベント
         */
        event WvvPlayerStateChanged PlayerStateChanged;

        /**
         * VideoPlayerのサイズ変更通知イベント
         */
        event WvvPlayerValueChanged PlayerWidthChanged;

        /**
         * TotalRange / VideoSize が取得できた
         */
        event WvvPlayerInitialized PlayerInitialized;
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

    #endregion


    #region Transcoder

    /**
     * 進捗通知用デリゲート型 （Transcoderで試用）
     */
    public delegate bool IWvvProgress<SenderType>(SenderType sender, double percent);

    #endregion

    #region Cache Manager

    /**
     * @param sender    IWvvCacheオブジェクト
     * @param file      キャッシュファイル (エラーが発生していれば、null: エラー情報は、sender.Errorで取得）
     */
    public delegate void WvvDownloadedHandler(IWvvCache sender, StorageFile file);

    /**
     * キャッシュマネージャが管理するキャッシュクラスのi/f定義
     */
    public interface IWvvCache
    {
        /**
         * キャッシュファイルを取得する。
         * @param callback  結果を返すコールバック
         */
        void GetFile(WvvDownloadedHandler callback);
        /**
         * キャッシュファイルを取得する。（非同期版）
         * 
         * @return キャッシュファイル (エラーが発生していれば、null: エラー情報は、Errorプロパティで取得）
         */
        Task<StorageFile> GetFileAsync();

        /**
         * エラー情報
         */
        Exception Error { get; }

        /**
         * キャッシュを解放する（CacheManagerによって削除可能な状態にする）
         */
        void Release();

        /**
         * キャッシュを無効化する
         */
        void Invalidate();
    }

    #endregion

}
