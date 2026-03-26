using Modbus.Device;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using WPF_MES_Monitoring_System.Model;

namespace WPF_MES_Monitoring_System.ViewModel.Service
{
    public class MachineService
    {
        // 가상 서버에 연결
        public async Task<MachineLog> GetRealTimeLogAysnc(string machineName, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", port); // 비동기적으로 서버에 연결
                    
                    if(client.Connected)
                    {
                        var master = ModbusIpMaster.CreateIp(client);

                        // 실제로는 장비에서 읽어오는 데이터로 로그를 생성해야 하지만, 시뮬레이션을 위해 랜덤 데이터를 사용
                        ushort[] registers = await Task.Run(() => master.ReadHoldingRegisters(1, 0, 2)); // 온도와 압력 데이터를 읽어온다고 가정
                        bool[] coils = await Task.Run(() => master.ReadCoils(1, 0, 1)); // 장비 상태를 읽어온다고 가정

                        return new MachineLog
                        {
                            Timestamp = DateTime.Now,
                            MachineName = machineName,
                            Temperature = registers[0],
                            Pressure = registers[1],
                            Status = coils[0] ? "ONLINE" : "OFFLINE",
                            LogMessage = coils[0] ? "Machine is operating normally." : "Machine is offline."
                        };
                    }
                    throw new Exception("연결은 되었으나 응답이 없습니다.");
                }
            } catch(Exception ex)
            {
                return new MachineLog
                {
                    Timestamp = DateTime.Now,
                    MachineName = machineName,
                    Status = "OFFLINE",
                    LogMessage = $"Failed to connect: {ex.Message}"
                };
            }
        }

        public async Task ControlMachineAsync(int port, bool start)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", port);
                    if (client.Connected)
                    {
                        var master = ModbusIpMaster.CreateIp(client);
                        // Coil 주소 0번(실제 주소 1번)에 값을 씁니다.
                        await Task.Run(() => master.WriteSingleCoil(1, 0, start));
                    }
                }
            }
            catch (Exception ex)
            {
                // 로그 기록 등 예외 처리
            }
        }


        // 저장
        public void SaveLog(MachineLog log)
        {
            using (var conn = new SQLite.SQLiteConnection(App.databasePath))
            {
                conn.CreateTable<MachineLog>();
                conn.Insert(log);
            }
        }
    }
}
