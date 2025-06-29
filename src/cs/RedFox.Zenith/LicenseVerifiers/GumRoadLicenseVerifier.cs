using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using RedFox.Zenith;

namespace RedFox.Zenith.LicenseVerifiers
{
    public class GumRoadLicenseVerifier(IDataStorage licenseStorage, HttpClient httpClient) : ILicenseVerifier
    {
        /// <summary>
        /// Our mechanism for storing license information.
        /// </summary>
        private readonly IDataStorage _licenseStorage = licenseStorage;

        private readonly HttpClient _httpClient = httpClient;

        private const string GumroadApiEndpoint = "https://api.gumroad.com/v2/licenses/verify";

        /// <inheritdoc/>
        public async Task<LicenseStatus> ActivateLicenseAsync(string id, string key)
        {
            var response = await _httpClient.PostAsync($"{GumroadApiEndpoint}?product_id={id}&license_key={key}", null);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return LicenseStatus.ActivationFailedBadKey;

            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson = JsonDocument.Parse(responseString);

            // TODO: Bundle this into a helper method.
            if(responseJson.RootElement.GetProperty("success").GetBoolean())
            {
                var purchaseElement = responseJson.RootElement.GetProperty("purchase");

                if (purchaseElement.GetProperty("product_id").GetString() != id)
                    return LicenseStatus.DataMismatch;
                if (purchaseElement.GetProperty("license_key").GetString() != key)
                    return LicenseStatus.DataMismatch;

                _licenseStorage.StoreData("ZenithData", key, true);
                _licenseStorage.StoreData("ZenithProd", id, true);
                _licenseStorage.StoreData("ZenithDate", DateTime.Now.AddDays(30).ToString(), true);
                _licenseStorage.StoreData("ZenithUser", purchaseElement.GetProperty("email").GetString() ?? string.Empty, true);

                return LicenseStatus.ActivationSuccess;
            }
            else
            {
                return LicenseStatus.ActivationFailedBadKey;
            }
        }

        /// <inheritdoc/>
        public async Task<LicenseStatus> CheckLicenseAsync()
        {
            var licenseKey = _licenseStorage.RetrieveData("ZenithData", "None", true);

            if (licenseKey.Equals("none", StringComparison.CurrentCultureIgnoreCase))
                return LicenseStatus.NoKeyAvailable;

            var productID = _licenseStorage.RetrieveData("ZenithProd", "None", true);

            var response = await _httpClient.PostAsync($"{GumroadApiEndpoint}?product_id={productID}&license_key={licenseKey}&increment_uses_count=false", null);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return LicenseStatus.NoKeyAvailable;

            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson = JsonDocument.Parse(responseString);

            if (responseJson.RootElement.GetProperty("success").GetBoolean())
            {
                var purchaseElement = responseJson.RootElement.GetProperty("purchase");

                if (purchaseElement.GetProperty("product_id").GetString() != productID)
                    return LicenseStatus.DataMismatch;
                if (purchaseElement.GetProperty("license_key").GetString() != licenseKey)
                    return LicenseStatus.DataMismatch;

                _licenseStorage.StoreData("ZenithData", licenseKey, true);
                _licenseStorage.StoreData("ZenithProd", productID, true);
                _licenseStorage.StoreData("ZenithDate", DateTime.Now.AddDays(30).ToString(), true);
                _licenseStorage.StoreData("ZenithUser", purchaseElement.GetProperty("email").GetString() ?? string.Empty, true);

                return LicenseStatus.Verified;
            }
            else
            {
                return LicenseStatus.Unverified;
            }
        }

        /// <inheritdoc/>
        public bool LicenseKeyExists() => !_licenseStorage.RetrieveData("ZenithData", "None", true).Equals("none", StringComparison.CurrentCultureIgnoreCase);
    }
}