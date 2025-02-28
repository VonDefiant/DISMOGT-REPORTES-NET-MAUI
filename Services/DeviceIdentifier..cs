using Microsoft.Maui.Storage;
using System;

namespace DISMOGT_REPORTES.Services
{
    public static class DeviceIdentifier
    {
        private const string Key = "device_unique_id";

        private static readonly Lazy<string> _deviceId = new Lazy<string>(() =>
        {
            var existingId = Preferences.Get(Key, null);
            if (!string.IsNullOrEmpty(existingId))
            {
                return existingId;
            }

            var newId = Guid.NewGuid().ToString();
            Preferences.Set(Key, newId);
            return newId;
        });

        public static string GetOrCreateUniqueId() => _deviceId.Value;
    }
}
