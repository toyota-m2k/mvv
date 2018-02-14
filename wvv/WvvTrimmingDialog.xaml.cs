using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    public sealed partial class WvvTrimmingDialog : UserControl, WvvDialog.IWvvDialogContent
    {
        #region Initialize / Terminate

        /**
         * 動画トリミング完了通知用デイゲート型
         * @param trimmed   true: トリミングされている / false:されていない --- この場合は、無駄なのでrendererを呼ばずに、ソースファイルをそのまま使うのがよい。
         * @param renderer  ファイル保存用i/f
         */
        public delegate void WvvTrimmingCompleted(bool trimmed, IWvvSaveAs renderer);

        /**
         * ダイアログを表示する。
         */
        public static async Task<bool> Show(StorageFile source, FrameworkElement anchor, WvvTrimmingCompleted completed)
        {
            if (null == completed)
            {
                return false;
            }

            var content = new WvvTrimmingDialog(source, completed);
            await WvvDialog.Show(content, anchor);
            return true;
        }

        /**
         * コンストラクタ
         */
        private WvvTrimmingDialog(StorageFile source, WvvTrimmingCompleted completed)
        {
            this.mCompleted = new WeakReference<WvvTrimmingCompleted>(completed);

            this.InitializeComponent();
            this.DataContext = this;
            this.mTrimmingView.SetSource(source);
        }

        #endregion

        #region Internal Properties/Fields

        // トリミング完了通知コールバック
        private WvvTrimmingCompleted Completed
        {
            get
            {
                WvvTrimmingCompleted v;
                return mCompleted.TryGetTarget(out v) ? v : null;
            }
            set
            {
                mCompleted.SetTarget(value);
            }
        }
        private WeakReference<WvvTrimmingCompleted> mCompleted = new WeakReference<WvvTrimmingCompleted>(null);

        #endregion

        #region WvvDialog Specific

        // ダイアログクラスの参照を保持するためのフィールド
        private WvvDialog Dialog
        {
            get
            {
                WvvDialog v;
                return mDialog.TryGetTarget(out v) ? v : null;
            }
            set
            {
                mDialog.SetTarget(value);
            }
        }
        private WeakReference<WvvDialog> mDialog = new WeakReference<WvvDialog>(null);

        // ダイアログを閉じるときのフラグ
        bool mClosing = false;

        /**
         * ダイアログを閉じる
         */
        private void closeDialog()
        {
            mClosing = true;
            WvvDialog dlg;
            if (mDialog.TryGetTarget(out dlg))
            {
                dlg.Close();
            }
        }

        /**
         * 確定して閉じる
         */
        private void OnCloseTapped(object sender, TappedRoutedEventArgs e)
        {
            Completed?.Invoke(mTrimmingView.IsTrimmed, mTrimmingView);
            closeDialog();
        }

        /**
         * キャンセルして閉じる
         */
        private void OnCancelTapped(object sender, TappedRoutedEventArgs e)
        {
            closeDialog();
        }

        /**
         * 
         */
        public void Opening(WvvDialog dlg)
        {
        }

        /**
         * ダイアログが開いたところで dlg オブジェクトをメンバーに覚えておく
         */
        public void Opened(WvvDialog dlg)
        {
            mDialog.SetTarget(dlg);
        }

        /**
         * 明示的に閉じる操作が行われたときだけ閉じておｋ（trueを返す）
         */
        public bool Closing(WvvDialog dlg)
        {
            return mClosing;
        }

        /**
         * ダイアログが閉じられたら、保持しているWvvDialogをクリア
         */
        public void Closed(WvvDialog dlg)
        {
            mDialog.SetTarget(null);
        }
        #endregion
    }
}
