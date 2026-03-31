using Modbus.Device;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using WPF_MES_Monitoring_System.Model;
using System.Threading;

namespace WPF_MES_Monitoring_System.ViewModel.Service
{
    public class MachineService
    {
        // 1. 장비별 연결(TcpClient, ModbusIpMaster)을 담아둘 저장소
        private static readonly ConcurrentDictionary<int, (TcpClient Client, IModbusMaster Master)> _connections
            = new ConcurrentDictionary<int, (TcpClient, IModbusMaster)>();

        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _locks
            = new ConcurrentDictionary<int, SemaphoreSlim>();

        // 포트별 세마포어를 가져오는 헬퍼 메서드
        private SemaphoreSlim GetLock(int port) => _locks.GetOrAdd(port, _ => new SemaphoreSlim(1, 1));
        // 가상 서버에 연결
        public async Task<MachineLog> GetRealTimeLogAysnc(string machineName, int port)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start(); // 측정 시작

            // 통신 시작 전 '내 차례'가 올 때까지 기다립니다.
            var semaphore = GetLock(port);
            await semaphore.WaitAsync();

            try
            {
                // 2. 연결 가져오기 혹은 생성하기
                var (client, master) = await GetOrCreateConnection(port);

                // 실제로는 장비에서 읽어오는 데이터로 로그를 생성해야 하지만, 시뮬레이션을 위해 랜덤 데이터를 사용
                ushort[] registers = await Task.Run(() => master.ReadHoldingRegisters(1, 0, 2)); // 온도와 압력 데이터를 읽어온다고 가정
                bool[] coils = await Task.Run(() => master.ReadCoils(1, 0, 1)); // 장비 상태를 읽어온다고 가정

                stopwatch.Stop();

                return new MachineLog
                {
                    Timestamp = DateTime.Now,
                    MachineName = machineName,
                    Temperature = registers[0],
                    Pressure = registers[1],
                    Status = coils[0] ? "ONLINE" : "OFFLINE",
                    LogMessage = coils[0] ? "Machine is operating normally." : "Machine is offline.",
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };
            } catch(Exception ex)
            {
                stopwatch.Stop();
                // 에러 발생 시(연결 끊김 등) 해당 커넥션 제거하여 다음 호출 때 재연결 유도
                CleanupConnection(port);

                return new MachineLog
                {
                    Timestamp = DateTime.Now,
                    MachineName = machineName,
                    Status = "OFFLINE",
                    LogMessage = $"Error: {ex.Message}"
                };
            } finally
            {
                // 작업이 끝나면 다음 차례를 위해 풀어준다.
                semaphore.Release();
            }
        }

        public async Task ControlMachineAsync(int port, bool start)
        {
            // 버튼 제어도 동일한 세마포어 사용
            var semaphore = GetLock(port);
            await semaphore.WaitAsync();
            try
            {
                var (client, master) = await GetOrCreateConnection(port);
                await Task.Run(() => master.WriteSingleCoil(1, 0, start));
            }
            catch (Exception)
            {
                CleanupConnection(port);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<(TcpClient Client, IModbusMaster Master)> GetOrCreateConnection(int port)
        {
            // 기존 연결이 있고, 실제로 연결된 상태인지 확인
            if (_connections.TryGetValue(port, out var conn) && conn.Client.Connected)
            {
                return conn;
            }

            // 연결이 없거나 끊겼으면 새로 생성
            CleanupConnection(port); // 기존 잔재 제거

            var client = new TcpClient();
            // 로컬 테스트 시 타임아웃을 짧게 주어 '느림' 방지
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            if (await Task.WhenAny(connectTask, Task.Delay(1000)) != connectTask)
            {
                client.Close();
                throw new Exception("Connection Timeout");
            }

            var master = ModbusIpMaster.CreateIp(client);
            var newConn = (client, master);
            _connections[port] = newConn;

            return newConn;
        }

        private void CleanupConnection(int port)
        {
            if (_connections.TryRemove(port, out var conn))
            {
                conn.Master?.Dispose();
                conn.Client?.Close();
                conn.Client?.Dispose();
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
