using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Zenith
{
    public enum LicenseStatus
    {
        DataMismatch,
        ActivationSuccess,
        ActivationFailedBadKey,
        NoKeyAvailable,
        Refunded,
        Verified,
        Unverified,
    }
}
