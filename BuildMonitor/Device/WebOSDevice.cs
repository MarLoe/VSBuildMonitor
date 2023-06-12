namespace BuildMonitor.Device
{
    /// <summary>
    /// Class representing a build device.
    /// </summary>
    /// <seealso cref="IDevice"/>
    public class BuildDevice : IDevice
    {
        #region IDevice

        public string HostName { get; set; } = string.Empty;

        public string IPAddress { get; set; } = string.Empty;

        public int Port { get; set; } = 0;

        public string PairingKey { get; set; } = string.Empty;

        #endregion

    }
}

