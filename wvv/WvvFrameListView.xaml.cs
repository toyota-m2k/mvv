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
                }
            }
        }
        private double mRightTrim;

        #endregion

        #region Bindings

        /**
         * フレームリスト
         */
        public ObservableCollection<ImageSource> Frames
        {
            get;
        } = new ObservableCollection<ImageSource>();

        public ScrollViewer ScrollViewer
        {
            get
            {
                if(null==mScrollViewer && null!=mListView)
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
         * スクロールコンテントの幅
         */
        public double FrameListWidth
        {
            get
            {
                return ScrollViewer?.ExtentWidth ?? 100;
            }
        }

        public double LWidth
        {
            get
            {
                return ScrollableWidth * LeftTrim;
            }
        }

        public double RWidth
        {
            get
            {
                return ScrollableWidth * RightTrim;
            }
        }

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

        private long mScrollViewerExtentWidthChangedToken = 0;

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

        public WvvFrameListView()
        {
            this.InitializeComponent();
            this.DataContext = this;
        }


        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mScrollViewerExtentWidthChangedToken = ScrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ExtentWidthProperty, ScrollViewer_ExtentWidthChanged);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ScrollViewer.UnregisterPropertyChangedCallback(ScrollViewer.ExtentWidthProperty, mScrollViewerExtentWidthChangedToken);
            Frames.Clear();
        }
        #endregion

        #region Event Handlers

        private void ScrollViewer_ExtentWidthChanged(DependencyObject sender, DependencyProperty dp)
        {
            notify("FrameListWidth");
        }

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
