using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Zenith
{
    public interface IDataStorage
    {
        public void StoreData(string key, string value, bool encrypt);

        public string RetrieveData(string key, string defaultValue, bool encrypted);
    }
}
