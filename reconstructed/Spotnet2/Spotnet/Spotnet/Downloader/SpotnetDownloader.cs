using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using GalaSoft.MvvmLight.Threading;
using NLog;
using Pri.LongPath;
using Spotnet.Controls;
using Spotnet.Downloader.ViewModel;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Phuse.NNTP.Net;
using Spotnet.Properties;
using Spotnet.Views;

namespace Spotnet.Downloader;

public class SpotnetDownloader : IDownloader, INotifyPropertyChanged, IDisposable
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private static bool _isWarningAboutDbUpdateClosedByUser;

	private readonly bool _isOnGlobalPause;

	private readonly object _lockStartStop = new object();

	private bool _isInitialized;

	private bool _isUpdateItemsOrderDisabledForMassUpdate;

	private DownloaderItems _items;

	private int _lockShutdownDialogShown;

	private Visibility _spotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility = Visibility.Collapsed;

	private System.Timers.Timer _timerToUpdateTotals;

	private DownloaderTotals _totalItems;

	public DownloaderItems Items => LazyInitializer.EnsureInitialized(ref _items, () => new DownloaderItems());

	public DownloaderTotals TotalItems => LazyInitializer.EnsureInitialized(ref _totalItems, delegate
	{
		_timerToUpdateTotals = new System.Timers.Timer(1000.0)
		{
			AutoReset = true
		};
		_timerToUpdateTotals.Elapsed += delegate
		{
			_totalItems?.UpdateTotal(_isOnGlobalPause);
		};
		_timerToUpdateTotals.Start();
		DownloaderTotals downloaderTotals = new DownloaderTotals(Items);
		downloaderTotals.ProgressChanged += TotalsOnProgressChanged;
		return downloaderTotals;
	});

	public Visibility SpotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility
	{
		get
		{
			return _spotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility;
		}
		set
		{
			if (_spotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility != value)
			{
				_spotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility = value;
				OnPropertyChanged("SpotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility");
			}
		}
	}

	public bool IsStarted { get; private set; }

	public bool IsDownloaderNotAvailable => false;

	public event Action OnDownloaderLoadedFirstTime;

	public event Action ItemsOrderChanged;

	public event Action<object, DownloadStatus> DownloaderStatusChanged;

	public event Action<int> DownloadsProgressChanged;

	public event PropertyChangedEventHandler PropertyChanged;

	public SpotnetDownloader()
	{
		DbUpdater.OnDbUpdateStart += DbUpdaterOnDbUpdateStart;
		DbUpdater.OnDbUpdateEnd += DbUpdaterOnDbUpdateEnd;
		ItemsOrderChanged += OnItemsOrderChanged;
	}

	public bool IsDownloadInQueueAlready(string messageId, out DownloaderItemViewModel item)
	{
		return Items.IsDownloadQueuedAlready(messageId, out item);
	}

	public bool IsAnyActiveDownloads()
	{
		return Items.Any((DownloaderItemViewModel i) => i.IsDownloading || i.IsQueued);
	}

	public void RestartProcessAsync()
	{
		ShutdownProcessAsync().ContinueWith(delegate(Task<bool> t)
		{
			if (t.Result)
			{
				StartProcessAsync();
			}
			else
			{
				AppHelper.Error("Failed to restart downloader, check logs for details");
				this.DownloaderStatusChanged?.Invoke(this, DownloadStatus.Failure);
			}
		});
	}

	public bool AddToDownloadQueue(string pathToNzb, DownloaderItemViewModel item)
	{
		if (!item.MessageId.IsNullOrEmpty() && IsDownloadInQueueAlready(item.MessageId, out var item2))
		{
			item2.Blink();
			Log.Debug("The spot in the download queue already: " + item.MessageId);
			return false;
		}
		if (!File.Exists(pathToNzb))
		{
			Log.Error("Nzb file not found: " + pathToNzb);
			return false;
		}
		item.RawStatus = DownloadStatus.NzbDownloading;
		Task.Run(delegate
		{
			item.PathToNzb = pathToNzb;
			item.RawStatus = DownloadStatus.Queued;
			item.IsHistoryChanged += OnIsHistoryChanged;
		});
		return true;
	}

	public bool UpdateDownloadSpeedLimit(int kbps)
	{
		VirtualNNTP.SetDownloadSpeedLimit(kbps);
		return true;
	}

	public DownloaderItemViewModel AddFakeItemBeforeNzbDownloaded(string sTitle, string messageId, int category)
	{
		return new SpotnetDownloaderItemViewModel(-1, sTitle, DownloadStatus.NzbDownloading, 0, 0.0, 0, -1, null, null, "", messageId, category, null, DateTime.Now.ToUnixTime(), 0L);
	}

	public Task<bool> StartProcessAsync()
	{
		return Task.Run(delegate
		{
			try
			{
				lock (_lockStartStop)
				{
					if (!IsStarted && Settings.Default.DownloadAction <= 1)
					{
						try
						{
							this.DownloaderStatusChanged?.Invoke(this, DownloadStatus.Starting);
							this.OnDownloaderLoadedFirstTime?.Invoke();
							if (!_isInitialized)
							{
								_isInitialized = true;
								RestoreItemsState();
								if (!Settings.Default.MigrationFromNzbGetDone)
								{
									Migrator.Run();
									Settings.Default.MigrationFromNzbGetDone = true;
									Settings.Default.Save();
								}
							}
							DownloadQueue.StartDownloadQueue();
							IsStarted = true;
							return true;
						}
						finally
						{
							this.DownloaderStatusChanged?.Invoke(this, DownloadStatus.Queued);
						}
					}
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
				return false;
			}
		});
	}

	private void RestoreItemsState()
	{
		try
		{
			if (Settings.Default.ExternalNzbGet)
			{
				return;
			}
			Dictionary<string, string> dictionary = AppHelper.RestoreDict(DownloaderProps.QueueFile);
			if (dictionary == null || !dictionary.Any())
			{
				return;
			}
			string text = dictionary.Values.First();
			if (text.IsNullOrEmpty())
			{
				return;
			}
			foreach (string item in text.Split().ToList())
			{
				if (int.TryParse(item, out var result))
				{
					SpotnetDownloaderItemViewModel.RestoreState(result);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
	}

	public Task<bool> ShutdownProcessAsync()
	{
		return Task.Run(delegate
		{
			try
			{
				lock (_lockStartStop)
				{
					if (!IsStarted)
					{
						return true;
					}
					this.DownloaderStatusChanged?.Invoke(this, DownloadStatus.Stopping);
					bool result = DownloadQueue.StopDownloadQueue();
					List<DownloaderItemViewModel> list = Items.ItemsDict.Values.ToList();
					DownloaderDataStorer.WaitForAllCurrentItemsSave();
					list.ForEach(delegate(DownloaderItemViewModel i)
					{
						((SpotnetDownloaderItemViewModel)i).FilesToDownload.ForEach(delegate(NNTPInput f)
						{
							f.WaitForSaveTheState();
							DownloaderDataStorer.CloseFileStream(f);
						});
					});
					IsStarted = false;
					this.DownloaderStatusChanged?.Invoke(this, DownloadStatus.Success);
					return result;
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
				return false;
			}
		});
	}

	public bool CanMoveDown(IEnumerable<DownloaderItemViewModel> items)
	{
		foreach (DownloaderItemViewModel item in items)
		{
			DownloaderItemViewModel downloaderItemViewModel = NextPriorityItem(item);
			if (downloaderItemViewModel == null || downloaderItemViewModel.IsHistory != item.IsHistory)
			{
				return false;
			}
		}
		return true;
	}

	public bool CanMoveUp(IEnumerable<DownloaderItemViewModel> items)
	{
		foreach (DownloaderItemViewModel item in items)
		{
			DownloaderItemViewModel downloaderItemViewModel = PrevPriorityItem(item);
			if (downloaderItemViewModel == null || downloaderItemViewModel.IsHistory != item.IsHistory)
			{
				return false;
			}
		}
		return true;
	}

	public void SetPlayInactiveToAllItems()
	{
		foreach (DownloaderItemViewModel value in Items.ItemsDict.Values)
		{
			value.IsPlayActive = false;
		}
	}

	public void MoveUp(IEnumerable<DownloaderItemViewModel> items)
	{
		List<DownloaderItemViewModel> list = items.OrderBy((DownloaderItemViewModel i) => i.Index).ToList();
		if (!CanMoveUp(list))
		{
			return;
		}
		foreach (DownloaderItemViewModel item in list)
		{
			SwapPriorityOfItems(PrevPriorityItem(item), item);
		}
		UpdateItemsOrder();
	}

	public void MoveDown(IEnumerable<DownloaderItemViewModel> items)
	{
		List<DownloaderItemViewModel> list = items.OrderByDescending((DownloaderItemViewModel i) => i.Index).ToList();
		if (!CanMoveDown(list))
		{
			return;
		}
		foreach (DownloaderItemViewModel item in list)
		{
			SwapPriorityOfItems(NextPriorityItem(item), item);
		}
		UpdateItemsOrder();
	}

	public void MoveTop(DownloaderItemViewModel item)
	{
		if (Items.Count > 1)
		{
			DownloaderItemViewModel downloaderItemViewModel = Items.OrderBy((DownloaderItemViewModel i) => i.Index).ToList().FirstOrDefault();
			if (downloaderItemViewModel != null && downloaderItemViewModel != item)
			{
				item.Index = downloaderItemViewModel.Index - 1;
				UpdateItemsOrder();
			}
		}
	}

	public void UpdateItemsOrder()
	{
		if (!_isUpdateItemsOrderDisabledForMassUpdate)
		{
			this.ItemsOrderChanged?.Invoke();
		}
	}

	public void RemoveItemsAsync(IEnumerable<DownloaderItemViewModel> items)
	{
		Task.Run(delegate
		{
			DownloaderItemViewModel[] itemsToRemove = items.ToArray();
			try
			{
				_isUpdateItemsOrderDisabledForMassUpdate = true;
				Items.RemoveItems(itemsToRemove);
			}
			finally
			{
				_isUpdateItemsOrderDisabledForMassUpdate = false;
				UpdateItemsOrder();
			}
		});
	}

	public void PauseItemsAsync(IEnumerable<DownloaderItemViewModel> items)
	{
		try
		{
			_isUpdateItemsOrderDisabledForMassUpdate = true;
			foreach (DownloaderItemViewModel item in items)
			{
				item.DownloadPause();
			}
		}
		finally
		{
			_isUpdateItemsOrderDisabledForMassUpdate = false;
			UpdateItemsOrder();
		}
	}

	public void ResumeItemsAsync(IEnumerable<DownloaderItemViewModel> items)
	{
		try
		{
			_isUpdateItemsOrderDisabledForMassUpdate = true;
			foreach (DownloaderItemViewModel item in items)
			{
				item.DownloadResume();
			}
		}
		finally
		{
			_isUpdateItemsOrderDisabledForMassUpdate = false;
			UpdateItemsOrder();
		}
	}

	public int GetNewHistoryIndex(int oldIndex)
	{
		if (oldIndex > 20000)
		{
			return oldIndex;
		}
		int result = 50000;
		try
		{
			result = Items.Where((DownloaderItemViewModel i) => i.IsHistory && i.Index != oldIndex).Min((DownloaderItemViewModel i) => i.Index) - 1;
		}
		catch (InvalidOperationException)
		{
		}
		return result;
	}

	public int GetPriority(DownloaderItemViewModel item)
	{
		if (item.IsHistory || item.IsTotals)
		{
			return 0;
		}
		return (from i in Items
			where !i.IsHistory && !i.IsTotals
			orderby i.Index
			select i).ToList().IndexOf(item) + 1;
	}

	public void Dispose()
	{
		ShutdownProcessAsync().Wait();
		DbUpdater.OnDbUpdateStart -= DbUpdaterOnDbUpdateStart;
		DbUpdater.OnDbUpdateEnd -= DbUpdaterOnDbUpdateEnd;
		if (_timerToUpdateTotals != null)
		{
			_timerToUpdateTotals.Stop();
			_timerToUpdateTotals.Dispose();
			_timerToUpdateTotals = null;
		}
		if (_totalItems != null)
		{
			_totalItems.ProgressChanged += TotalsOnProgressChanged;
		}
		ItemsOrderChanged -= OnItemsOrderChanged;
		SpotDownloader.ClearDownloadersList();
	}

	private void DbUpdaterOnDbUpdateEnd()
	{
		if (SpotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility == Visibility.Collapsed)
		{
			_isWarningAboutDbUpdateClosedByUser = true;
		}
		else
		{
			SpotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility = Visibility.Collapsed;
		}
	}

	private void DbUpdaterOnDbUpdateStart()
	{
		if (!_isWarningAboutDbUpdateClosedByUser)
		{
			SpotsDbIsNotUpToDateSoSpeedCanBeLowWarningVisibility = Visibility.Visible;
		}
	}

	private void OnIsHistoryChanged(DownloaderItemViewModel sender, bool isHistory)
	{
		if (!isHistory || sender.RawStatus == DownloadStatus.Deleted)
		{
			return;
		}
		DispatcherHelper.CheckBeginInvokeOnUI(async delegate
		{
			try
			{
				((MainWindow)Application.Current.MainWindow).DisplayTooltip(sender.Titel + " " + Words.isComplete);
				await ProcessShutdownPcAfterDownloads();
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}
		});
	}

	private async Task ProcessShutdownPcAfterDownloads()
	{
		if (!Sys.ShutdownPCAfterDownloads || IsAnyActiveDownloads() || Interlocked.CompareExchange(ref _lockShutdownDialogShown, 1, 0) != 0)
		{
			return;
		}
		try
		{
			ShutdownComputerDialog shutdownComputerDialog = new ShutdownComputerDialog(Words.ShutdownDialogComputerToBeShutdownDownloadsCompleted);
			shutdownComputerDialog.Owner = Sys.MainWindow;
			shutdownComputerDialog.Show();
			shutdownComputerDialog.Activate();
			shutdownComputerDialog.Topmost = true;
			shutdownComputerDialog.Topmost = false;
			shutdownComputerDialog.Focus();
			ManualResetEventSlim waitForWindowToClose = new ManualResetEventSlim();
			shutdownComputerDialog.Closed += delegate
			{
				waitForWindowToClose.Set();
			};
			await Task.Run(delegate
			{
				waitForWindowToClose.Wait();
			});
		}
		finally
		{
			Interlocked.Exchange(ref _lockShutdownDialogShown, 0);
		}
	}

	private void TotalsOnProgressChanged(double p)
	{
		this.DownloadsProgressChanged?.Invoke((int)p);
	}

	public void ShowLog(DownloaderItemViewModel item)
	{
		AppHelper.LaunchInExternalProgram(((SpotnetDownloaderItemViewModel)item).LogPath);
	}

	private void SwapPriorityOfItems(DownloaderItemViewModel item1, DownloaderItemViewModel item2)
	{
		int index = item1.Index;
		item1.Index = item2.Index;
		item2.Index = index;
	}

	private DownloaderItemViewModel NextPriorityItem(DownloaderItemViewModel item)
	{
		return Items.OrderBy((DownloaderItemViewModel i) => i.Index).FirstOrDefault((DownloaderItemViewModel i) => i.Index > item.Index);
	}

	private DownloaderItemViewModel PrevPriorityItem(DownloaderItemViewModel item)
	{
		return Items.OrderBy((DownloaderItemViewModel i) => i.Index).LastOrDefault((DownloaderItemViewModel i) => i.Index < item.Index);
	}

	private void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private void OnItemsOrderChanged()
	{
		SpotDownloader.UpdatePriorities();
	}
}
