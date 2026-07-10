using System.Globalization;
using OOFSponderModern.Models;

namespace OOFSponderModern.ViewModels;

public sealed class ScheduleDayViewModel : ViewModelBase
{
    private static readonly TimeSpan TimeStep = TimeSpan.FromMinutes(30);
    private readonly ScheduleDay _model;
    private readonly Action _onChanged;
    private readonly Func<bool> _isLinkedTimeAdjustmentEnabled;

    public ScheduleDayViewModel(ScheduleDay model, Action onChanged, Func<bool> isLinkedTimeAdjustmentEnabled)
    {
        _model = model;
        _onChanged = onChanged;
        _isLinkedTimeAdjustmentEnabled = isLinkedTimeAdjustmentEnabled;
        MoveStartEarlierCommand = new RelayCommand(() => ShiftStartTime(-TimeStep));
        MoveStartLaterCommand = new RelayCommand(() => ShiftStartTime(TimeStep));
        MoveEndEarlierCommand = new RelayCommand(() => ShiftEndTime(-TimeStep));
        MoveEndLaterCommand = new RelayCommand(() => ShiftEndTime(TimeStep));
    }

    public ScheduleDay Model => _model;

    public RelayCommand MoveStartEarlierCommand { get; }
    public RelayCommand MoveStartLaterCommand { get; }
    public RelayCommand MoveEndEarlierCommand { get; }
    public RelayCommand MoveEndLaterCommand { get; }

    public string DayName => CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(_model.DayOfWeek);

    public bool IsOffWork
    {
        get => _model.IsOffWork;
        set
        {
            if (_model.IsOffWork == value)
            {
                return;
            }

            _model.IsOffWork = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWorking));
            OnPropertyChanged(nameof(VisualSummary));
            _onChanged();
        }
    }

    public bool IsWorking => !IsOffWork;

    public string StartTimeText
    {
        get => _model.StartTime.ToString(@"hh\:mm", CultureInfo.CurrentCulture);
        set
        {
            if (!TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out var parsed))
            {
                return;
            }

            parsed = new TimeSpan(parsed.Hours, parsed.Minutes, 0);
            var delta = parsed - _model.StartTime;
            if (_model.StartTime == parsed)
            {
                return;
            }

            _model.StartTime = parsed;
            if (_isLinkedTimeAdjustmentEnabled())
            {
                _model.EndTime = NormalizeTime(_model.EndTime + delta);
                OnPropertyChanged(nameof(EndTimeText));
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(VisualSummary));
            _onChanged();
        }
    }

    public string EndTimeText
    {
        get => _model.EndTime.ToString(@"hh\:mm", CultureInfo.CurrentCulture);
        set
        {
            if (!TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out var parsed))
            {
                return;
            }

            parsed = new TimeSpan(parsed.Hours, parsed.Minutes, 0);
            var delta = parsed - _model.EndTime;
            if (_model.EndTime == parsed)
            {
                return;
            }

            _model.EndTime = parsed;
            if (_isLinkedTimeAdjustmentEnabled())
            {
                _model.StartTime = NormalizeTime(_model.StartTime + delta);
                OnPropertyChanged(nameof(StartTimeText));
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(VisualSummary));
            _onChanged();
        }
    }

    public string VisualSummary => IsOffWork ? "Off work / OOF" : $"Working {StartTimeText}-{EndTimeText}";

    private Task ShiftStartTime(TimeSpan delta)
    {
        _model.StartTime = NormalizeTime(_model.StartTime + delta);
        if (_isLinkedTimeAdjustmentEnabled())
        {
            _model.EndTime = NormalizeTime(_model.EndTime + delta);
            OnPropertyChanged(nameof(EndTimeText));
        }

        OnPropertyChanged(nameof(StartTimeText));
        OnPropertyChanged(nameof(VisualSummary));
        _onChanged();
        return Task.CompletedTask;
    }

    private Task ShiftEndTime(TimeSpan delta)
    {
        _model.EndTime = NormalizeTime(_model.EndTime + delta);
        if (_isLinkedTimeAdjustmentEnabled())
        {
            _model.StartTime = NormalizeTime(_model.StartTime + delta);
            OnPropertyChanged(nameof(StartTimeText));
        }

        OnPropertyChanged(nameof(EndTimeText));
        OnPropertyChanged(nameof(VisualSummary));
        _onChanged();
        return Task.CompletedTask;
    }

    private static TimeSpan NormalizeTime(TimeSpan value)
    {
        var ticks = value.Ticks % TimeSpan.FromDays(1).Ticks;
        if (ticks < 0)
        {
            ticks += TimeSpan.FromDays(1).Ticks;
        }

        return new TimeSpan(ticks);
    }
}
