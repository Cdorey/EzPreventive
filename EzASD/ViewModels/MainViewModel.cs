using AdonisUI.ViewModels;
using EzASD.Data;
using EzASD.Data.Entities;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace EzASD.ViewModels
{
    public class MainViewModel : PropertyChangedBase
    {
        private readonly RecordsContext _recordsContext;
        private ChildViewModel childViewModel;
        private Chat23ViewModel chat23ViewModel;

        private static string BirthDayToCurrentAgeConvert(DateTime birthDate)
        {
            var year = DateTime.Today.Year - birthDate.Year;
            var month = DateTime.Today.Month - birthDate.Month;
            var day = DateTime.Today.Day - birthDate.Day;
            var calculator = year * 12 + month;
            if (day < 0)
            {
                calculator -= 1;
            }
            return calculator switch
            {
                (>= 72) => "6y",
                (>= 60) => "5y",
                (>= 48) => "4y",
                (>= 36) => "36m",
                (>= 30) => "30m",
                (>= 24) => "24m",
                (>= 18) => "18m",
                (>= 12) => "12m",
                (>= 8) => "8m",
                (>= 6) => "6m",
                (>= 3) => "3m",
                _ => string.Empty,
            };
        }

        private void SavePositiveSignRecords()
        {
            try
            {
                //当前的阳性记录
                var x = EarlyWarningViewModel.SaveRecords().ToList();
                //数据库中的阳性记录
                var currentRecordsInDb = _recordsContext.PositiveSignRecords!.Where(x => x.ChildId == ChildViewModel.Id).ToArray();
                foreach (var record in currentRecordsInDb)
                {
                    if (x.Contains(record.PositiveQuestion))
                    {
                        x.Remove(record.PositiveQuestion);
                    }
                    else
                    {
                        _recordsContext.Remove(record);
                    }
                }

                if (x.Any())
                {
                    x.ForEach(newRec => _recordsContext.Add(new PositiveSignRecord { ChildId = ChildViewModel.Id, Date = DateTime.Now, PositiveQuestion = newRec }));
                }
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                _recordsContext.SaveChanges();
            }
        }

        private void SaveChat23aRecord()
        {
            try
            {
                var record = chat23ViewModel.ToChat23ARecord();
                record.ChildId = ChildViewModel.Id;
                _recordsContext.Add(record);
            }
            finally
            {
                _recordsContext.SaveChanges();
            }
        }

        public ChildViewModel ChildViewModel
        {
            get => childViewModel;
            private set
            {
                childViewModel = value;
                childViewModel.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(ChildViewModel.BirthDate))
                    {
                        EarlyWarningViewModel.CurrentAge = BirthDayToCurrentAgeConvert(ChildViewModel.BirthDate);
                    }
                };
            }
        }

        public EarlyWarningViewModel EarlyWarningViewModel { get; private set; }

        public Chat23ViewModel Chat23ViewModel
        {
            get => chat23ViewModel;
            private set
            {
                chat23ViewModel = value;
            }
        }

        public ObservableCollection<Child> History { get; }


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public MainViewModel(RecordsContext recordsContext)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        {
            _recordsContext = recordsContext;
            EarlyWarningViewModel = new();
            var child = new Child();
            _recordsContext.Add(child);
            _recordsContext.SaveChanges();
            ChildViewModel = new(child);
            Chat23ViewModel = new();
            History = new ObservableCollection<Child>(_recordsContext.Children!);
        }
    }
}
