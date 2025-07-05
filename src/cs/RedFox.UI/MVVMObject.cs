using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace RedFox.UI
{
    /// <summary>
    /// A base class for UI Items
    /// </summary>
    [SupportedOSPlatform("windows")]
    public abstract class MVVMObject : INotifyPropertyChanged
    {
        protected List<(string, object)> UndoStack { get; } = [];

        /// <summary>
        /// The internal kvps.
        /// </summary>
        protected readonly Dictionary<string, object?> KeyValuePairs = [];

        /// <summary>
        /// The internal kvps.
        /// </summary>
        protected readonly Dictionary<string, List<string>> Binders = [];

        /// <summary>
        /// Property Changed Event Handler
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the value of the given property
        /// </summary>
        protected virtual T GetValue<T>(T defaultValue, [CallerMemberName] string propertyName = "")
        {
            if (KeyValuePairs.TryGetValue(propertyName, out var value) && value is T result)
                return result;
            else
                return defaultValue;
        }

        /// <summary>
        /// Sets the value of the given property
        /// </summary>
        protected virtual void SetValue<T>(T newValue, [CallerMemberName] string propertyName = "")
        {
            KeyValuePairs[propertyName] = newValue;
            NotifyPropertyChanged(propertyName);

            // Check if there's others we need to update
            if (Binders.TryGetValue(propertyName, out var binds))
            {
                foreach (var bind in binds)
                {
                    NotifyPropertyChanged(bind);
                }
            }
        }

        public void AddBinds(string property, params string[] binds)
        {
            if (!Binders.TryGetValue(property, out var currentBinds))
            {
                currentBinds = [];
                Binders[property] = currentBinds;
            }

            currentBinds.AddRange(binds);
        }

        /// <summary>
        /// Notifies that the property has changed
        /// </summary>
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}