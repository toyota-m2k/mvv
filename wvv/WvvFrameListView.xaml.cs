﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

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
                    scroll();
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
                    Position = value;
                    notify("TWidth");
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
                    Position = value;
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
                    Position = 1.0 - value;
                    notify("RWidth");
                }
            }
        }
        private double mRightTrim;

        /**
         * 再生位置マークを表示するか？
         * 
         * このフラグをtrueにしても、再生マークが表示されていないときは、FrameListHeight がゼロになっていないか確認すること。
         * ListViewのコンテント（フレームサムネイルビットマップリスト）とListViewの間に、微妙なマージン（上下 2px)があり、
         * 再生マークなどの高さをビットマップにそろえるため、FrameListHeightとして、このマージンを含まない高さを与えることにした。
         * 初期値はゼロにしているので、プログラムから値を指定する必要がある。
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


        public bool AnimationEnabled
        {
            get; set;
        } = true;


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
            //notify("ScrollAmount");
            Canvas.SetLeft(mScrollGrid, 0);
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
                    //notify("ScrollAmount");
                    Canvas.SetLeft(mScrollGrid, 0);

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
                    notify("FrameListViewHeight");
                }
            }
        }
        private double mFrameListHeight = 0;

        public double FrameListViewHeight
        {
            get
            {
                // return mListView.ActualHeight;       NG
                return mFrameListHeight + 4;
            }
        }

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
        //public double ScrollAmount
        //{
        //    get
        //    {
        //        return -ScrollableWidth * Position;
        //    }
        //}

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
        //private long mListViewActualHeifhtChangedToken = 0;
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



        private DoubleAnimation mAnim = null;
        private Storyboard mStoryboard = null;

        /**
         * フレームリストをスクロールする
         */
        private void scroll()
        {
            mStoryboard.SkipToFill();

            if (AnimationEnabled) {
                var scrTo = -ScrollableWidth * Position;
                var scrFrom = Canvas.GetLeft(mScrollGrid);
                var duration = Math.Max(50, 500 * Math.Abs(scrTo - scrFrom) / ScrollableWidth);
                mAnim.From = scrFrom;
                mAnim.To = scrTo;
                mAnim.Duration = new Duration(TimeSpan.FromMilliseconds(duration));
                mStoryboard.Begin();
            }
            else
            {
                Canvas.SetLeft(mScrollGrid, -ScrollableWidth * Position);
            }
        }

        private void initAnimation()
        {
            if (null == mStoryboard)
            {
                mAnim = new DoubleAnimation();
                mAnim.EasingFunction = new CubicEase();
                mStoryboard = new Storyboard();
                Storyboard.SetTarget(mAnim, mScrollGrid);
                Storyboard.SetTargetProperty(mAnim, "(Canvas.Left)");
                mStoryboard.Children.Add(mAnim);
            }
        }

        private void dismissAnimation()
        {
            if (null != mStoryboard)
            {
                mStoryboard.Stop();
                mStoryboard = null;
                mAnim = null;
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
            //mListViewActualHeifhtChangedToken = mListView.RegisterPropertyChangedCallback(ListView.ActualHeightProperty, ListView_ActualHeightChanged);
            //mScrollViewerActualHeightChangedToken = ScrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ActualHeightProperty, ScrollViewer_ActualHeightChanged);
            initAnimation();
        }

        /**
         * 解放処理
         */
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ScrollViewer.UnregisterPropertyChangedCallback(ScrollViewer.ExtentWidthProperty, mScrollViewerExtentWidthChangedToken);
            //mListView.UnregisterPropertyChangedCallback(ListView.ActualHeightProperty, mListViewActualHeifhtChangedToken);
            //ScrollViewer.UnregisterPropertyChangedCallback(ScrollViewer.ActualHeightProperty, mScrollViewerActualHeightChangedToken);
            Reset();
            dismissAnimation();
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

        //private void ListView_ActualHeightChanged(DependencyObject sender, DependencyProperty dp)
        //{
        //    notify("FrameListViewHeight");
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
