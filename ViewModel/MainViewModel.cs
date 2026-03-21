using LiveCharts;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
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
        // 불량률 데이터를 담을 시리즈
        public SeriesCollection DefectRateSeries { get; set; }

        private string selectedMachine = ALL_MACHINES;
        private const string ALL_MACHINES = "전체";
        private const string STATUS_RUN = "RUN";
        private const string STATUS_ERROR = "ERROR";
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


        private DispatcherTimer timer;
        public MainViewModel()
        {
            

            InitializeData();
            InitializeChart();
            SetupTimer();

            // 초기 데이터 로드
            LoadDataFromDb();
            UpdateAllStatus(); // 초기 카운트 계산
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
                new LineSeries
                {
                    Title = "불량률 (%)",
                    Values = new ChartValues<double>(),
                    PointGeometry = DefaultGeometries.Circle,
                    Stroke = Brushes.Crimson,
                    Fill = Brushes.Transparent,
                }
            };
            UpdateChartData();
        }

        private void UpdateChartData()
        {
            var targetList = SelectedMachine == ALL_MACHINES
                ? Logs.Take(20).ToList()
                : Logs.Where(log => log.MachineName == SelectedMachine).Take(20).ToList();

            UpdateDefectRate(targetList);
        }

        private void SetupTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2); // 2초마다 실행
            timer.Tick += Timer_Tick; // 생성 로그를 timer.Tick 이벤트에 연결
            timer.Start();
        }
        private void UpdateAllStatus()
        {
            // 매번 중복 연산하지 않도록 최적화 (현재 상태 요약용)
            var latestStatusPerMachine = Logs.GroupBy(x => x.MachineName)
                                             .Select(g => g.First())
                                             .ToList();

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(RunningCount));
            OnPropertyChanged(nameof(ErrorCount));
        }

        private void UpdateDefectRate(List<MachineLog> logList)
        {
            if (logList.Count == 0)
            {
                return;
            }
            double defectCount = logList.Count(log => log.Status == STATUS_ERROR);
            double totalCount = logList.Count;
            double defectRate = (defectCount / totalCount) * 100;

            // 차트 값 (최근 10개만 업데이트)
            if (DefectRateSeries[0].Values.Count > 10)
                DefectRateSeries[0].Values.RemoveAt(0);

            DefectRateSeries[0].Values.Add(defectRate);
        }

        private IEnumerable<MachineLog> LatestMachineLogs => Logs.GroupBy(x => x.MachineName).Select(g => g.First());

        public int TotalCount => LatestMachineLogs.Count();
        public int RunningCount => LatestMachineLogs.Count(x => x.Status == STATUS_RUN);
        public int ErrorCount => LatestMachineLogs.Count(x => x.Status == STATUS_ERROR);


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

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var newLog = machineService.GenerateRandomLog();

            machineService.SaveLog(newLog);

            Logs.Insert(0, newLog);
            // 상태 요약 UI 갱신 알림
            UpdateAllStatus();

            // 차트 데이터 갱신
            UpdateChartData();
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
