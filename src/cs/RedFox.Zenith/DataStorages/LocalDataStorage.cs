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
    public class LocalDataStorage(string filePath, string aesKey, string salt) : IDataStorage
    {
        public Dictionary<string, string>? Read()
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                using var reader = new BinaryReader(File.OpenRead(filePath));

                if (!reader.ReadString().Equals("zenith", StringComparison.CurrentCultureIgnoreCase))
                    return null;

                Dictionary<string, string> results = [];

                var count = reader.ReadInt32() ^ 0x11DAE2F4;

                for (int i = 0; i < count; i++)
                {
                    var key = reader.ReadString();
                    var value = reader.ReadString();

                    results[key] = value;
                }

                return results;
            }
            catch
            {
                return null;
            }
        }
        public void Write(Dictionary<string, string> kvps)
        {
            try
            {
                using var writer = new BinaryWriter(File.Create(filePath));

                writer.Write("zenith");
                writer.Write(kvps.Count ^ 0x11DAE2F4);

                foreach (var (k, v) in kvps)
                {
                    writer.Write(Convert.ToBase64String(Encoding.Unicode.GetBytes(k)));
                    writer.Write(v);
                }
            }
            catch
            {
                return;
            }
        }

        /// <inheritdoc/>
        public void StoreData(string key, string value, bool encrypted)
        {
            var kvps = Read() ?? [];

            if (encrypted)
            {
                using var aesAlg = Aes.Create();

                var passwordBytes = Rfc2898DeriveBytes.Pbkdf2(aesKey, Encoding.Unicode.GetBytes(salt), 100, HashAlgorithmName.SHA512, 48);

                aesAlg.Mode = CipherMode.CBC;
                aesAlg.BlockSize = 128;
                aesAlg.FeedbackSize = 128;
                aesAlg.KeySize = 128;
                aesAlg.Key = passwordBytes[..32];
                aesAlg.IV = passwordBytes[32..];

                value = Convert.ToBase64String(aesAlg.EncryptCbc(Encoding.Unicode.GetBytes(value), aesAlg.IV));
            }

            kvps[key] = value;
            Write(kvps);
        }

        /// <inheritdoc/>
        public string RetrieveData(string key, string defaultValue, bool encrypted)
        {
            var kvps = Read();

            if (kvps is null)
                return defaultValue;
            if (!kvps.TryGetValue(key, out var value))
                return defaultValue;

            if (encrypted)
            {
                using var aesAlg = Aes.Create();

                var passwordBytes = Rfc2898DeriveBytes.Pbkdf2(aesKey, Encoding.Unicode.GetBytes(salt), 100, HashAlgorithmName.SHA512, 48);

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
