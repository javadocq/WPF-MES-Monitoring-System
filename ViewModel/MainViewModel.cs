using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using WPF_MES_Monitoring_System.Model;
using WPF_MES_Monitoring_System.ViewModel.Command;

namespace WPF_MES_Monitoring_System.ViewModel
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        // UI에 바인딩 될 로그 컬렉션(즉시 반영)
        private ObservableCollection<MachineLog> logs;

        public ObservableCollection<MachineLog> Logs
        {
            get { return logs; }
            set
            {
                logs = value;
                OnPropertyChanged();
            }
        }

        // 버튼 클릭 시 동작할 Command
        public RelayCommand AddLogCommand { get; }

        // 시뮬레이션 데이터
        private string[] machines = { "CNC-01", "PRESS-02", "ROBOT-03", "PACK-04"};
        private Random random = new Random();

        public MainViewModel()
        {
            Logs = new ObservableCollection<MachineLog>();
            AddLogCommand = new RelayCommand(GenerateRandomLog);

            // 초기 데이터 로드
            LoadDataFromDb();
        }

        // 랜덤 로그 생성
        private void GenerateRandomLog()
        {
            var newLog = new MachineLog
            {
                Timestamp = DateTime.Now,
                MachineName = machines[random.Next(machines.Length)],
                Temperature = random.Next(20, 100),
                Status = random.Next(10) > 8 ? "ERROR" : "RUN",
                LogMessage = "Simulated log message"
            };

            using(var conn = new SQLite.SQLiteConnection(App.databasePath))
            {
                conn.CreateTable<MachineLog>();
                conn.Insert(newLog);
            }

            Logs.Insert(0, newLog);

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(RunningCount));
            OnPropertyChanged(nameof(ErrorCount));

        }

        public int TotalCount
        {
            get { return Logs.Count; }
        }

        // 가동 중인 로그 개수 계산
        public int RunningCount
        {
            get { return Logs.Count(log => log.Status == "RUN"); }
        }

        // 에러 상태인 로그 개수 계산
        public int ErrorCount
        {
            get { return Logs.Count(log => log.Status == "ERROR"); }
        }

        private void LoadDataFromDb()
        {
            using (var conn = new SQLite.SQLiteConnection(App.databasePath))
            {
                conn.CreateTable<MachineLog>();

                var logList = conn.Table<MachineLog>().OrderByDescending(l => l.Timestamp).ToList();
                Logs = new ObservableCollection<MachineLog>(logList);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
