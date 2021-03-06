using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData.Binding;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceChartTileViewModel : TileViewModel
	{
		private readonly ObservableCollection<HistoryItemViewModel> _history;
		[AutoNotify] private ObservableCollection<double> _yValues;
		[AutoNotify] private ObservableCollection<double> _xValues;
		[AutoNotify] private double? _xMinimum;
		[AutoNotify] private List<string>? _yLabels;
		[AutoNotify] private List<string>? _xLabels;

		public WalletBalanceChartTileViewModel(ObservableCollection<HistoryItemViewModel> history)
		{
			_history = history;
			_yValues = new ObservableCollection<double>();
			_xValues = new ObservableCollection<double>();
			TimePeriodOptions = new ObservableCollection<TimePeriodOptionViewModel>();

			foreach (var item in (TimePeriodOption[]) Enum.GetValues(typeof(TimePeriodOption)))
			{
				TimePeriodOptions.Add(new TimePeriodOptionViewModel(item, UpdateSample)
				{
					IsSelected = item == TimePeriodOption.ThreeMonths
				});
			}
		}

		public ObservableCollection<TimePeriodOptionViewModel> TimePeriodOptions { get; }

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_history.ToObservableChangeSet()
				.Subscribe(_ => UpdateSample(TimePeriodOptions.First(x => x.IsSelected)))
				.DisposeWith(disposables);
		}

		private void UpdateSample(TimePeriodOptionViewModel selectedPeriodOption)
		{
			foreach (var item in TimePeriodOptions)
			{
				item.IsSelected = item == selectedPeriodOption;
			}

			var timePeriod = selectedPeriodOption.Option;

			switch (timePeriod)
			{
				case TimePeriodOption.All:
					if (_history.Any())
					{
						var oldest = _history.Last().Date;

						UpdateSample((DateTimeOffset.Now - oldest) / 125, DateTimeOffset.Now - oldest);
					}

					break;

				case TimePeriodOption.Day:
					UpdateSample(TimeSpan.FromHours(0.5), TimeSpan.FromHours(24));
					break;

				case TimePeriodOption.Week:
					UpdateSample(TimeSpan.FromHours(12), TimeSpan.FromDays(7));
					break;

				case TimePeriodOption.Month:
					UpdateSample(TimeSpan.FromDays(1), TimeSpan.FromDays(30));
					break;

				case TimePeriodOption.ThreeMonths:
					UpdateSample(TimeSpan.FromDays(2), TimeSpan.FromDays(90));
					break;

				case TimePeriodOption.SixMonths:
					UpdateSample(TimeSpan.FromDays(3.5), TimeSpan.FromDays(182.5));
					break;

				case TimePeriodOption.Year:
					UpdateSample(TimeSpan.FromDays(7), TimeSpan.FromDays(365));
					break;
			}
		}

		private void UpdateSample(TimeSpan sampleTime, TimeSpan sampleBackFor)
		{
			var sampleLimit = DateTimeOffset.Now - sampleBackFor;

			XMinimum = sampleLimit.ToUnixTimeMilliseconds();

			var values = _history.SelectTimeSampleBackwards(
				x => x.Date,
				x => x.Balance,
				sampleTime,
				sampleLimit,
				DateTime.Now);

			XValues.Clear();
			YValues.Clear();

			foreach (var (timestamp, balance) in values.Reverse())
			{
				YValues.Add((double)balance.ToDecimal(MoneyUnit.BTC));
				XValues.Add(timestamp.ToUnixTimeMilliseconds());
			}

			if (YValues.Any())
			{
				var maxY = YValues.Max();
				YLabels = new List<string> { "0", (maxY / 2).ToString("F2"), maxY.ToString("F2") };
			}
			else
			{
				YLabels = null;
			}

			if (XValues.Any())
			{
				var minX = XValues.Min();
				var maxX = XValues.Max();
				var halfX = minX + ((maxX - minX) / 2);

				var range = DateTimeOffset.FromUnixTimeMilliseconds((long)maxX) -
							DateTimeOffset.FromUnixTimeMilliseconds((long)minX);

				if (range <= TimeSpan.FromDays(1))
				{
					XLabels = new List<string>
					{
						DateTimeOffset.FromUnixTimeMilliseconds((long) minX).DateTime.ToString("t"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) halfX).DateTime.ToString("t"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) maxX).DateTime.ToString("t"),
					};
				}
				else if (range <= TimeSpan.FromDays(7))
				{
					XLabels = new List<string>
					{
						DateTimeOffset.FromUnixTimeMilliseconds((long) minX).DateTime.ToString("ddd MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) halfX).DateTime.ToString("ddd MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) maxX).DateTime.ToString("ddd MMM-d"),
					};
				}
				else
				{
					XLabels = new List<string>
					{
						DateTimeOffset.FromUnixTimeMilliseconds((long) minX).DateTime.ToString("MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) halfX).DateTime.ToString("MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) maxX).DateTime.ToString("MMM-d"),
					};
				}
			}
			else
			{
				XLabels = null;
			}
		}
	}
}
