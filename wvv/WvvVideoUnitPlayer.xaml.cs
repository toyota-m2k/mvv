using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    public sealed partial class WvvVideoUnitPlayer : UserControl, IDisposable
    {
        public WvvVideoUnitPlayer()
        {
            this.InitializeComponent();
        }

        public void Dispose()
        {
            mPanel.Dispose();
            mPlayer.Dispose();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mPanel.Player = mPlayer;
        }

        private void UnLoaded(object sender, RoutedEventArgs e)
        {
            mPanel.Player = null;
        }

        public Size LayoutSize
        {
            get => mPlayer.LayoutSize;
            set { mPlayer.LayoutSize = value; }
        }

        public async void SetUri(Uri uri)
        {
            IWvvCache cache = await (await WvvCacheManager.GetInstanceAsync()).GetCacheAsync(uri);
            mPlayer.SetSource(cache);
            mPanel.SetSource(cache);
            cache.Release();
        }

        public void SetSource(StorageFile source)
        {
            mPlayer.SetSource(source);
            mPanel.SetSource(source);
        }

        public void Start()
        {
            mPlayer.IsPlaying = true;
        }
        public void Stop()
        {
            mPlayer.IsPlaying = false;
        }
    }
}
