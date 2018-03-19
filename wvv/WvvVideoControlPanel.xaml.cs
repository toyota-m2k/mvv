using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using wvv.utils;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    /**
     * PinP Player のオーナーが実装すべき i/f
     */
    public interface IPinPPlayerOwner
    {
        /**
         * PinP Player を開いてよいか？
         * @return key (PinP_Openedに渡す識別子）... nullならPinP Playerは開かない
         */
        object PinP_QueryOpenKey();
        /**
         * PinP Player を開いた。
         * PinPOwner側で閉じたいときは、player.Close()を呼び出す。
         */
        void PinP_Opened(object key, IPinPPlayer player);
        /**
         * PinP Playerを閉じた
         */
        void PinP_Closed(object key, IPinPPlayer player);
    }



    /**
     * VideoControlPanelクラス
     */
    public sealed partial class WvvVideoControlPanel : UserControl, INotifyPropertyChanged, IWvvMarkerEditor, IDisposable
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        #region Initialization / Termination

        /**
         * コンストラクタ
         */
        public WvvVideoControlPanel()
        {
            TrackingTimer = new DispatcherTimer();
            TrackingTimer.Interval = TimeSpan.FromMilliseconds(10);
            TrackingTimer.Tick += updatePlayingPosition;

            mExtractor = new WvvFrameExtractor2(ThumbnailHeight, ThumbnailCount);

            this.DataContext = this;
            this.InitializeComponent();
        }

        private void updatePlayingPosition(object s, object e)
        {
            if (null != Player)
            {
                double pos = Player.SeekPosition;
                mSlider.Value = pos;
                mFrameListView.TickPosition = pos / TotalRange;
                updatePositionString();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // mExtractor.Cancel(); ... Reset()でやる
            // TrackingTimer.Stop();
            Reset();
        }

        #endregion

        #region Bindings

        /**
         * フレームサムネイルの作成と、MediaPlayerへのファイルロードがともに完了したらTrueになる。
         */
        public bool MoviePrepared
        {
            get { return PlayerState != PlayerState.NONE; }
        }

        public bool IsPlaying
        {
            get
            {
                return mPauseTemporary || (Player?.IsPlaying ?? false);
            }
        }
        /**
         * フレームサムネイルのロードが完了しているか？
         */
        private bool mFramesLoaded = false;
        private bool FramesLoaded
        {
            get
            {
                return mFramesLoaded;
            }
            set
            {
                if(mFramesLoaded != value)
                {
                    mFramesLoaded = value;
                    notify("MoviePrepared");
                }
            }
        }

        /**
         * 動画の総再生時間
         */
        private double mTotalRange = 100;
        public double TotalRange
        {
            get { return mTotalRange; }
            private set
            {
                if (mTotalRange != value)
                {
                    mTotalRange = value;
                    notify("TotalRange");
                    notify("SmallChange");
                    notify("LargeChange");
                    updatePositionString();
                }
                mMarkerView.TotalRange = value;
            }
        }

        /**
         * パネルの幅
         * HorizontalAlignment = Stretch でレイアウトする場合は設定しないので、バインドせず、必要に応じて直接プロパティを変更することにする。
         */
        public double PanelWidth
        {
            get => mVideoControlPanelContainer.Width;
            set
            {
                mVideoControlPanelContainer.Width = value;
                if (value == double.NaN)
                {
                    mVideoControlPanelContainer.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                else
                {
                    mVideoControlPanelContainer.HorizontalAlignment = HorizontalAlignment.Left;
                }
            }
        }

        /**
         * フレーム一覧を表示する(true)か、しない(false)か
         */
        public bool mShowingFrames = false;
        public bool ShowingFrames
        {
            get { return mShowingFrames; }
            set
            {
                if (mShowingFrames != value)
                {
                    mShowingFrames = value;
                    notify("ShowingFrames");
                    notify("ThumbMode");
                }
            }
        }

        /**
         * フレームサムネイルの大小
         */
        private bool mLargeThumbnail = false;
        public bool LargeThumbnail
        {
            get { return mLargeThumbnail; }
            set
            {
                if (mLargeThumbnail != value)
                {
                    mLargeThumbnail = value;
                    notify("LargeThumbnail");
                    notify("ThumbMode");
                    makeThumbnails();
                }
            }
        }

        /**
         * Slider Thumbのモード
         * ShowingFramesとLargeThumbnailを組み合わせてバインドできればよいのだが、
         * DataTriggerBehaviorで、これを実現する方法がわからないので、１つのプロパティにしておく。
         */
        public string ThumbMode
        {
            get
            {
                if (!ShowingFrames)
                {
                    return "MIN";
                }
                else
                {
                    if (LargeThumbnail)
                    {
                        return "MAX";
                    }
                    else
                    {
                        return "NOR";
                    }
                }
            }
        }

        /**
         * サムネイルの高さ
         * スライダーのThumb（画像）の高さで決まる値・・・即値
         */
        private int ThumbnailHeight
        {
            get
            {
                return (mLargeThumbnail) ? 63 : 44;
            }
        }

        /**
         * フレームリストに表示するサムネイル数（フレーム数）
         */
        private int ThumbnailCount = 20;

        /**
         * 矢印キーによるスライダーの移動量
         */
        public double SmallChange
        {
            get { return mTotalRange / 100; }
        }

        /**
         * スライダーの移動量（大きいやつ・・・操作方法は知らない）
         */
        public double LargeChange
        {
            get { return mTotalRange / 20; }
        }

        /**
         * フレームサムネイルの下に表示する文字列
         */
        private string mPositionString = "";
        public string PositionString
        {
            get { return mPositionString; }
        }
        #endregion

        #region Public API

        /**
         * VideoPlayer オブジェクトの参照
         * 
         * イベントハンドラの登録を解除する必要があるので、勝手にnullにされないよう、
         * WeadReferenceにはしていない。利用者側で nullをセットするか、Dispose()を呼び出して解除すること。。
         */
        private IWvvVideoPlayer mPlayer = null;
        public IWvvVideoPlayer Player
        {
            get
            {
                return mPlayer;
            }
            set
            {
                if(mPlayer == value)
                {
                    return;
                }
                if(null!=mPlayer)
                {
                    mPlayer.PlayerStateChanged -= OnPlayerStateChanged;
                    mPlayer.PlayerInitialized -= OnPlayerInitialized;
                    //mPlayer.PlayerWidthChanged -= OnPlayerWidthChanged;
                }
                mPlayer = value;
                if(null!=mPlayer)
                {
                    mPlayer.PlayerStateChanged += OnPlayerStateChanged;
                    mPlayer.PlayerInitialized += OnPlayerInitialized;
                    //mPlayer.PlayerWidthChanged += OnPlayerWidthChanged;
                }
            }
        }

        /**
         * IWvvCacheオブジェクトを動画ソースとしてセット。
         * （AddRefして保持する。）
         */
        public void SetSource(IWvvCache source)
        {
            Reset();
            if(null==source)
            {
                return;
            }

            source.AddRef();
            mCache = source;
            
            mCache.GetFile( async (s, file) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (s == mCache)
                    {
                        SetSourceInternal(file);
                    }
                });
            });
        }

        /**
         * 動画ソースとしてURIをセットする。
         */
        public async void SetSource(Uri uri)
        {
            Reset();
            if (null == uri)
            {
                return;
            }

            await WvvCacheManager.InitializeAsync();
            var cache = await WvvCacheManager.Instance.GetCacheAsync(uri);
            if (null == cache)
            {
                CmLog.error("WvvVideoControlPanel: WvvCacheManager GetCacheAsync Error.");
                return;
            }
            SetSource(cache);
            cache.Release();
        }

        /**
         * StorageFileを動画ソースとしてセットする
         */
        public void SetSource(StorageFile source)
        {
            Reset();
            if (null == source)
            {
                return;
            }

            SetSourceInternal(source);
        }

        /**
         * 動画ソースをセットする
         * （内部でResetしない）
         */
        private void SetSourceInternal(StorageFile source)
        {
            mSource = source;
            makeThumbnails();
        }

        public void Reset()
        {
            if (null != mCache)
            {
                mCache.Release();
                mCache = null;
            }
            mSource = null;
            mMarkerView.Clear();
            mSlider.Value = 0;

            TrackingTimer.Stop();
            FramesLoaded = false;
            mFrameListView.Reset();
            mExtractor.Cancel();
        }

        /**
         * クリーンアップ
         */
        public void Dispose()
        {
            TrackingTimer.Stop();
            TrackingTimer.Tick -= updatePlayingPosition;
            Player = null;
            mSource = null;
            mMarkerView.Clear();
            mFrameListView.Frames.Clear();
            if(null!=mCache)
            {
                mCache.Release();
                mCache = null;
            }

            //if (null != mPinPPlayer)
            //{
            //    mPinPNow = false;
            //    mPinPPlayer.Closed -= OnPinPPlayerClosed;
            //    mPinPPlayer.Close();
            //}
        }

        #endregion

        #region Privates

        private WvvFrameExtractor2 mExtractor = null;
        private IWvvCache mCache = null;
        private StorageFile mSource = null;

        private PlayerState PlayerState
        {
            get
            {
                return Player?.PlayerState ?? PlayerState.NONE;
            }
        }


        /**
         * サムネイルを作成する
         */
        private async void makeThumbnails()
        {
            if(null==mSource)
            {
                return;
            }

            mExtractor.ThumbnailHeight = ThumbnailHeight;
            mExtractor.FrameCount = ThumbnailCount;
            mFrameListView.Frames.Clear();
            if(!ShowingFrames)
            {
                return;
            }

            try
            {
                MediaClip clip = await MediaClip.CreateFromFileAsync(mSource);
                TotalRange = clip.OriginalDuration.TotalMilliseconds;
                await mExtractor.ExtractAsync(clip, (s, i, img) =>
                {
                   mFrameListView.Frames[i] = img;
                },
                (sender, blank) =>
                {
                    mFrameListView.FrameListHeight = mExtractor.ThumbnailHeight;
                    for (int i = 0; i < ThumbnailCount; i++)
                    {
                        mFrameListView.Frames.Add(blank);
                    }
                });
            }
            catch (Exception ex)
            {
                if(null!=mCache)
                {
                    mCache.Invalidate();
                }
                TotalRange = 1000;
                CmLog.error(ex, "WvvVideoControlPanel.makeThumbnails: Error");
            }
            finally
            {
                FramesLoaded = true;
            }
        }

        /**
         * PositionStringを更新する。
         * @param   pos     スライダーの位置（＝再生位置）
         */
        private void updatePositionString(double pos)
        {
            string v = "";
            TimeSpan total = TimeSpan.FromMilliseconds(TotalRange);
            TimeSpan current = TimeSpan.FromMilliseconds(pos);
            if (total.Hours > 0)
            {
                v = String.Format("{0:00}:{1:00}:{2:00} / {3:00}:{4:00}:{5:00}", current.Hours, current.Minutes, current.Seconds, total.Hours, total.Minutes, total.Seconds);
            }
            else if (total.Minutes > 0)
            {
                v = String.Format("{0:00}:{1:00} / {2:00}:{3:00}", current.Minutes, current.Seconds, total.Minutes, total.Seconds);
            }
            else
            {
                v = String.Format("{0:00}:{1:00} / {2:00}:{3:00}", current.Seconds, (int)Math.Round((double)current.Milliseconds / 10), total.Seconds, (int)Math.Round((double)total.Milliseconds / 10));
            }

            if (v != mPositionString)
            {
                mPositionString = v;
                notify("PositionString");
            }
        }

        /**
         * 現在の再生位置で、PositionStringを更新する。
         */
        private void updatePositionString()
        {
            double? pos = Player?.SeekPosition;
            if (null != pos)
            {
                updatePositionString(pos.Value);
                return;
            }
            else if (mPositionString.Length != 0)
            {
                mPositionString = "";
                notify("PositionString");
            }
        }

        // 再生中のトラッキングサムを移動させるためのタイマー
        private DispatcherTimer TrackingTimer { get; set; }

        #endregion

        #region IWvvMarkerEditor i/f

        /**
         * マーカーの追加/削除の通知イベント
         * 
         * マーカーの追加・削除は、WvvMoviePlayer/WvvMarkerViewのUI操作によって実行される場合と、
         * 外部から WvvMoviePlayer.AddMarker/RemoveMarker()を呼び出すことによって実行される場合がありえる。
         * これらを区別するためには、requester 引数を利用する。
         * 内部から呼び出された場合は、requesterに、WvvMoviePlayerまたはWvvMarkerViewのインスタンスがセットされる。
         */
        public event WvvMarkerEvent MarkerAdded;
        public event WvvMarkerEvent MarkerRemoved;

        public void AddMarker(double position, object requester)
        {
            mMarkerView.AddMarker(position, requester);
        }

        public void RemoveMarker(double position, object requester)
        {
            mMarkerView.RemoveMarker(position, requester);
        }

        public void SetMarkers(IEnumerable<double> markers)
        {
            mMarkerView.SetMarkers(markers);
        }

        #endregion

        #region Event Handlers

        private void OnPlayerStateChanged(IWvvVideoPlayer player, PlayerState state)
        {
            notify("MoviePrepared");

            switch(state)
            {
                case PlayerState.PLAYING:
                    TrackingTimer.Start();
                    break;
                case PlayerState.PAUSED:
                    TrackingTimer.Stop();
                    // 最後の更新が行われないことがあるのではないかと思って、Stop後にupdateを呼ぶようにしてみたが、あまり効果はなさそうだ。
                    // updatePlayingPosition(null, null);
                    break;
                case PlayerState.NONE:
                default:
                    TrackingTimer.Stop();
                    break;
            }
            notify("IsPlaying");
        }

        private void OnPlayerInitialized(IWvvVideoPlayer player, double totalRange, Size videoSize)
        {
            TotalRange = totalRange;
        }


        //private void OnPlayerWidthChanged(IWvvVideoPlayer player, double width)
        //{
        //    PanelWidth = width;
        //}


        /**
         * タップイベントを親(MMJScrollViewer)に回さないためのストッパー
         */
        private void OnContainerTapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        /**
         * マーカー選択通知
         */
        private void MV_MarkerSelected(double value, object clientData)
        {
            mSlider.Value = value;
            if (Player.IsPlaying)
            {
                Player.SeekPosition = value;
            }
        }

        /**
         * マーカー削除通知
         */
        private void MV_MarkerRemoved(double value, object clientData)
        {
            MarkerRemoved?.Invoke(this, value, clientData);
        }

        /**
         * マーカー追加通知
         */
        private void MV_MarkerAdded(double value, object clientData)
        {
            MarkerAdded?.Invoke(this, value, clientData);
        }

        /**
         * 再生/停止ボタン
         */
        private void OnButtonPlayStop(object sender, TappedRoutedEventArgs e)
        {
            if(MoviePrepared)
            {
                Player.IsPlaying = !Player.IsPlaying;
            }
            e.Handled = true;
        }

        /**
         * 前のマーカー位置へシーク
         */
        private void OnPrevMarker(object sender, TappedRoutedEventArgs e)
        {
            mMarkerView.PrevMark(mSlider.Value, mMarkerView);
            e.Handled = true;
        }

        /**
         * 次のマーカー位置へシーク
         */
        private void OnNextMarker(object sender, TappedRoutedEventArgs e)
        {
            mMarkerView.NextMark(mSlider.Value, mMarkerView);
            e.Handled = true;
        }

        /**
         * マーカー追加ボタンクリック
         */
        private void OnAddMarker(object sender, TappedRoutedEventArgs e)
        {
            mMarkerView.AddMarker(Player.SeekPosition, mMarkerView);
            e.Handled = true;
        }

        /**
         * フルスクリーンモード切替ボタン
         */
        private void OnFullScreen(object sender, TappedRoutedEventArgs e)
        {
            if(MoviePrepared)
            {
                Player.FullScreen = true;
            }
            e.Handled = true;
        }

        private WeakReference<IPinPPlayerOwner> mPinPOwner;
        public IPinPPlayerOwner PinPOwner
        {
            get
            {
                return mPinPOwner?.GetTarget();
            }
            set
            {
                if (null == value)
                {
                    mPinPOwner = null;
                }
                else
                {
                    mPinPOwner = new WeakReference<IPinPPlayerOwner>(value);
                }
            }

        }

        //private bool mPinPNow = false;
        //private IPinPPlayer mPinPPlayer = null;
        
        private class OwnerKey
        {
            public IPinPPlayerOwner Owner { get; private set; }
            public object Key { get; private set; }

            public OwnerKey(IPinPPlayerOwner owner, object key)
            {
                Owner = owner;
                Key = key;
            }
        }

        /**
         * PinPモード切替ボタン
         */
        private async void OnPInP(object sender, TappedRoutedEventArgs e)
        {
            if(null==mSource)
            {
                return;
            }
            object key = null;
            var owner = PinPOwner;
            if(null!=owner)
            {

                key = owner.PinP_QueryOpenKey();
                if (null == key)
                {
                    return;
                }
            }
            await WvvPinPPage.OpenPinP(MediaSource.CreateFromStorageFile(mSource), mSlider.Value, null, (pinp, clientData) =>
            {
                pinp.Closed += OnPinPPlayerClosed;
                var ok = clientData as OwnerKey;
                if (null!=ok && null!=ok.Owner)
            { 
                    ok.Owner.PinP_Opened(ok.Key, pinp);
            }
            }, new OwnerKey(owner, key));
        }

        private void OnPinPPlayerClosed(IPinPPlayer player, object clientData)
        {
            player.Closed -= OnPinPPlayerClosed;
            var ok = clientData as OwnerKey;
            if (null != ok && null!=ok.Owner)
            {
                ok.Owner.PinP_Closed(ok.Key, player);
            }
        }

        private async void OnShowHideFrameList(object sender, TappedRoutedEventArgs e)
        {
            if (!ShowingFrames)
            {
                ShowingFrames = true;
                mFrameListView.EnsureInitScrollViewer();
                if(mFrameListView.Frames.Count==0)
                {
                    makeThumbnails();
                }
            }
            else
            {
                ShowingFrames = false;
            }

            //if (!ShowingFrames)
            //{
            //    ShowingFrames = true;
            //}
            //else
            //{
            //    if (LargeThumbnail)
            //    {
            //        LargeThumbnail = false;
            //        ShowingFrames = false;
            //    }
            //    else
            //    {
            //        LargeThumbnail = true;
            //    }
            //}
            e.Handled = true;
        }

        /**
         * スライダーのトラッカー操作
         */
        private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if(null==Player || Player.IsPlaying)
            {
                return;
            }

            var slider = sender as Slider;

            if (null!=slider && MoviePrepared)
            {
                Player.SeekPosition = slider.Value;
                if (!Player.IsPlaying)
                {
                    mFrameListView.TickPosition = slider.Value / TotalRange;
                    updatePositionString(slider.Value);
                }
            }
        }

        /**
         * ホイールによるスライダーの操作にも対応しておこう。
         */
        private void OnSliderWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var elem = sender as UIElement;
            if (null != elem)
            {
                int delta = e.GetCurrentPoint(elem).Properties.MouseWheelDelta;
                if (0 != delta)
                {
                    //Debug.WriteLine("Pointer: WheelChanged", delta);
                    delta = (delta > 0) ? 1 : -1;
                    double v = mSlider.Value + delta * mSlider.SmallChange;
                    if (v < mSlider.Minimum)
                    {
                        v = mSlider.Minimum;
                    }
                    else if (v > mSlider.Maximum)
                    {
                        v = mSlider.Maximum;
                    }
                    mSlider.Value = v;
                }
            }
            e.Handled = true;
        }

        bool mPauseTemporary = false;

        /**
         * スライダー操作中に動画の再生を一時的に停止する
         */
        private void pauseOnStartTracking()
        {
            mPauseTemporary = false;
            if (MoviePrepared)
            {
                mPauseTemporary = Player.IsPlaying;
                if (mPauseTemporary)
                {
                    Player.IsPlaying = false;
                }
            }
        }

        /**
         * スライダー操作が終わったときに（必要なら）動画の再生を再開する。
         */
        private void restartOnEndTracking()
        {
            if (MoviePrepared)
            {
                if (mPauseTemporary)
                {
                    Player.IsPlaying = true;
                }
            }
            mPauseTemporary = false;
        }

        /**
         * スライダーのThumbのドラッグが開始された
         */
        private void OnSliderDragStarted(object sender, DragStartedEventArgs e)
        {
            //Debug.WriteLine("Slider: DragStarted");
            pauseOnStartTracking();
        }

        /**
         * スライダーのThumbのドラッグが終わった
         */
        private void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
        {
            //Debug.WriteLine("Slider: DragCompleted");
            restartOnEndTracking();
        }

        /**
         * スライダーのThumb以外の部分がクリックされた
         */
        private void OnSliderPointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //Debug.WriteLine("Slider: Pressed");
            pauseOnStartTracking();
            e.Handled = true;
        }

        /**
         * スライダーのThumb以外の部分で、マウス/タッチがリリースされた。
         * 実際には、Thumb上でのReleasedでも呼ばれるみたい。この場合、Pressedは呼ばれず、Releasedだけが呼ばれる・・・なんか気持ち悪い。
         */
        private void OnSliderPointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //Debug.WriteLine("Slider: Released");
            restartOnEndTracking();
            e.Handled = true;
        }

        /**
         * 上のストッパーをいれると、Sliderが（Thumbボタンの外の）タップイベントを処理できなくなるので、自前で処理する。
         */
        private void OnSliderTapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Slider)
            {
                var pos = e.GetPosition(mSlider);
                if (pos.Y < 12 || !ShowingFrames)
                {
                    // スライダー部分のタップ
                    mSlider.Value = mSlider.Maximum * (double)pos.X / (double)mSlider.ActualWidth;
                }
                else
                {
                    // フレームサムネイル部分のタップ
                    var v = mFrameListView.GetTappedPosition(e);
                    mSlider.Value = TotalRange * v;
                }
                e.Handled = true;
            }
        }

        #endregion

    }
}
