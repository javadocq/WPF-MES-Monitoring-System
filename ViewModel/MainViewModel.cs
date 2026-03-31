using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WPF_MES_Monitoring_System.Model;
using WPF_MES_Monitoring_System.ViewModel.Command;
using WPF_MES_Monitoring_System.ViewModel.Service;

namespace WPF_MES_Monitoring_System.ViewModel
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        // --- CNC-01 (Port 502) ---
        private double _cnc01Temp;
        public double Cnc01_Temp
        {
            get => _cnc01Temp;
            set { _cnc01Temp = value; OnPropertyChanged(); OnPropertyChanged(nameof(Cnc01_TempColor)); }
        }
        private double _cnc01Press;
        public double Cnc01_Press
        {
            get => _cnc01Press;
            set { _cnc01Press = value; OnPropertyChanged(); }
        }
        private string _cnc01Status = STATUS_OFF;
        public string Cnc01_Status
        {
            get => _cnc01Status;
            set { _cnc01Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Cnc01_StatusColor)); }
        }
        public Brush Cnc01_StatusColor => Cnc01_Status == STATUS_ON ? Brushes.LimeGreen : Brushes.Crimson;
        public Brush Cnc01_TempColor => Cnc01_Temp >= 400 ? Brushes.Crimson : Cnc01_Temp >= 350 ? Brushes.Orange : Brushes.Black;


        // --- PRESS-02 (Port 503) ---
        private double _press02Temp;
        public double Press02_Temp
        {
            get => _press02Temp;
            set { _press02Temp = value; OnPropertyChanged(); OnPropertyChanged(nameof(Press02_TempColor)); }
        }
        private double _press02Press;
        public double Press02_Press
        {
            get => _press02Press;
            set { _press02Press = value; OnPropertyChanged(); }
        }
        private string _press02Status = STATUS_OFF;
        public string Press02_Status
        {
            get => _press02Status;
            set { _press02Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Press02_StatusColor)); }
        }
        public Brush Press02_StatusColor => Press02_Status == STATUS_ON ? Brushes.LimeGreen : Brushes.Crimson;
        public Brush Press02_TempColor => Press02_Temp >= 400 ? Brushes.Crimson : Press02_Temp >= 350 ? Brushes.Orange : Brushes.Black;


        // --- ROBOT-03 (Port 504) ---
        private double _robot03Temp;
        public double Robot03_Temp
        {
            get => _robot03Temp;
            set { _robot03Temp = value; OnPropertyChanged(); OnPropertyChanged(nameof(Robot03_TempColor)); }
        }
        private double _robot03Press;
        public double Robot03_Press
        {
            get => _robot03Press;
            set { _robot03Press = value; OnPropertyChanged(); }
        }
        private string _robot03Status = STATUS_OFF;
        public string Robot03_Status
        {
            get => _robot03Status;
            set { _robot03Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Robot03_StatusColor)); }
        }
        public Brush Robot03_StatusColor => Robot03_Status == STATUS_ON ? Brushes.LimeGreen : Brushes.Crimson;
        public Brush Robot03_TempColor => Robot03_Temp >= 400 ? Brushes.Crimson : Robot03_Temp >= 350 ? Brushes.Orange : Brushes.Black;


        // --- PACK-04 (Port 505) ---
        private double _pack04Temp;
        public double Pack04_Temp
        {
            get => _pack04Temp;
            set { _pack04Temp = value; OnPropertyChanged(); OnPropertyChanged(nameof(Pack04_TempColor)); }
        }
        private double _pack04Press;
        public double Pack04_Press
        {
            get => _pack04Press;
            set { _pack04Press = value; OnPropertyChanged(); }
        }
        private string _pack04Status = STATUS_OFF;
        public string Pack04_Status
        {
            get => _pack04Status;
            set { _pack04Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Pack04_StatusColor)); }
        }
        public Brush Pack04_StatusColor => Pack04_Status == STATUS_ON ? Brushes.LimeGreen : Brushes.Crimson;
        public Brush Pack04_TempColor => Pack04_Temp >= 400 ? Brushes.Crimson : Pack04_Temp >= 350 ? Brushes.Orange : Brushes.Black;



        // 불량률 데이터를 담을 시리즈
        public SeriesCollection DefectRateSeries { get; set; }

        private string selectedMachine = ALL_MACHINES;
        private const string ALL_MACHINES = "전체";
        private const string STATUS_ON = "ONLINE";
        private const string STATUS_OFF = "OFFLINE";
        private ICollectionView logView;
        public ICollectionView LogView
        {
            get { return logView; }
        }

        public string SelectedMachine
        {
            get { return selectedMachine; }
            set { 
                selectedMachine = value; 
                OnPropertyChanged(); 
                ApplyFilter(); 
            }
        }


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
        public ActionCommand<string> StartCommand { get; }
        public ActionCommand<string> StopCommand { get; }



        private DispatcherTimer timer;
        public MainViewModel()
        {
            

            InitializeData();
            InitializeChart();
            SetupTimer();

            // 초기 데이터 로드
            LoadDataFromDb();
            UpdateAllStatus(); // 초기 카운트 계산

            StartCommand = new ActionCommand<string>(async (port) => {
                await machineService.ControlMachineAsync(int.Parse(port), true);
                // 버튼 누르자마자 UI 즉시 갱신 (사용자 체감 성능 향상)
                UpdateStatusImmediately(port, STATUS_ON);
            });

            StopCommand = new ActionCommand<string>(async (port) => {
                await machineService.ControlMachineAsync(int.Parse(port), false);
                UpdateStatusImmediately(port, STATUS_OFF);
            });
        }

        private void UpdateStatusImmediately(string port, string status)
        {
            switch (port)
            {
                case "502": Cnc01_Status = status; break;
                case "503": Press02_Status = status; break;
                case "504": Robot03_Status = status; break;
                case "505": Pack04_Status = status; break;
            }
            UpdateAllStatus(); // 상단 요약 카운트 즉시 갱신
        }

        private void InitializeData()
        {
            Logs = new ObservableCollection<MachineLog>();

            logView = CollectionViewSource.GetDefaultView(Logs);
            logView.Filter = FilterLogs; // 필터 조건 함수 연결


        }

        // --- 각 기계별 실시간 차트 데이터 (4세트) ---
        public SeriesCollection Cnc01_Series { get; set; }
        public SeriesCollection Press02_Series { get; set; }
        public SeriesCollection Robot03_Series { get; set; }
        public SeriesCollection Pack04_Series { get; set; }

        private void InitializeChart()
        {
            DefectRateSeries = new SeriesCollection
            {
                new LineSeries { Title = "상세 추세", Values = new ChartValues<ObservableValue>(), Stroke = Brushes.Crimson }
            };

            // 불량률 차트 초기화
            Cnc01_Series = CreateMiniSeries(Brushes.OrangeRed);
            Press02_Series = CreateMiniSeries(Brushes.DodgerBlue);
            Robot03_Series = CreateMiniSeries(Brushes.LimeGreen);
            Pack04_Series = CreateMiniSeries(Brushes.MediumPurple);
        }

        private SeriesCollection CreateMiniSeries(Brush color)
        {
            return new SeriesCollection
                {
                    new LineSeries
                    {
                        Values = new ChartValues<ObservableValue>(),
                        Stroke = color,
                        Fill = Brushes.Transparent,
                        PointGeometry = null,
                        StrokeThickness = 2
                    }
                };
        }

        // 전체 가동 시간 및 개별 기계 가동 시간 측정
        private double _totalTickCount = 0;
        private double _cnc01OnlineTick = 0;
        private double _press02OnlineTick = 0;
        private double _robot03OnlineTick = 0;
        private double _pack04OnlineTick = 0;

        private double _cnc01Rate;
        public double Cnc01_Rate { get => _cnc01Rate; set { _cnc01Rate = value; OnPropertyChanged(); } }

        private double _press02Rate;
        public double Press02_Rate { get => _press02Rate; set { _press02Rate = value; OnPropertyChanged(); } }

        private double _robot03Rate;
        public double Robot03_Rate { get => _robot03Rate; set { _robot03Rate = value; OnPropertyChanged(); } }

        private double _pack04Rate;
        public double Pack04_Rate { get => _pack04Rate; set { _pack04Rate = value; OnPropertyChanged(); } }

        private void UpdateUtilizationRates()
        {

            bool isAnyDataReceived = !string.IsNullOrEmpty(Cnc01_Status) ||
                             !string.IsNullOrEmpty(Press02_Status) ||
                             !string.IsNullOrEmpty(Robot03_Status) ||
                             !string.IsNullOrEmpty(Pack04_Status);

            if (!isAnyDataReceived) return;

            _totalTickCount++;

            if (Cnc01_Status == STATUS_ON) _cnc01OnlineTick++;
            if (Press02_Status == STATUS_ON) _press02OnlineTick++;
            if (Robot03_Status == STATUS_ON) _robot03OnlineTick++;
            if (Pack04_Status == STATUS_ON) _pack04OnlineTick++;

            // 가동률 계산 (소수점 1자리)
            Cnc01_Rate = Math.Round((_cnc01OnlineTick / _totalTickCount) * 100, 1);
            Press02_Rate = Math.Round((_press02OnlineTick / _totalTickCount) * 100, 1);
            Robot03_Rate = Math.Round((_robot03OnlineTick / _totalTickCount) * 100, 1);
            Pack04_Rate = Math.Round((_pack04OnlineTick / _totalTickCount) * 100, 1);
        }
        private void SetupTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // 1초마다 실행
            timer.Tick += Timer_Tick; // 생성 로그를 timer.Tick 이벤트에 연결
            timer.Start();
        }
        private void UpdateAllStatus()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(RunningCount));
            OnPropertyChanged(nameof(ErrorCount));
        }

        private IEnumerable<MachineLog> LatestMachineLogs => Logs.GroupBy(x => x.MachineName).Select(g => g.First());

        public int TotalCount => LatestMachineLogs.Count();
        public int RunningCount
        {
            get
            {
                int count = 0;
                if (Cnc01_Status == STATUS_ON) count++;
                if (Press02_Status == STATUS_ON) count++;
                if (Robot03_Status == STATUS_ON) count++;
                if (Pack04_Status == STATUS_ON) count++;
                return count;
            }
        }
        public int ErrorCount => TotalCount - RunningCount;


        private void LoadDataFromDb()
        {
            using (var conn = new SQLite.SQLiteConnection(App.databasePath))
            {
                conn.CreateTable<MachineLog>();

                var logList = conn.Table<MachineLog>().OrderByDescending(l => l.Timestamp).ToList();

                // IObservableCollection을 새로 할당하는 대신 기존 컬렉션을 업데이트하여 UI 바인딩 유지
                // ICollectionView는 기존 컬렉션을 참조하기 때문에, 새로 할당하면 필터링이 깨질 수 있음
                Logs.Clear();
                foreach (var log in logList)
                {
                    Logs.Add(log);
                }
            }
        }

        private MachineService machineService = new MachineService();

        // 장비별 마지막 재시도 시간을 저장
        private Dictionary<string, DateTime> _lastRetryTime = new Dictionary<string, DateTime>();

        private Dictionary<string, bool> _isMachineBusy = new Dictionary<string, bool>();

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var machineConfigs = new[]
            {
                (Name: "CNC-01", Port: 502),
                (Name: "PRESS-02", Port: 503),
                (Name: "ROBOT-03", Port: 504),
                (Name: "PACK-04", Port: 505)
            };
            foreach (var config in machineConfigs)
            {
                // 1. 현재 통신 중인지 체크 (아까 만든 딕셔너리)
                if (_isMachineBusy.TryGetValue(config.Name, out bool busy) && busy) continue;

                // 2. 오프라인 상태라면 '쿨타임' 체크 (예: 5초)
                if (IsMachineOffline(config.Name)) // 현재 상태가 OFFLINE인지 확인하는 로직
                {
                    if (_lastRetryTime.TryGetValue(config.Name, out var lastTime))
                    {
                        if ((DateTime.Now - lastTime).TotalSeconds < 5) continue; // 5초 안 지났으면 패스!
                    }
                }

                // 3. 통신 시도 전 시간 기록
                _lastRetryTime[config.Name] = DateTime.Now;
                _ = ProcessSingleMachineAsync(config.Name, config.Port);
            }
        }

        private async Task ProcessSingleMachineAsync(string name, int port)
        {
            _isMachineBusy[name] = true; // "나 지금 일하러 간다!" 표시
            try
            {
                var newLog = await machineService.GetRealTimeLogAysnc(name, port);

                // UI 업데이트는 오는 즉시 실행 (Dispatcher 활용)
                App.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Insert(0, newLog);
                    if (Logs.Count > 100) Logs.RemoveAt(100);
                    UpdateMachineProperties(newLog);

                    // 가동률 등은 데이터가 하나 올 때마다 갱신하거나, 필요시 호출
                    UpdateUtilizationRates();
                });

                if (newLog.Status == STATUS_ON)
                    machineService.SaveLog(newLog);
            }
            finally
            {
                _isMachineBusy[name] = false; // "나 다 했어, 다음 타이머 때 불러줘!"
            }
        }

        private bool IsMachineOffline(string machineName)
        {
            return machineName switch
            {
                "CNC-01" => Cnc01_Status == STATUS_OFF,
                "PRESS-02" => Press02_Status == STATUS_OFF,
                "ROBOT-03" => Robot03_Status == STATUS_OFF,
                "PACK-04" => Pack04_Status == STATUS_OFF,
                _ => false
            };
        }

        private void UpdateMachineProperties(MachineLog log)
        {
            SeriesCollection? targetSeries = null;

            switch (log.MachineName)
            {
                case "CNC-01":
                    Cnc01_Temp = log.Temperature;
                    Cnc01_Press = log.Pressure;
                    Cnc01_Status = log.Status;
                    targetSeries = Cnc01_Series;
                    break;
                case "PRESS-02":
                    Press02_Temp = log.Temperature;
                    Press02_Press = log.Pressure;
                    Press02_Status = log.Status;
                    targetSeries = Press02_Series;
                    break;
                case "ROBOT-03":
                    Robot03_Temp = log.Temperature;
                    Robot03_Press = log.Pressure;
                    Robot03_Status = log.Status;
                    targetSeries = Robot03_Series;
                    break;
                case "PACK-04":
                    Pack04_Temp = log.Temperature;
                    Pack04_Press = log.Pressure;
                    Pack04_Status = log.Status;
                    targetSeries = Pack04_Series;
                    break;
            }

            // targetSeries가 null이 아니고, 기계가 온라인일 때만 차트 그리기.
            if (targetSeries != null && targetSeries.Count > 0 && log.Status == STATUS_ON)
            {
                var values = targetSeries[0].Values;
                if (values.Count >= 15) values.RemoveAt(0);
                values.Add(new ObservableValue(log.Temperature));
            }
        }

        private void ApplyFilter()
        {
            logView.Refresh(); // 뷰를 새로고침해서 필터 적용
        }

        // 필터링 로직
        private bool FilterLogs(object obj)
        {
            if (obj is MachineLog log)
            {
                // "전체"가 선택된 경우 모든 로그를 보여줌
                if (SelectedMachine == ALL_MACHINES)
                    return true;
                // 선택된 머신 이름과 일치하는 로그만 보여줌
                return log.MachineName == SelectedMachine;
            }
            return false;
        }

        // 콤보박스에 뿌려줄 목록
        public List<string> MachineOptions { get; } = new List<string>{ "전체", "CNC-01", "PRESS-02", "ROBOT-03", "PACK-04" };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
