using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Core;
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
    public sealed partial class CompositionPage : Page
    {
        private MediaComposition mComposition;

        public CompositionPage()
        {
            mComposition = new MediaComposition();
            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            mPlayerElement.Source = null;
        }

            private void OnPlayerPage(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private async void OnAddClip(object sender, TappedRoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                Debug.WriteLine("File picking cancelled");
                return;
            }

            // These files could be picked from a location that we won't have access to later
            var storageItemAccessList = StorageApplicationPermissions.FutureAccessList;
            storageItemAccessList.Add(file);

            var clip = await MediaClip.CreateFromFileAsync(file);
            mComposition.Clips.Add(clip);
            preview();
        }

        private void preview()
        {
            MediaStreamSource mediaStreamSource = mComposition.GeneratePreviewMediaStreamSource(
                    (int)mPlayerElement.ActualWidth,
                    (int)mPlayerElement.ActualHeight);

            mPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);
        }

        private void OnPreview(object sender, TappedRoutedEventArgs e)
        {
            preview();
        }


        private async void OnSaveAs(object sender, TappedRoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeChoices.Add("MP4 files", new List<string>() { ".mp4" });
            picker.SuggestedFileName = "RenderedComposition.mp4";

            Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                // Call RenderToFileAsync
                var saveOperation = mComposition.RenderToFileAsync(file, MediaTrimmingPreference.Precise);

                saveOperation.Progress = new AsyncOperationProgressHandler<TranscodeFailureReason, double>(async (info, progress) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
                    {
                        Debug.WriteLine(string.Format("Saving file... Progress: {0:F0}%", progress));
                    }));
                });
                saveOperation.Completed = new AsyncOperationWithProgressCompletedHandler<TranscodeFailureReason, double>(async (info, status) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
                    {
                        try
                        {
                            var results = info.GetResults();
                            if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
                            {
                                Debug.WriteLine("Saving was unsuccessful");
                            }
                            else
                            {
                                Debug.WriteLine("Trimmed clip saved to file");
                            }
                        }
                        finally
                        {
                            // Update UI whether the operation succeeded or not
                        }

                    }));
                });
            }
            else
            {
                Debug.WriteLine("User cancelled the file selection");
            }
        }

        private void OnTrimBefore(object sender, TappedRoutedEventArgs e)
        {
            var currentClip = mComposition.Clips.FirstOrDefault(
                    (mc) => {
                        return mc.StartTimeInComposition <= mPlayerElement.MediaPlayer.PlaybackSession.Position &&
                               mc.EndTimeInComposition >= mPlayerElement.MediaPlayer.PlaybackSession.Position;
                        });

            TimeSpan positionFromStart = mPlayerElement.MediaPlayer.PlaybackSession.Position - currentClip.StartTimeInComposition;
            currentClip.TrimTimeFromStart = positionFromStart;
        }

        private void OnTrimAfter(object sender, TappedRoutedEventArgs e)
        {
            var currentClip = mComposition.Clips.FirstOrDefault(
                    (mc) => {
                        return mc.StartTimeInComposition <= mPlayerElement.MediaPlayer.PlaybackSession.Position &&
                               mc.EndTimeInComposition >= mPlayerElement.MediaPlayer.PlaybackSession.Position;
                    });

            TimeSpan positionFromEnd = currentClip.EndTimeInComposition - mPlayerElement.MediaPlayer.PlaybackSession.Position;
            currentClip.TrimTimeFromEnd = positionFromEnd;
        }

        private void OnReset(object sender, TappedRoutedEventArgs e)
        {
            mComposition.Clips.Clear();
            mPlayerElement.Source = null;
        }
    }
}
