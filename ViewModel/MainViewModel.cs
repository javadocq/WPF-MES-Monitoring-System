using LiveCharts;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WPF_MES_Monitoring_System.Model;
using WPF_MES_Monitoring_System.ViewModel.Command;
using WPF_MES_Monitoring_System.ViewModel.Service;
using LiveCharts.Defaults;

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
        private string _cnc01Status = "OFFLINE";
        public string Cnc01_Status
        {
            get => _cnc01Status;
            set { _cnc01Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Cnc01_StatusColor)); }
        }
        public Brush Cnc01_StatusColor => Cnc01_Status == "ONLINE" ? Brushes.LimeGreen : Brushes.Crimson;
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
        private string _press02Status = "OFFLINE";
        public string Press02_Status
        {
            get => _press02Status;
            set { _press02Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Press02_StatusColor)); }
        }
        public Brush Press02_StatusColor => Press02_Status == "ONLINE" ? Brushes.LimeGreen : Brushes.Crimson;
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
        private string _robot03Status = "OFFLINE";
        public string Robot03_Status
        {
            get => _robot03Status;
            set { _robot03Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Robot03_StatusColor)); }
        }
        public Brush Robot03_StatusColor => Robot03_Status == "ONLINE" ? Brushes.LimeGreen : Brushes.Crimson;
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
        private string _pack04Status = "OFFLINE";
        public string Pack04_Status
        {
            get => _pack04Status;
            set { _pack04Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(Pack04_StatusColor)); }
        }
        public Brush Pack04_StatusColor => Pack04_Status == "ONLINE" ? Brushes.LimeGreen : Brushes.Crimson;
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
                UpdateChartData();
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
            });

            StopCommand = new ActionCommand<string>(async (port) => {
                await machineService.ControlMachineAsync(int.Parse(port), false);
            });
        }

        private void InitializeData()
        {
            Logs = new ObservableCollection<MachineLog>();

            logView = CollectionViewSource.GetDefaultView(Logs);
            logView.Filter = FilterLogs; // 필터 조건 함수 연결


        }

        private void InitializeChart()
        {
            // 불량률 차트 초기화
            DefectRateSeries = new SeriesCollection
            {
                new LineSeries { Title = "CNC-01", Values = new ChartValues<ObservableValue>(), Stroke = Brushes.OrangeRed, Fill = Brushes.Transparent },
                new LineSeries { Title = "PRESS-02", Values = new ChartValues<ObservableValue>(), Stroke = Brushes.DodgerBlue, Fill = Brushes.Transparent },
                new LineSeries { Title = "ROBOT-03", Values = new ChartValues<ObservableValue>(), Stroke = Brushes.Green, Fill = Brushes.Transparent },
                new LineSeries { Title = "PACK-04", Values = new ChartValues<ObservableValue>(), Stroke = Brushes.Purple, Fill = Brushes.Transparent }
            };
            UpdateChartData();
        }

        private void UpdateChartData()
        {
            var targetList = SelectedMachine == ALL_MACHINES
                ? Logs.Take(20).ToList()
                : Logs.Where(log => log.MachineName == SelectedMachine).Take(20).ToList();

            var currentData = Logs.GroupBy(l => l.MachineName)
                                  .Select(g => g.First())
                                  .ToList();

            foreach(var log in currentData)
            {
                // 기계 이름에 맞는 시리즈 인덱스 찾기
                int seriesIndex = log.MachineName switch
                {
                    "CNC-01" => 0,
                    "PRESS-02" => 1,
                    "ROBOT-03" => 2,
                    "PACK-04" => 3,
                    _ => -1
                };

                if(seriesIndex != -1)
                {
                    var targetSeries = DefectRateSeries[seriesIndex].Values;

                    if(targetSeries.Count >= 20)
                    {
                        targetSeries.RemoveAt(0);
                    }
                    targetSeries.Add(new ObservableValue(log.Temperature));
                }
            }

            UpdateTemperate(targetList);
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

        private void UpdateTemperate(List<MachineLog> logList)
        {
            if (logList.Count == 0)
            {
                return;
            }
            double TemperLog = logList.First().Temperature;

            // 차트 값 (최근 10개만 업데이트)
            if (DefectRateSeries[0].Values.Count > 10)
                DefectRateSeries[0].Values.RemoveAt(0);

            DefectRateSeries[0].Values.Add(new ObservableValue(TemperLog));
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

        private async void Timer_Tick(object? sender, EventArgs e)
        {

            var tasks = new List<Task<MachineLog>>
            {
                machineService.GetRealTimeLogAysnc("CNC-01", 502),
                machineService.GetRealTimeLogAysnc("PRESS-02", 503),
                machineService.GetRealTimeLogAysnc("ROBOT-03", 504),
                machineService.GetRealTimeLogAysnc("PACK-04", 505)
            };

            // 모든 응답이 올 때까지 기다림
            var results = await Task.WhenAll(tasks);

            foreach (var newLog in results)
            {
                if (newLog.Status == STATUS_ON)
                {
                    machineService.SaveLog(newLog);

                    App.Current.Dispatcher.Invoke(() => {
                        Logs.Insert(0, newLog);
                        if (Logs.Count > 100) Logs.RemoveAt(100);
                    });
                }
                // 각 기계별 개별 프로퍼티(카드 UI) 업데이트
                UpdateMachineProperties(newLog);

            }

            // 상태 요약 UI 갱신 알림
            UpdateAllStatus();

            if (results.Any(r => r.Status == STATUS_ON))
            {
                UpdateChartData();
            }

        }

        private void UpdateMachineProperties(MachineLog log)
        {
            switch (log.MachineName)
            {
                case "CNC-01":
                    Cnc01_Temp = log.Temperature;
                    Cnc01_Press = log.Pressure;
                    Cnc01_Status = log.Status;
                    break;
                case "PRESS-02":
                    Press02_Temp = log.Temperature;
                    Press02_Press = log.Pressure;
                    Press02_Status = log.Status;
                    break;
                case "ROBOT-03":
                    Robot03_Temp = log.Temperature;
                    Robot03_Press = log.Pressure;
                    Robot03_Status = log.Status;
                    break;
                case "PACK-04":
                    Pack04_Temp = log.Temperature;
                    Pack04_Press = log.Pressure;
                    Pack04_Status = log.Status;
                    break;
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
