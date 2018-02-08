using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wvv
{
    public delegate bool IWvvProgress<SenderType>(SenderType sender, double percent);
}
