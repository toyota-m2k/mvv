using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace wvv
{
    public interface IPinPPlayer
    {
        void Close();
    }

    public delegate void NotifyPinPOpened(IPinPPlayer pinp);


    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class WvvPinPPage : Page, IPinPPlayer
    {
        #region Class Methods

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

            public PinPCreationInfo(MediaSource source, double startAt, Size? reqSize)
            {
                this.Source = source;
                this.ID = ++sIDGenerator;
                this.StartAt = startAt;
                this.ReqSize = reqSize;
            }
        }

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
        public static async Task<bool> OpenPinP(MediaSource source, double position=-1, Size? reqSize=null, NotifyPinPOpened opened=null)
        {
            if(null==source)
            {
                return false;
            }
            var info = new PinPCreationInfo(source, position, reqSize);
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

        private static ViewModePreferences getCompactOverlayOption(Size? reqSize)
        {
            var option = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
            if (null != reqSize)
            {
                // ToDo: 500 x 384 のサイズに制限する
                option.CustomSize = reqSize.Value;
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
            if(null!=mInfo && null!=mInfo.Source)
            {
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
                //
                SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;

                if(mInfo.ID>0 && null!=sNotifyHanders)
                {
                    NotifyPinPOpened notify;
                    
                    if(sNotifyHanders.TryGetValue(mInfo.ID, out notify))
                    {
                        if (null != notify)
                        {
                            notify(this);
                        }
                        sNotifyHanders.Remove(mInfo.ID);
                    }
                }
            }
        }

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
                SystemNavigationManagerPreview.GetForCurrentView().CloseRequested -= OnCloseRequested;
            }
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


        /**
         * ×ボタンがクリックされた
         */
        private void OnCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            Dispose();
        }

        /**
         */
        public async void Close()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Window.Current.Close();     // CloseRequested は呼ばれるのだろうか？ --> 呼ばれなかったので、ここで呼ぶ。（万一、複数回呼び出しても安全なはず）
                Dispose();
            });
        }

        #endregion

    }
}
