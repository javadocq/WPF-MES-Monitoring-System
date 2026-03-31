using Modbus.Device;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace WPF_MES_Monitoring_System.Model
{
    public class MachineConnection : IDisposable
    {
        public TcpClient Client { get; set; }
        public IModbusMaster Master { get; set; }
        public string Name { get; set; }
        public int Port { get; set; }
        public bool IsConnected => Client != null && Client.Connected;

        public void Dispose()
        {
            Master?.Dispose();
            Client?.Close();
            Client?.Dispose();
        }
    }
}
