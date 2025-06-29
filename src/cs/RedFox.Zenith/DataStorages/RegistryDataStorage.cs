using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RedFox.Zenith.LicenseStorages
{
    [SupportedOSPlatform("windows")]
    public class RegistryDataStorage(string subKey, string salt) : IDataStorage
    {
        /// <inheritdoc/>
        public void StoreData(string key, string value, bool encrypted)
        {
            using var regKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\{subKey}");

            if (encrypted)
            {
                using var aesAlg = Aes.Create();

                var passwordBytes = Rfc2898DeriveBytes.Pbkdf2(subKey, Encoding.Unicode.GetBytes(salt), 100, HashAlgorithmName.SHA512, 48);

                aesAlg.Mode = CipherMode.CBC;
                aesAlg.BlockSize = 128;
                aesAlg.FeedbackSize = 128;
                aesAlg.KeySize = 128;
                aesAlg.Key = passwordBytes[..32];
                aesAlg.IV = passwordBytes[32..];

                value = Convert.ToBase64String(aesAlg.EncryptCbc(Encoding.Unicode.GetBytes(value), aesAlg.IV));
            }

            regKey.SetValue(key, value, RegistryValueKind.String);
        }

        /// <inheritdoc/>
        public string RetrieveData(string key, string defaultValue, bool encrypted)
        {
            using var regKey = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\{subKey}");

            if (regKey is null || regKey.GetValue(key) is not string value)
                return defaultValue;

            if (encrypted)
            {
                using var aesAlg = Aes.Create();

                var passwordBytes = Rfc2898DeriveBytes.Pbkdf2(subKey, Encoding.Unicode.GetBytes(salt), 100, HashAlgorithmName.SHA512, 48);

                aesAlg.Mode = CipherMode.CBC;
                aesAlg.BlockSize = 128;
                aesAlg.FeedbackSize = 128;
                aesAlg.KeySize = 128;
                aesAlg.Key = passwordBytes[..32];
                aesAlg.IV = passwordBytes[32..];

                value = Encoding.Unicode.GetString(aesAlg.DecryptCbc(Convert.FromBase64String(value), aesAlg.IV));
            }

            return value;
        }
    }
}
