using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Zenith
{
    public interface ILicenseVerifier
    {
        public bool LicenseKeyExists();
        public Task<LicenseStatus> ActivateLicenseAsync(string id, string key);

        public Task<LicenseStatus> CheckLicenseAsync();
    }
}
