namespace WebMessage.Device
{
    /// <summary>
    /// Struct representing a build device.
    /// </summary>
    /// <seealso cref="IDevice"/>
    public class WebMessageDevice : IDevice
    {
        #region IDevice

        public string HostName { get; set; }

        public string IPAddress { get; set; }

        public int Port { get; set; }

        public string PairingKey { get; set; }

        #endregion

        public WebMessageDevice()
        {
            HostName = string.Empty;
            IPAddress = string.Empty;
            Port = 0;
            PairingKey = string.Empty;
        }

    }
}

