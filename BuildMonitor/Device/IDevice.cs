namespace WebMessage.Device
{
    /// <summary>
    /// Device information for the device to monitor.
    /// </summary>
    public interface IDevice
    {
        /// <summary>
        /// Can be the host name or the IP address.
        /// </summary>
        string HostName { get; set; }

        /// <summary>
        /// The host IP address.
        /// </summary>
        string IPAddress { get; set; }

        /// <summary>
        /// The port to connect to.
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// The pairing key received from the first pairing.
        /// </summary>
        string PairingKey { get; set; }
    }
}

