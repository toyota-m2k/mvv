﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using wvv.utils;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace wvv
{
    /**
     * PinPウィンドウが閉じたときの通知イベントのハンドラ型
     */
    public delegate void ClosedEventHandler(IPinPPlayer player, object clientData);

    /**
     * PinPPlayerのi/f（上のイベントハンドラで使う）
     */
    public interface IPinPPlayer
    {
        void BringUp();
        void Close();
        event ClosedEventHandler Closed;
    }

    public delegate void NotifyPinPOpened(IPinPPlayer pinp, object clientData);

    /**
     * PinP風のポップアップ動画再生画面
     * 
     * Usage
     * 
     * if(WvvPinPPage.IsSupported) {
     *      await WvvPinPPage.OpenPinP(source, -1, null, (pinp)=>{
     *          pinp.Closed += {
     *              // PinP画面が閉じられた時の処理
     *          }
     *      }, null);
     * }
     */
    public sealed partial class WvvPinPPage : Page, IPinPPlayer
    {
        #region Public Methods

        /**
         * PinP モード（ApplicationViewMode.CompactOverlay)をサポートしているか？
         * （デバイスによってサポートしていないものがあるらしい）
         */
        public static bool IsSupported
        {
            get
            {
                return ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay);
            }
        }

        /**
         * PinPPageを開く
         * @param   source      動画ソース
         * @param   position    動画を再生する位置（-1なら停止した状態で開く）
         * @param   reqSize     プレーヤーのサイズ（参考値・・・この通りになるとは限らない）/ nullなら、サイズは成り行き任せ
         * @param   opened      IPinPPlayerを返すデリゲート （通知不要ならnull）
         */
        public static async Task<bool> OpenPinP(MediaSource source, double position=-1, Size? reqSize=null, NotifyPinPOpened opened=null, object clientData=null)
        {
            if(null==source)
            {
                return false;
            }
            var info = new PinPCreationInfo(source, position, reqSize, clientData);
            if(null!=opened)
            {
                if(null== sNotifyHanders)
                {
                    sNotifyHanders = new Dictionary<int, NotifyPinPOpened>();
                }
                sNotifyHanders.Add(info.ID, opened);
            }

            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(WvvPinPPage), info);
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
                var option = getCompactOverlayOption(reqSize);
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, option);
            });
            return await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }

        /**
         * ×ボタンがクリックされた
         * この方法は、Restricted Capabilities の指定が必要なので、できれば避けたい。
         */
        //private void OnCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        //{
        //    Dispose();
        //}
        private int mCurrentViewId = 0;
        public async void BringUp()
        {
            if (null != mInfo )
            {
                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(mCurrentViewId);
            }
        }


        /**
         * PinP Playerを閉じる (IPinPPlayerの唯一のメソッド）
         */
        public async void Close()
        {
            if (null != mInfo)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Window.Current.Close();     // CloseRequested は呼ばれるのだろうか？ --> 呼ばれなかったので、ここで呼ぶ。（万一、複数回呼び出しても安全なはず）
                    Closed?.Invoke(this, mInfo.ClientData);
                    Dispose();
                });
            }
        }

        /**
         * PinP Playerが閉じたときのイベント
         */
        public event ClosedEventHandler Closed;

        #endregion

        #region Privates

        /**
         * これから開くPinPPage と呼び出し元を紐づけるための小さな仕掛け
         */
        private static Dictionary<int, NotifyPinPOpened> sNotifyHanders = null;

        /**
         * 開くPinPPageに渡す情報
         */
        private class PinPCreationInfo
        {
            private static int sIDGenerator = 0;

            public int ID { get; private set; }
            public MediaSource Source { get; private set; }
            public double StartAt { get; private set; }
            public Size? ReqSize { get; private set; }
            public object ClientData { get; private set; }

            public PinPCreationInfo(MediaSource source, double startAt, Size? reqSize, object clientData)
            {
                this.Source = source;
                this.ID = ++sIDGenerator;
                this.StartAt = startAt;
                this.ReqSize = reqSize;
                this.ClientData = clientData;
            }
        }


        private static ViewModePreferences getCompactOverlayOption(Size? reqSize)
        {
            var option = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
            if (null != reqSize)
            {
                // 500 x 384 のサイズに制限する
                var size = reqSize.Value;
                size.Width = Math.Min(500, size.Width);
                size.Height = Math.Min(384, size.Height);
                option.CustomSize = size;
            }
            return option;
        }

        #endregion

        #region Fields

        private PinPCreationInfo mInfo;
        private long mFullWindowListenerToken;

        #endregion

        #region Initialize / Terminate

        /**
         * コンストラクタ
         * 
         * 意味的には private だが、Frame#Navigate()で開くために、publicでなければならない。
         */
        public WvvPinPPage()
        {
            mInfo = null;
            Loaded += OnLoaded;                 // xaml に定義するとメモリリークする
                                                // Unloadedイベントは来ないので定義しない。代わりに、SystemNavigationManagerPreview.CloseRequested を使う。
            this.InitializeComponent();
        }

        /**
         * このページに遷移してきた・・・まだViewはロードされていない。
         */
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            mInfo = e.Parameter as PinPCreationInfo;
        }

        /**
         * ビューがロードされた
         */
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;                 // Loadedイベントハンドラはもう不要

            if (null!=mInfo && null!=mInfo.Source)
            {
                mCurrentViewId = ApplicationView.GetForCurrentView().Id;
                ApplicationView.GetForCurrentView().Consolidated += OnConsolidated;

                mPlayer.SetMediaPlayer(new MediaPlayer());
                mPlayer.MediaPlayer.Source = mInfo.Source;
                mFullWindowListenerToken = mPlayer.RegisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, MPE_FullWindowChanged);

                if(mInfo.StartAt>=0) {
                    mPlayer.MediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(mInfo.StartAt);
                    mPlayer.MediaPlayer.Play();
                }


                // ×ボタンの監視
                //
                // ×ボタンで閉じると（他の閉じ方はないと思う）ClosedとかUnloadedなどのイベントが来ない。
                // 代わりに、Windows 10 Creators Update で追加された、SystemNavigationManagerPreview.CloseRequestedイベントでリソース解放を行う。
                //
                // manifest に、  以下の記述を追加すること。（これをやらないと、イベントが発行されない）
                //  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
                //  ...
                //  <Capabilities>
                //      < rescap:Capability Name = "confirmAppClose" />
                //  </ Capabilities >
                //　↑ Restricted Capabilities を指定するのは避けたい。
                //SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;

                if(mInfo.ID>0 && null!=sNotifyHanders)
                {
                    NotifyPinPOpened notify;
                    
                    if(sNotifyHanders.TryGetValue(mInfo.ID, out notify))
                    {
                        if (null != notify)
                        {
                            notify(this, mInfo.ClientData);
                        }
                        sNotifyHanders.Remove(mInfo.ID);
                    }
                }
            }
        }

        /**
         * SubWindowの×ボタンがクリックされたときに呼び出される。
         */
        private void OnConsolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            CmLog.debug("WvvPinPPage.OnConsolidated: PinP Player Closed.)");
            ApplicationView.GetForCurrentView().Consolidated -= OnConsolidated;
            Close();
        }

        //private void OnUnloaded(object sender, RoutedEventArgs e)
        //{
        //    Debug.WriteLine("MediaPage Unloaded.");
        //    Dispose();
        //}

        /**
         * リソース解放
         */
        private void Dispose()
        {
            var player = mPlayer.MediaPlayer;
            if (null != player)
            {
                mPlayer.UnregisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, mFullWindowListenerToken);
                mPlayer.SetMediaPlayer(null);
                player.Dispose();
                //この方法は、Restricted Capabilities の指定が必要なので、できれば避けたい。
                //SystemNavigationManagerPreview.GetForCurrentView().CloseRequested -= OnCloseRequested;
            }
            Closed = null;
            mInfo = null;


            CmLog.debug("WvvPinPPage.Dispose: PinP Player Disposed.)");
        }

        #endregion

        #region Event Handlers

        /**
         * PinPのMediaPlayer を全画面に変更してから元に戻すと、MainWindowと同じサイズになってしまう。
         * これを回避するため、戻ってきたタイミングで、元のサイズに戻す。
         */
        private async void MPE_FullWindowChanged(DependencyObject sender, DependencyProperty dp)
        {
            var mpe = sender as MediaPlayerElement;
            if (null != mpe)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    if (!mpe.IsFullWindow)
                    {
                        var option = getCompactOverlayOption(mInfo.ReqSize);
                        await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, option);
                    }
                });
            }
        }

        #endregion

    }
}
