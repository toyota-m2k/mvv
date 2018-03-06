using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class GridViewPage : Page
    {
        public GridViewPage()
        {
            this.InitializeComponent();
        }

        private void Composition_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CompositionPage));
        }

        private void OnVideoPlayer(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(WvvVideoPlayerPage));
        }

        private void FrameSelector_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(FrameSelectionPage));
        }
    }
}
