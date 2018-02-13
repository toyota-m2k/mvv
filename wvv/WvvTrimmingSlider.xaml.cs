using System;
using System.ComponentModel;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    /**
     * トリミング用スライダークラス
     */
    public sealed partial class WvvTrimmingSlider : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion

        #region Events

        public delegate void TrimmingEventHandler(WvvTrimmingSlider sender, double position, bool force);

        /**
         * トリミング開始位置が変更された
         * TrimStart　の値が変更されたときに呼ばれる
         */
        public event TrimmingEventHandler TrimStartChanged;

        /**
         * トリミング終了位置が変更された
         * TrimEnd　の値が変更されたときに呼ばれる
         */
        public event TrimmingEventHandler TrimEndChanged;
        
        /**
         * 再生位置が変更された
         * UI操作によって CurrentPositionが変更されたときに呼ばれる。
         * プログラム的に、CurrentPosition, AbsoluteCurrentPositionを変更したときは呼ばれない。
         */
        public event TrimmingEventHandler CurrentPositionChanged;

        #endregion

        #region Properties

        /**
         * トリミング開始位置（先頭からのオフセット）
         */
        public double TrimStart
        {
            get
            {
                return mTrimStart;
            }
            set
            {
                var v = Math.Min(value, TotalRange - TrimEnd);
                if (v!= mTrimStart)
                {
                    var c = AbsoluteCurrentPosition;
                    mTrimStart = v;
                    notify("LWidth");
                    AbsoluteCurrentPosition = c;
                }
            }
        }
        private double mTrimStart = 0;

        /**
         * トリミング終了位置（末端からのオフセット）
         */
        public double TrimEnd
        {
            get
            {
                return mTrimEnd;
            }
            set
            {
                var v = Math.Min(value, TotalRange - TrimStart);
                if (v!= mTrimEnd)
                {
                    var c = AbsoluteCurrentPosition;
                    mTrimEnd = v;
                    notify("RWidth");
                    AbsoluteCurrentPosition = c;
                }
            }
        }
        private double mTrimEnd = 0;

        /**
         * オリジナル動画全体の長さ
         */
        public double TotalRange
        {
            get
            {
                return mTotalRange;
            }
            set
            {
                if (value != mTotalRange && mTotalRange>0)
                {
                    mTotalRange = value;
                    mTrimStart = 0;
                    mTrimEnd = 0;
                    mCurrentPosition = 0;

                    notify("LWidth");
                    notify("RWidth");
                    notify("MWidth");
                }
            }
        }
        private double mTotalRange = 100;


        public double MinimumRange
        {
            get; set;
        } = 100;        // 100ms 未満にはトリミングできないようにする。

        /**
         * トリミング後の動画の長さ
         */
        public double TrimmedRange
        {
            get
            {
                return mTotalRange - mTrimStart - mTrimEnd;
            }
        }

        /**
         * 動画ファイル全体の中での再生位置
         */
        public double AbsoluteCurrentPosition
        {
            get
            {
                return TrimStart + CurrentPosition;
            }
            set
            {
                // var v = Math.Min(Math.Max(TrimStart, value), TotalRange-TrimEnd);
                CurrentPosition = value - TrimStart;
            }
        }

        /**
         * トリミング後の動画の中の再生位置
         */
        public double CurrentPosition
        {
            get
            {
                return mCurrentPosition;
            }
            set
            {
                var v = Math.Max(Math.Min(value, TotalRange - TrimEnd - TrimStart), 0);
                if(mCurrentPosition!=v)
                {
                    mCurrentPosition = v;
                    notify("MWidth");
                }
            }
        }
        private double mCurrentPosition = 0;

        /**
         * Trim用ノブを表示する(true)か、しない(false)か？
         */
        public bool ShowTrimmingKnob
        {
            get
            {
                return mShowTrimmingKnob;
            }
            set
            {
                if(mShowTrimmingKnob != value)
                {
                    mShowTrimmingKnob = value;
                    notify("ShowTrimingKnob");
                }
            }
        }
        private bool mShowTrimmingKnob = true;

        #endregion

        #region Private Fields

        /**
         * スライダーの幅（Knob/Thumbの位置計算のベース）
         */
        private double mTrimmerWidth = 10;

        #endregion

        #region Bindings

        /**
         * TrimStart 部分の長さ
         */
        public double LWidth
        {
            get
            {
                return mTrimmerWidth * TrimStart / TotalRange;
            }
        }

        /**
         * TrimEnd 部分の長さ
         */
        public double RWidth
        {
            get
            {
                return mTrimmerWidth * TrimEnd / TotalRange;
            }
        }

        /**
         * TrimStartから再生位置までの長さ
         */
        public double MWidth
        {
            get
            {
                // mTrimmerWidth * TrimmedRange / TotalRange   * CurrentPosition / TrimmedRange;
                return mTrimmerWidth * CurrentPosition / TotalRange;
            }
        }

        #endregion

        #region Initializing / Terminating

        public WvvTrimmingSlider()
        {
            this.DataContext = this;
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        public void Reset()
        {
            mTotalRange = 100;
            mTrimStart = 0;
            mTrimEnd = 0;
            mCurrentPosition = 0;

            notify("LWidth");
            notify("RWidth");
            notify("MWidth");
        }

        /**
         * totalRangeを与えてコントロールを初期化する
         */
        //public void Init(double totalRange)
        //{
        //    mTotalRange = totalRange;
        //    mTrimStart = 0;
        //    mTrimEnd = 0;
        //    mCurrentPosition = 0;

        //    notify("LWidth");
        //    notify("RWidth");
        //    notify("MWidth");
        //}

        #endregion

        #region UI Event Handler

        /**
         * スライダーのサイズに合わせてKnob/Thumbの位置を調整する
         */
        private void OnTrimmerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double w = mTrimmerBase.ActualWidth;
            if(w!=mTrimmerWidth)
            {
                mTrimmerWidth = w;
                notify("LWidth");
                notify("RWidth");
                notify("MWidth");
            }
        }

        #endregion

        #region Tracking Knobs

        /**
         * Thumb/KnobのTracking情報
         */
        struct Tracking
        {
            public delegate void MovedHandler(double newValue, bool completed);

            public bool Active;
            public double Orginal;
            public double Min;
            public double Max;
            public double Start;
            public double Prev;
            public double Ext;
            public int Dir;
            public MovedHandler Moved;
        }
        private Tracking mTracking = new Tracking();

        /**
         * TrimStart位置調整用Knobがクリックされた
         */
        private void OnLKnobPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);

            beginTracking(e, 1, LWidth, TrimStart, TotalRange-TrimEnd - MinimumRange);

            mTracking.Moved = (v, last) =>
            {
                TrimStart = v;
                if (mTracking.Ext != v)
                {
                    mTracking.Ext = v;
                    TrimStartChanged?.Invoke(this, v, last);
                }
            };

            Debug.WriteLine("LKnob Pressed.");
        }

        /**
         * TrimEnd位置調整用Knobがクリックされた
         */
        private void OnRKnobPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);

            beginTracking(e, -1, RWidth, TrimEnd, TotalRange-TrimStart- MinimumRange);

            mTracking.Moved = (v, last) =>
            {
                TrimEnd = v;
                if (mTracking.Ext != v)
                {
                    mTracking.Ext = v;
                    TrimEndChanged?.Invoke(this, v, last);
                }
            };

            Debug.WriteLine("RKnob Pressed.");

        }

        /**
         * CurrentPosition調整用Thumbがクリックされた
         */
        private void OnThumbPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);

            beginTracking(e, 1, MWidth, AbsoluteCurrentPosition, TotalRange - TrimEnd - TrimStart);

            mTracking.Moved = (v, last) =>
            {
                CurrentPosition = v;
                if (mTracking.Ext != AbsoluteCurrentPosition)
                {
                    mTracking.Ext = AbsoluteCurrentPosition;
                    CurrentPositionChanged?.Invoke(this, CurrentPosition, false);
                }
            };

            Debug.WriteLine("Thumb Pressed.");
        }

        /**
         * トラッキング開始
         */
        private void beginTracking(PointerRoutedEventArgs e, int dir, double original, double ext, double max)
        {
            var pos = e.GetCurrentPoint(mTrimmerBase);
            mTracking.Start = pos.Position.X;
            mTracking.Active = true;
            mTracking.Orginal = mTracking.Prev = original;
            mTracking.Ext = ext;
            mTracking.Dir = dir;
            mTracking.Min = 0;
            mTracking.Max = max;
        }

        /**
         * トラッキング中/後の移動距離から新しい値を計算
         */
        private double getNewValue(PointerRoutedEventArgs e)
        {
            var pos = e.GetCurrentPoint(mTrimmerBase);
            Debug.WriteLine("Knob Moving. {0}", pos.Position.X - mTracking.Start);

            double x = mTracking.Orginal + (pos.Position.X - mTracking.Start) * mTracking.Dir;
            double v = x * TotalRange / mTrimmerWidth;
            if (v < mTracking.Min)
            {
                v = mTracking.Min;
            }
            else if (v > mTracking.Max)
            {
                v = mTracking.Max;
            }
            return v;
        }

        /**
         * トラッキング中のイベントハンドラ
         */
        private void OnKnobMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!mTracking.Active)
            {
                return;
            }

            var v = getNewValue(e);
            mTracking.Prev = v;
            mTracking.Moved(v, false);
        }

        /**
         * トラッキング終了時のハンドラ
         */
        private void OnKnobReleased(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);

            mTracking.Active = false;

            if (null != mTracking.Moved)
            {
                var v = getNewValue(e);
                mTracking.Moved(v, true);
                mTracking.Moved = null;
            }
            Debug.WriteLine("Knob Released.");
        }

        #endregion

    }
}
