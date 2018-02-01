using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv
{
    /**
     * View の中央にポップアップするダイアログクラス
     */
    public sealed partial class WvvDialog : UserControl
    {
        public interface IWvvDialogContent
        {
            void Opening(WvvDialog dlg);
            void Opened(WvvDialog dlg);
            bool Closing(WvvDialog dlg);
            void Closed(WvvDialog dlg);
        }

        /**
         * Content（通常、UserControl派生のインスタンス）をダイアログに表示する。
         * @param content ダイアログの中身
         * @param target  Flyoutの位置決めに使うアンカー・・・センタリングするので、実際には使われないが、Flyout.ShowAt()に渡さないとエラーになるので。
         */
        public static async Task<WvvDialog> Show(FrameworkElement content, UIElement anchor)
        {

            //var m = new Flyout
            //{
            //    Placement = FlyoutPlacementMode.Full,
            //    FlyoutPresenterStyle = (Style)dlg.Resources["FlyoutPresenterStyle"]
            //};
            var dlg = new WvvDialog();
            await anchor.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var m = dlg.Flyout;
                m.Content = content;
                m.ShowAt((FrameworkElement)anchor);
            });
            return dlg;

        }

        public UIElement DialogContent
        {
            get
            {
                return Flyout.Content;
            }
        }

        public void Close()
        {
            if(null!=Flyout)
            {
                Flyout.Hide();
            }
        }

        public WvvDialog()
        {
            this.InitializeComponent();
        }

        private void OnFlyoutOpened(object sender, object e)
        {
            (DialogContent as IWvvDialogContent)?.Opened(this);
        }

        private void OnFlyoutClosing(Windows.UI.Xaml.Controls.Primitives.FlyoutBase sender, Windows.UI.Xaml.Controls.Primitives.FlyoutBaseClosingEventArgs args)
        {
            if(!(DialogContent as IWvvDialogContent)?.Closing(this) ?? true)
            {
                args.Cancel = true;
            }
        }

        private void OnFlyoutClosed(object sender, object e)
        {
            (DialogContent as IWvvDialogContent)?.Closed(this);
        }
    }
}
