using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace wvv
{
    public sealed partial class WvvFrameListView : UserControl, INotifyPropertyChanged
    {
        #region Public Interface

        /**
         * スクロール位置（全体を1とした値: 0--1)
         * 通常、TickPosition / LeftTrim / RightTrim の指定に連動して自動的に調整されるため、特にこれだけを設定する必要はないと思う。
         */
        public double Position
        {
            get { return mPosition; }
            set
            {
                if (mPosition != value)
                {
                    mPosition = value;
                    notify("ScrollMargin");
                }
            }
        }
        private double mPosition = 0;

        /**
         * 再生位置（全体を1とした値: 0--1)
         */
        public double TickPosition
        {
            get { return mTickPosition; }
            set
            {
                if (value != mTickPosition)
                {
                    mTickPosition = value;
                    notify("TWidth");
                    Position = value;
                }
            }
        }
        double mTickPosition = 0;

        /**
         * 先頭のトリミング位置（全体を１とした値： 0--1)
         */
        public double LeftTrim
        {
            get { return mLeftTrim; }
            set
            {
                if (mLeftTrim != value)
                {
                    mLeftTrim = value;
                    notify("LWidth");
                    Position = value;
                }
            }

        }
        private double mLeftTrim;

        /**
         * 末尾のトリミング位置（全体を１とした値： 0--1)
         */
        public double RightTrim
        {
            get { return mRightTrim; }
            set
            {
                if (mRightTrim != value)
                {
                    mRightTrim = value;
                    notify("RWidth");
                    Position = 1.0 - value;
                }
            }
        }
        private double mRightTrim;

        /**
         * 再生位置マークを表示するか？
         */
        public bool ShowCurrentTick
        {
            get { return mShowCurrentTick; }
            set
            {
                if(value != mShowCurrentTick )
                {
                    mShowCurrentTick = value;
                    notify("ShowCurrentTick");
                }
            }
        }
        private bool mShowCurrentTick = true;


        /**
         * 操作情報をクリアする
         */
        public void Reset()
        {
            Frames.Clear();
            mPosition = mTickPosition = mLeftTrim = mRightTrim = 0;
            notify("LWidth");
            notify("RWidth");
            notify("TWidth");
            notify("ScrollMargin");
        }

        /**
         * タップ位置から全フレーム内の位置（シーク位置）を取得する。
         */
        public double GetTappedPosition(TappedRoutedEventArgs e)
        {
            var pos = e.GetPosition(mContainerGrid);
            return ( ScrollableWidth * Position + pos.X ) / FrameListWidth;
        }

        #endregion

        #region Bindings

        /**
         * フレームリスト
         */
        public ObservableCollection<ImageSource> Frames
        {
            get;
        } = new ObservableCollection<ImageSource>();

        /**
         * スクロールコンテントの幅
         */
        public double FrameListWidth
        {
            get
            {
                return mFrameListWidth;
            }
            set
            {
                if(mFrameListWidth!=value)
                {
                    mFrameListWidth = value;
                    notify("FrameListWidth");
                    notify("LWidth");
                    notify("RWidth");
                    notify("TWidth");
                    notify("ScrollMargin");
                }
            }
        }
        private double mFrameListWidth = 100;

        /**
         * スクロールコンテントの高さ
         * 
         * Height="{Binding ElementName=mListView, Path=ActualHeight}" という指定がうまく動作しなかったので、
         * コードビハインドでなんとかする。
         */
        public double FrameListHeight
        {
            get
            {
                return mFrameListHeight;
            }
            set
            {
                if(mFrameListHeight!=value)
                {
                    mFrameListHeight = value;
                    notify("FrameListHeight");
                }
            }
        }
        private double mFrameListHeight = 44;

        /**
         * 左トリミング部分の幅(px)
         */
        public double LWidth
        {
            get
            {
                return FrameListWidth * LeftTrim;
            }
        }

        /**
         * 右トリミング部分の幅(px)
         */
        public double RWidth
        {
            get
            {
                return FrameListWidth * RightTrim;
            }
        }

        /**
         * 先頭から再生位置までの幅(px)
         */
        public double TWidth
        {
            get
            {
                return FrameListWidth * TickPosition;
            }
        }

        /**
         * Positionに応じたスクロール量
         */
        public Thickness ScrollMargin
        {
            get
            {
                double sc = ScrollableWidth * Position;
                return new Thickness(-sc, 0, 0, 0);
            }
        }

        #endregion


        #region Private Fields / Properties

        /**
         * ScrollViewerオブジェクトを取得
         */
        private ScrollViewer ScrollViewer
        {
            get
            {
                if (null == mScrollViewer && null != mListView)
                {
                    var border = VisualTreeHelper.GetChild(mListView, 0) as Border;
                    if (null != border)
                    {
                        mScrollViewer = border.Child as ScrollViewer;
                    }
                }
                return mScrollViewer;
            }
        }
        private ScrollViewer mScrollViewer = null;


        /**
         * スクロールビューアのExtentWidth, ActualHeightの変化を監視するためのリスナー登録トークン
         */
        private long mScrollViewerExtentWidthChangedToken = 0;
        //private long mScrollViewerActualHeightChangedToken = 0;

        /**
         * スクロール可能な幅
         */
        private double ScrollableWidth
        {
            get
            {
                return FrameListWidth - mContainerGrid.ActualWidth;
            }
        }


        #endregion

        #region Initialzation /Termination

        /**
         * コンストラクタ
         */
        public WvvFrameListView()
        {
            this.InitializeComponent();
            this.DataContext = this;
        }

        /**
         * 初期化
         */
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mScrollViewerExtentWidthChangedToken = ScrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ExtentWidthProperty, ScrollViewer_ExtentWidthChanged);
            //mScrollViewerActualHeightChangedToken = ScrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ActualHeightProperty, ScrollViewer_ActualHeightChanged);
        }

        /**
         * 解放処理
         */
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ScrollViewer.UnregisterPropertyChangedCallback(ScrollViewer.ExtentWidthProperty, mScrollViewerExtentWidthChangedToken);
            //ScrollViewer.UnregisterPropertyChangedCallback(ScrollViewer.ActualHeightProperty, mScrollViewerActualHeightChangedToken);
            Frames.Clear();
        }
        #endregion

        #region Event Handlers

        /**
         * ExtentWidth監視リスナー
         */
        private void ScrollViewer_ExtentWidthChanged(DependencyObject sender, DependencyProperty dp)
        {
            FrameListWidth = ScrollViewer.ExtentWidth;
            //FrameListHeight = ScrollViewer.ActualHeight;
        }

        //private void ScrollViewer_ActualHeightChanged(DependencyObject sender, DependencyProperty dp)
        //{
        //    FrameListHeight = ScrollViewer.ActualHeight;
        //}


        #endregion

        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion
    }
}
