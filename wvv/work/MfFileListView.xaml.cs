using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using wvv.utils;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv.work
{
    #region MfFileListView

    public sealed partial class MfFileListView : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private bool setProp<T>(string name, ref T field, T value)
        {
            if(!field.Equals(value))
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

        public MfFileListView()
        {
            FileList = new MfFileList(this);
            this.InitializeComponent();
            this.DataContext = this;

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            FileList.Add(new MfWaitingItem());
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

        }




        #region Bindings

        /**
         * ファイルリスト
         */
        public MfFileList FileList { get; }

        /**
         * 個人のストレージ使用量
         * （単位込みで返す）
         */
        private readonly string[] mPersonalConsumedPropNames = new string[] { "PersonalConsumed", "PersonalConsumedText" };
        private int mPersonalConsumed = 0;  // MB
        public int PersonalConsumed
        {
            get => mPersonalConsumed;
            set => setProp(mPersonalConsumedPropNames, ref mPersonalConsumed, value);
        }

        private string formatComsumedString(int consumedInMB)
        {
            if (consumedInMB < 1024 * 10)
            {
                return String.Format("{0} MB", consumedInMB);
            }
            else
            {
                return String.Format("{0} GB", consumedInMB / 1024);
            }
        }
        public string PersonlConsumedText { get => formatComsumedString(mPersonalConsumed); }

        /**
         * 法人全体のストレージ使用量
         * （単位込みで返す）
         */
        private readonly string[] mCompanyConsumedPropNames = new string[] { "CompanyConsumed", "CompanyConsumedText" };
        private int mCompanyConsumed = 0;   //MB
        public int CompanyConsumed
        {
            get => mCompanyConsumed;
            set => setProp(mCompanyConsumedPropNames, ref mCompanyConsumed, value);
        }
        public string CompanyConsumedText { get => formatComsumedString(mCompanyConsumed); }

        // GridView のスクロール方向
        // 設定で変えられるようにするほどではないが、どちらにでも変えられるよう仕組みを用意しておく。
        public class OrientationSettings
        {
            public Orientation Orientation { get => Orientation.Horizontal; }   // Vertical: 横スクロール / Horizontal: 縦スクロール

            public ScrollMode HorizScrollMode
            {
                get => (Orientation == Orientation.Vertical) ? ScrollMode.Enabled : ScrollMode.Disabled;
            }
            public ScrollMode VertScrollMode
            {
                get => (Orientation == Orientation.Vertical) ? ScrollMode.Disabled : ScrollMode.Enabled;
            }
            public ScrollBarVisibility HorzScrollBarVisibility
            {
                get => (Orientation == Orientation.Vertical) ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
            }
            public ScrollBarVisibility VertScrollBarVisibility
            {
                get => (Orientation == Orientation.Vertical) ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Visible;
            }
        }
        public OrientationSettings Settings { get; } = new OrientationSettings();


        #endregion

        private void OnItemClicked(object sender, ItemClickEventArgs e)
        {
            var file = e.ClickedItem as IMfFileInfo;
            if(null!=file && file.Ready)
            {
                CmLog.debug("Select: {0} - {1}", file.Label, file.DurationText);
            }
        }
    }

    #endregion

    #region IMfFileInfo

    /**
     * GridViewに表示するアイテムが実装すべきi/f
     */
    public interface IMfFileInfo
    {
        string Label { get; }
        string DurationText { get; }
        ImageSource Thumbnail { get; }
        bool Ready { get; }
    }

    #endregion

    #region MfFileList

    /**
     * GridViewのデータソース
     */
    public class MfFileList : ObservableCollection<IMfFileInfo>, ISupportIncrementalLoading
    {
        private WeakReference<DependencyObject> mOwner;
        private CoreDispatcher Dispatcher { get => mOwner.GetTarget()?.Dispatcher; }
        private object mLocker;

        /**
         * コンストラクタ
         */
        public MfFileList(DependencyObject owner)
        {
            mOwner = new WeakReference<DependencyObject>(owner);
            mLocker = new object();
        }

        public int MaxCount { get; set; } = 100;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var op = AsyncInfo.Run<LoadMoreItemsResult>((token) =>
            {
                return Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    await Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        for (uint i = 0; i < count; i++)
                        {
                            Insert(Count-1, new MfFileInfo());
                            if(Count==MaxCount+1)
                            {
                                count = i+1;
                                RemoveAt(Count - 1);
                                break;
                            }
                        }
                    });
                    var r = new LoadMoreItemsResult();
                    r.Count = count;
                    return r;
                });

            });
            return op;
        }

        public bool HasMoreItems => Count<MaxCount;
    }

    #endregion

    #region WaitingItem

    /**
     * グリッドビューの最後のアイテムとしてセットしておく、ぐるぐるアイテム
     */
    class MfWaitingItem : IMfFileInfo
    {
        public string Label => "";

        public string DurationText => "";

        public ImageSource Thumbnail => null;

        public bool Ready { get => false; }
    }

    #endregion

    #region MfFileInfo

    /**
     * グリッドビューのアイテム
     */
    class MfFileInfo : IMfFileInfo
    {
        public string Label { get; private set; }

        public string DurationText { get; private set; }

        public ImageSource Thumbnail { get; private set; }

        public bool Ready { get => true; }

        public static int sCount = 0;
        public MfFileInfo()
        {
            Label = String.Format("Label-{0}", sCount);
            DurationText = String.Format("Range-{0}", sCount);
            Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/SplashScreen.scale-200.png"));
            sCount++;
        }
    }
    #endregion

}
