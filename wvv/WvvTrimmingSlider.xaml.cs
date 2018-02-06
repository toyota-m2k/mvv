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

        public delegate void TrimmingEventHandler(WvvTrimmingSlider sender, double position);

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
                    TrimStartChanged?.Invoke(this, mTrimStart);
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
                    TrimEndChanged?.Invoke(this, mTrimEnd);
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
                if (value != mTotalRange)
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


        /**
         * トリミング後の動画の長さ
         */
        private double TrimmedRange
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
            public bool Active;
            public double Orginal;
            public double Min;
            public double Max;
            public double Start;
            public double Prev;
        }
        private Tracking mTracking = new Tracking();

        /**
         * TrimStart位置調整用Knobがクリックされた
         */
        private void OnLKnobPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);
            var pos = e.GetCurrentPoint(mTrimmerBase);
            mTracking.Start = pos.Position.X;
            mTracking.Active = true;
            mTracking.Orginal = LWidth;
            mTracking.Max = TotalRange - TrimEnd;
            mTracking.Min = 0;

            Debug.WriteLine("LKnob Pressed.");
        }

        /**
         * TrimStart位置調整用Knobのドラッグ処理
         */
        private void OnLKnobMoved(object sender, PointerRoutedEventArgs e)
        {
            if(!mTracking.Active)
            {
                return;
            }

            var pos = e.GetCurrentPoint(mTrimmerBase);
            Debug.WriteLine("LKnob Moving. {0}", pos.Position.X - mTracking.Start);

            double x = mTracking.Orginal + (pos.Position.X - mTracking.Start);
            double v = x * TotalRange / mTrimmerWidth;
            if(v< mTracking.Min)
            {
                v = mTracking.Min;
            }
            else if(v>mTracking.Max)
            {
                v = mTracking.Max;
            }
            TrimStart = v;
            
        }

        /**
         * TrimEnd位置調整用Knobがクリックされた
         */
        private void OnRKnobPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);

            var pos = e.GetCurrentPoint(mTrimmerBase);
            mTracking.Start = pos.Position.X;
            mTracking.Active = true;
            mTracking.Orginal = RWidth;
            mTracking.Max = TotalRange - TrimStart;
            mTracking.Min = 0;

            Debug.WriteLine("RKnob Pressed.");

        }

        /**
         * TrimEnd位置調整用Knobのドラッグ処理
         */
        private void OnRKnobMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!mTracking.Active)
            {
                return;
            }

            var pos = e.GetCurrentPoint(mTrimmerBase);
            Debug.WriteLine("RKnob Moving. {0}", pos.Position.X - mTracking.Start);

            double x = mTracking.Orginal - (pos.Position.X - mTracking.Start);
            double v = x * TotalRange / mTrimmerWidth;
            if (v < mTracking.Min)
            {
                v = mTracking.Min;
            }
            else if (v > mTracking.Max)
            {
                v = mTracking.Max;
            }
            TrimEnd = v;
        }

        /**
         * CurrentPosition調整用Thumbがクリックされた
         */
        private void OnThumbPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);
            var pos = e.GetCurrentPoint(mTrimmerBase);
            mTracking.Start = pos.Position.X;
            mTracking.Active = true;
            mTracking.Orginal = MWidth;
            mTracking.Prev = AbsoluteCurrentPosition;

            mTracking.Min = 0;
            mTracking.Max = TotalRange - TrimEnd - TrimStart;

            Debug.WriteLine("Thumb Pressed.");
        }

        /**
         * CurrentPosition調整用Thumbのドラッグ処理
         */
        private void OnThumbMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!mTracking.Active)
            {
                return;
            }

            var pos = e.GetCurrentPoint(mTrimmerBase);
            Debug.WriteLine("Thumb Moving. {0}", pos.Position.X - mTracking.Start);

            double x = mTracking.Orginal + (pos.Position.X - mTracking.Start);

            double v = x * TotalRange / mTrimmerWidth;
            if (v < mTracking.Min)
            {
                v = mTracking.Min;
            }
            else if (v > mTracking.Max)
            {
                v = mTracking.Max;
            }
            CurrentPosition = v;
            if(mTracking.Prev != AbsoluteCurrentPosition)
            {
                mTracking.Prev = AbsoluteCurrentPosition;
                CurrentPositionChanged?.Invoke(this, CurrentPosition);
            }
        }

        private void OnKnobReleased(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);

            mTracking.Active = false;
            Debug.WriteLine("Knob Released.");

        }

        #endregion

    }
}
