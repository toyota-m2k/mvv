using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace wvv
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class WvvVideoPlayerPage : Page
    {
        public WvvVideoPlayerPage()
        {
            this.DataContext = this;
            this.InitializeComponent();
        }

        private StorageFile mSource;

        private async void OnOpenFile(object sender, TappedRoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".mpg");
            mSource = await picker.PickSingleFileAsync();
            if (mSource == null)
            {
                Debug.WriteLine("File picking cancelled");
                return;
            }
            mPlayer.SetSource(mSource);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mPlayer.SetUri(new Uri("https://video.twimg.com/ext_tw_video/965217837219381248/pu/vid/640x360/2iECofDRubgGIAcg.mp4"));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if(null!=mTempFolder)
            {
                mTempFolder.Dispose();
                mTempFolder = null;
            }
        }

        private void OnPlayerPage(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void Composition_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CompositionPage));
        }

        private void OnTrimming(object sender, TappedRoutedEventArgs e)
        {
            mTrimmingView.SetSource(mSource);
        }

        WvvTempFolder mTempFolder;

        private async void DoTrim(object sender, TappedRoutedEventArgs e)
        {
            if(null==mTempFolder)
            {
                mTempFolder = await WvvTempFolder.Create("trimming");
            }
            mTrimmingView.SaveAs((await mTempFolder.CreateTempFile("m", ".mp4")).File, (trimmer, succeeded) =>
            {
                if(succeeded)
                {
                    Debug.WriteLine("Encoded successfully.");
                }
                else
                {
                    Debug.WriteLine("Encoding error.");
                }
            });
        }

        private void Open_URL(object sender, RoutedEventArgs e)
        {
            var text = mMovieURL.Text;
            if (null != text && text.Length > 0)
            {
                mPlayer.SetUri(new Uri(text));
            }
        }
    }
}
