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

            public PinPCreationInfo(MediaSource source)
            {
                this.Source = source;
                this.ID = ++sIDGenerator;
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
         * @param   source  動画ソース
         * @param   opened  IPinPPlayerを返すデリゲート
         */
        public static async Task<bool> OpenPinP(MediaSource source, NotifyPinPOpened opened)
        {
            if(null==source)
            {
                return false;
            }
            var info = new PinPCreationInfo(source);
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
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
            });
            return await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }

        #endregion

        #region Fields

        int mID;
        MediaSource mSource;
        private long mFullWindowListenerToken;

        #endregion

        #region Initialize / Terminate

        /**
         * コンストラクタ
         */
        public WvvPinPPage()
        {
            mID = 0;
            Loaded += OnLoaded;                 // xaml に定義するとメモリリークする
                                                // Unloadedイベントは来ないので定義しない。代わりに、SystemNavigationManagerPreview.CloseRequested を使う。
            this.InitializeComponent();
        }

        /**
         * このページに遷移してきた・・・まだViewはロードされていない。
         */
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var info = e.Parameter as PinPCreationInfo;
            if(null!=info)
            {
                mSource = info.Source;
                mID = info.ID;
            }

            var src = e.Parameter as MediaSource;
            if (null != src)
            {
                mSource = src;
                return;
            }

            var file = e.Parameter as StorageFile;
            if(null!=file)
            {
                mSource = MediaSource.CreateFromStorageFile(file);
                return;
            }

            var uri = e.Parameter as Uri;
            if(null!=uri)
            {
                mSource = MediaSource.CreateFromUri(uri);
                return;
            }
        }

        /**
         * ビューがロードされた
         */
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;                 // Loadedイベントハンドラはもう不要
            if(null!=mSource)
            {
                mPlayer.SetMediaPlayer(new MediaPlayer());
                mPlayer.MediaPlayer.Source = mSource;
                mFullWindowListenerToken = mPlayer.RegisterPropertyChangedCallback(MediaPlayerElement.IsFullWindowProperty, MPE_FullWindowChanged);

                mPlayer.MediaPlayer.Play();

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

                if(mID>0 && null!=sNotifyHanders)
                {
                    NotifyPinPOpened notify;
                    
                    if(sNotifyHanders.TryGetValue(mID, out notify))
                    {
                        if (null != notify)
                        {
                            notify(this);
                        }
                        sNotifyHanders.Remove(mID);
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
                        await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
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

        public async void Close()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Window.Current.Close();     // CloseRequested は呼ばれるのだろうか？
                Dispose();
            });
        }

        #endregion

    }
}
