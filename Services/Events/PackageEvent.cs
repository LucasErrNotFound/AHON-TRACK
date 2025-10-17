using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Events
{
    public class PackageEventService
    {
        private static PackageEventService? _instance;
        private static readonly object _lock = new object();

        public static PackageEventService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new PackageEventService();
                    }
                }
                return _instance;
            }
        }

        public event EventHandler? PackagesChanged;

        public void NotifyPackagesChanged()
        {
            PackagesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
