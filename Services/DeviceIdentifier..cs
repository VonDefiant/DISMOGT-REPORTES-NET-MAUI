using Microsoft.Maui.Storage;

namespace DISMOGT_REPORTES.Services
{
    public static class DeviceIdentifier
    {
        private const string Key = "device_unique_id";
        public static string GetOrCreateUniqueId()
        {
            if (Preferences.ContainsKey(Key))
            {
                return Preferences.Get(Key, "ID_NO_DISPONIBLE");
            }
            else
            {
                var newId = Guid.NewGuid().ToString();
                Preferences.Set(Key, newId);
                return newId;
            }
        }
    }
}
