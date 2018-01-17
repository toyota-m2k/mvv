using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    public interface IAppendMarkerChecker
    {
        bool CanAppendMarker(double position);
    }

    public sealed partial class WvvMarkerView : UserControl, IAppendMarkerChecker
    {
        #region Constants

        const double POS_MIN = -1;
        const double POS_RIGHT_MARGIN = 16;

        #endregion

        #region Fields

        BitmapImage mMarkerImage;
        SortedList<double, Image> mMarkers;

        #endregion

        #region Initialization/Termination

        public WvvMarkerView()
        {
            this.InitializeComponent();
            mMarkerImage = new BitmapImage(new Uri("ms-appx:///Assets/marker.png"));
            mMarkers = new SortedList<double, Image>(128);
        }
        #endregion

        #region Properties

        public double TotalRange
        {
            get { return mTotalRange; }
            set
            {
                if(value != mTotalRange)
                {
                    mTotalRange = value;
                    resetMarkerIconPositions();
                }
            }
        }
        private double mTotalRange = 100;

        #endregion

        #region UI Event Handlers

        private async void OnMarkerRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // Debug.WriteLine("Mark Right Tapped {0}", (double)((Image)sender).Tag);
            var img = sender as Image;
            if (null != img)
            {
                // ToDo: ちゃんとしたPopupMenuへの差し替え
                var menu = new PopupMenu();
                menu.Commands.Add(new UICommand("削除"));

                var rect = new Rect(0, 0, img.ActualWidth, img.ActualHeight);
                var selected = await menu.ShowForSelectionAsync(img.TransformToVisual(null).TransformBounds(rect));

                if (null != selected)
                {
                    removeMarker(img, this);
                }

                //if (e.PointerDeviceType == PointerDeviceType.Mouse)
                //{
                //    removeMarker(img, this);
                //}
            }
        }

        private void OnMarkerTapped(object sender, TappedRoutedEventArgs e)
        {
            // Debug.WriteLine("Mark Tapped {0}", (double)((Image)sender).Tag);
            var img = sender as Image;
            if (null != img)
            {
                selectMarker(img, this);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            resetMarkerIconPositions();
        }

        #endregion

        #region Private Methods

        private Image createMarkerIcon(double value)
        {
            Image img = new Image();
            img.Source = mMarkerImage;
            img.Stretch = Stretch.None;
            img.HorizontalAlignment = HorizontalAlignment.Left;
            img.Tag = value;
            setMarkerIconPosition(img);

            img.Tapped += OnMarkerTapped;
            img.RightTapped += OnMarkerRightTapped;
            return img;
        }

        private void destroyMarkerIcon(Image img)
        {
            img.Tapped -= OnMarkerTapped;
            img.RightTapped -= OnMarkerRightTapped;
        }

        private void setMarkerIconPosition(Image img)
        {
            double value = (double)img.Tag;
            double min = POS_MIN, max = ActualWidth - POS_RIGHT_MARGIN, pos;
            pos = (max - min) * value / TotalRange + min;
            img.Margin = new Thickness(pos, 0, 0, 0);
        }

        private void resetMarkerIconPositions()
        {
            foreach (Image img in mMarkerContainer.Children)
            {
                setMarkerIconPosition(img);
            }
        }

        private void removeMarker(Image img, object clientData)
        {
            destroyMarkerIcon(img);
            mMarkerContainer.Children.Remove(img);
            mMarkers.Remove((double)img.Tag);
            MarkerRemoved?.Invoke((double)img.Tag, clientData);
        }

        private void selectMarker(Image img, object clientData)
        {
            MarkerSelected?.Invoke((double)img.Tag, clientData);
        }

        #endregion

        #region Public Methods

        /**
         * マーカーをすべてクリアする。
         * 動画ファイル読み直し時の処理を想定しているので、MarkerRemovedイベントは発行しない。
         */
        public void Clear()
        {
            foreach (Image img in mMarkerContainer.Children)
            {
                destroyMarkerIcon(img);
            }
            mMarkerContainer.Children.Clear();
            mMarkers.Clear();
        }

        /**
         * 設定されているマーカーをすべてクリアしてから、新しいマーカーを設定する。
         * 動画ファイル読み直し時の処理を想定しているので、MarkerRemoved/MarkerAddedイベントは発行しない。
         */
        public void SetMarkers(IEnumerable<double> markers, object clientData)
        {
            Clear();
            foreach (var v in markers)
            {
                AddMarker(v, clientData);
            }
        }

        /**
         * マーカーを追加する。
         * 
         * @event MarkerAdded
         */
        public void AddMarker(double value, object clientData)
        {
            Image img;
            var checker = clientData as IAppendMarkerChecker;
            if(null!=checker)
            {
                if(!checker.CanAppendMarker(value))
                {
                    return;
                }
            }
            else if (mMarkers.TryGetValue(value, out img))
            {
                return;
            }

            img = createMarkerIcon(value);
            mMarkerContainer.Children.Add(img);
            mMarkers[value] = img;
            MarkerAdded?.Invoke(value, clientData);
        }

        /**
         * マーカーを削除する。
         * 
         * @event MarkerRemoved
         */
        public void RemoveMarker(double value, object clientData)
        {
            Image img;
            if (mMarkers.TryGetValue(value, out img))
            {
                removeMarker(img, clientData);
            }
        }

        /**
         * 指定位置(current)近傍のマーカーを取得
         * 
         * @param prev (out) currentの前のマーカー（なければ-1）
         * @param next (out) currentの次のマーカー（なければ-1）
         * @return true: currentがマーカー位置にヒット（prevとnextにひとつ前/後のindexがセットされる）
         *         false: ヒットしていない
         */
        private bool getNeighbourMarkIndex(double current, out int prev, out int next)
        {
            prev = next = -1;
            int count = mMarkers.Count, s = 0, e = count - 1, m;
            if (e < 0)
            {
                return false;
            }

            var markers = mMarkers.Keys;
            if(markers[e]<current)
            {
                prev = e;
                return false;
            }

            while (s < e)
            {
                m = (s + e) / 2;
                double v = markers[m];
                if (v == current)
                {
                    prev = m - 1;
                    if(m<count-1)
                    {
                        next = m + 1;
                    }
                    return true;
                }
                else if (v < current)
                {
                    s = m + 1;
                }
                else // current < markers[m]
                {
                    e = m;
                }
            }
            next = s;
            prev = s - 1;
            return false;
        }

        /**
         * 次のマーカーを選択する。
         * 
         * @event MarkerSelected
         * @return true 成功 / false: これ以上後ろにマーカーは存在しない
         */
        public bool NextMark(double current, object clientData)
        {
#if true
            int next, prev;
            getNeighbourMarkIndex(current, out prev, out next);
            if(next<0)
            {
                return false;
            }
            selectMarker(mMarkers.Values[next], clientData);
            return true;

#else
            int s = 0, e = mMarkers.Count - 1, m;
            if(e<0)
            {
                return false;
            }


            var markers = mMarkers.Keys;
            if (current < markers[s])
            {
                selectMarker(mMarkers.Values[s], clientData);
                return true;
            }
            if(markers[e]<=current)
            {
                return false;
            }


            while (s<e) {
                m = (s + e) / 2;
                if(markers[m]<=current)
                {
                    s = m + 1;
                }
                else // current < markers[m]
                {
                    e = m;
                }
            }
            selectMarker(mMarkers.Values[s], clientData);
            return true;
#endif
        }

        /**
         * 前のマーカーを選択する。
         * 
         * @event MarkerSelected
         * @return true 成功 / false: これ以上前にマーカーは存在しない
         */
        public bool PrevMark(double current, object clientData)
        {
#if true
            int next, prev;
            getNeighbourMarkIndex(current, out prev, out next);
            if (prev < 0)
            {
                return false;
            }
            selectMarker(mMarkers.Values[prev], clientData);
            return true;
#else
            int s = 0, e = mMarkers.Count - 1, m;
            if (e < 0)
            {
                return false;
            }


            var markers = mMarkers.Keys;
            if (markers[e]<current)
            {
                selectMarker(mMarkers.Values[e], clientData);
                return true;
            }
            if (current <= markers[s])
            {
                return false;
            }


            while (s < e)
            {
                m = (s + e) / 2;
                if (markers[m] < current)
                {
                    s = m + 1;
                }
                else // current < markers[m]
                {
                    e = m;
                }
            }
            if(e==s)
            {
                e--;
            }
            selectMarker(mMarkers.Values[e], clientData);
            return true;
#endif
        }

        /**
         * マーカーを設定可能な最小スパンのチェック
         * あほみたいに追加ボタンを押してマーカーだらけになるのを回避するためのチェック
         */
        private const double MIN_MARKER_SPAN = 100;       // 100 ms
        public bool CanAppendMarker(double position)
        {
            int next, prev;
            if(getNeighbourMarkIndex(position, out prev, out next))
            {
                return false;
            }

            if(prev>=0 && Math.Abs(mMarkers.Keys[prev]-position) < MIN_MARKER_SPAN)
            {
                return false;
            }
            if(next>=0 && Math.Abs(mMarkers.Keys[next]-position) < MIN_MARKER_SPAN)
            {
                return false;
            }
            return true;
        }

        #endregion

        #region Events

        public delegate void MarkerEvent(double value, object clientData);

        /**
         * マーカーが追加されたときのイベント
         */
        public event MarkerEvent MarkerAdded;
        /**
         * マーカーが削除されたときのイベント
         */
        public event MarkerEvent MarkerRemoved;
        /**
         * マーカーが選択されたときのイベント
         */
        public event MarkerEvent MarkerSelected;
#endregion
    }
}
