using System;
using System.Collections.Generic;
using System.ComponentModel;
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

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv.work
{
    public sealed partial class MfPlayerView : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private bool setProp<T>(string name, ref T field, T value)
        {
            if (!field.Equals(value))
            {
                field = value;
                notify(name);
                return true;
            }
            return false;
        }

        private bool setProp<T>(string[] names, ref T field, T value)
        {
            if (!field.Equals(value))
            {
                field = value;
                foreach (var name in names)
                {
                    notify(name);
                }
                return true;
            }
            return false;
        }


        #endregion

        private bool mReady = false;
        public bool Ready
        {
            get => mReady;
            set => setProp("Ready", ref mReady, value);
        }

        private MfFileInfo mItem;
        public MfFileInfo Item
        {
            get => mItem;
            set => setProp("Item", ref mItem, value);
        }

        public MfPlayerView()
        {
            this.InitializeComponent();
        }

        private void OnPlay(object sender, TappedRoutedEventArgs e)
        {

        }

        private void OnDelete(object sender, TappedRoutedEventArgs e)
        {

        }

        private void OnCurrentPositionChanged(object sender, double position, bool final)
        {

        }

        private void OnSliderTapped(object sender, double position, bool final)
        {

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
