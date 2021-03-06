﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
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
    public sealed partial class FrameSelectionPage : Page
    {
        public FrameSelectionPage()
        {
            this.InitializeComponent();
        }

        private void VideoPlayer_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(WvvVideoPlayerPage));
        }

        private void Composition_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CompositionPage));
        }

        private void OnPlayerPage(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private async void OnOpenFile(object sender, TappedRoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".avi");
            picker.FileTypeFilter.Add(".wmv");
            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                Debug.WriteLine("File picking cancelled");
                return;
            }

            mFrameSelectorView.SetSource(file);
        }
    }
}
