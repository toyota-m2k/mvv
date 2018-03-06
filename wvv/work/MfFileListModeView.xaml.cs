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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace wvv.work
{

    public enum MfOrderBy
    {
        CreationDate,
        RegistrationDate,
        Duration,
        Size
    }

    public class MfOrderByConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {

            string ParameterString = parameter as string;
            if (ParameterString == null)
            {
                return DependencyProperty.UnsetValue;
            }

            if (Enum.IsDefined(value.GetType(), value) == false)
            {
                return DependencyProperty.UnsetValue;
            }

            object paramvalue = Enum.Parse(value.GetType(), ParameterString);

            if (paramvalue.Equals(value))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (!(bool)value)
            {
                return DependencyProperty.UnsetValue;
            }
            string ParameterString = parameter as string;
            if (ParameterString == null)
            {
                return DependencyProperty.UnsetValue;
            }

            return Enum.Parse(typeof(MfOrderBy), ParameterString);
        }
    }

    public sealed partial class MfFileListModeView : UserControl, INotifyPropertyChanged
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

        public MfFileListModeView()
        {
            this.InitializeComponent();
            this.DataContext = this;
        }

        private readonly string[] mOrderByProps = new string[] { "OrderBy", "IsOrderByDate" };
        private MfOrderBy mOrderBy = MfOrderBy.CreationDate;
        public MfOrderBy OrderBy
        {
            get => mOrderBy;
            set => setProp(mOrderByProps, ref mOrderBy, value);
        }

        private bool mAscendant = false;
        public bool Ascendant
        {
            get => mAscendant;
            set => setProp("Ascendant", ref mAscendant, value);
        }

        public bool IsOrderByDate
        {
            get => OrderBy == MfOrderBy.CreationDate || OrderBy == MfOrderBy.RegistrationDate;
        }
    }
}
