using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using Pri.LongPath;
using Spotnet.DataVirtualization;
using Spotnet.Extensions;
using Spotnet.Helpers;
using Spotnet.Model;
using Spotnet.Model.Newznab;
using Spotnet.Properties;
using Spotnet.ViewModel;

namespace Spotnet.DAL;

public class SpotProvider : IVirtualListLoader<ISpotRow>
{
	public const string NoEroticaFilter = "cat<9";

	public const string NoEroticaFilterForSearch = "cats NOT LIKE '9 %'";

	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private int _cacheQueryCount;

	internal bool IsCacheQueryCountPrecise;

	private Dictionary<string, int> _cacheQueryCounts;

	private string _lastRowFilter;

	private string _queryName;

	private string _rowFilter;

	private readonly string _queryDefaultName;

	public long RowNew;

	private const string PosterIdentFilterString = "^PosterIdent[ ]+IN[ ]+\\(([W|B|F|T|N|,| ]+)\\)";

	private string _lastCountQueryRequested;

	private readonly object _lockSlowCountCalculation = new object();

	internal bool Connected { get; private set; }

	internal string Filename { get; private set; }

	internal long QueryCount
	{
		get
		{
			if (_cacheQueryCount <= 0)
			{
				return 0L;
			}
			return _cacheQueryCount;
		}
	}

	private static StatusBarViewModel StatusBarVm => ((ViewModelLocator)Application.Current.Resources["Locator"]).StatusBar;

	public bool Corrupted { get; private set; }

	public string QueryName
	{
		get
		{
			if (_queryName.IsNullOrEmpty() || _queryName.EqualsIgnoreCase("cat < 9"))
			{
				return _queryDefaultName;
			}
			return _queryName;
		}
		set
		{
			_queryName = value;
		}
	}

	public string SortOrder
	{
		get
		{
			return Settings.Default.SortDirection;
		}
		set
		{
			Settings.Default.SortDirection = value;
			Settings.Default.Save();
		}
	}

	public string RowFilter
	{
		get
		{
			return _rowFilter ?? "";
		}
		set
		{
			_rowFilter = value;
		}
	}

	private List<char> PosterIdentFilter
	{
		get
		{
			if (!_rowFilter.StartsWith("PosterIdent"))
			{
				return null;
			}
			List<char> result = new List<char>();
			Regex regex = new Regex("^PosterIdent[ ]+IN[ ]+\\(([W|B|F|T|N|,| ]+)\\)", RegexOptions.IgnoreCase);
			if (regex.IsMatch(_rowFilter.Trim()))
			{
				result = (from s in regex.Match(_rowFilter).Groups[1].Value.Replace(" ", "").Split(',')
					select s[0]).ToList();
			}
			return result;
		}
	}

	public bool CanSort => true;

	private static MainWindowViewModel MainWindowVm => ((ViewModelLocator)Application.Current.Resources["Locator"]).MainWindow;

	internal static event Action OnDbSettingsUpdate;

	public SpotProvider()
	{
		_cacheQueryCount = -1;
		_cacheQueryCounts = new Dictionary<string, int>();
		RowNew = -1L;
		Corrupted = false;
		_rowFilter = "cat < 9";
		_lastRowFilter = "";
		_queryName = _queryDefaultName;
		_queryDefaultName = Words.TabSpots;
		if (!Settings.Default.SortColumn.Trim().ToLower().Equals("rowid") || !Settings.Default.SortDirection.Trim().ToLower().Equals("desc"))
		{
			Settings.Default.SortColumn = "rowid";
			Settings.Default.SortDirection = "desc";
			Settings.Default.Save();
		}
	}

	private void UpdateQueryCount()
	{
		StatusBarVm.SetDefaultSpotsListStatusMessage();
	}

	public int GetNewCounts(string query)
	{
		if (Sys.MainWindow.SpotProvider.RowNew <= 1)
		{
			return 0;
		}
		query = Favorites.ReplaceWithFavoritesQuery(query);
		string text = (AppHelper.IsSearchQuery(query) ? CreateSearchQueryCountNew(query) : CreateQueryCountNew(query));
		if (text.IsNullOrEmpty())
		{
			return 0;
		}
		using ISqlDb sqlDb = SqlDbFactory.CreateSqlDbSpots();
		using ISqlDbTransaction transaction = sqlDb.BeginReadTransaction();
		return (int)sqlDb.ExecuteScalar(text, transaction);
	}

	public IList<ISpotRow> LoadRange(int startIndex, int count, long minRowId, out int overallCount, out bool isNewQuery, out bool isLastPage, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		int num = 0;
		isNewQuery = false;
		IList<ISpotRow> list2;
		try
		{
			if (Settings.Default.MaxResults == 0)
			{
				throw new Exception("This type of list is not supported anymore");
			}
			if (!_lastRowFilter.Equals(_rowFilter))
			{
				isNewQuery = true;
				_lastRowFilter = _rowFilter;
			}
			if (!Connected || _rowFilter.ToLower().Equals("newznab"))
			{
				overallCount = 0;
				isLastPage = true;
				return new List<ISpotRow>();
			}
			if (NewznabHelper.IsNewznabQuery(_rowFilter))
			{
				IList<ISpotRow> list = NewznabHelper.ExecuteQuery(_rowFilter, startIndex, count, out overallCount, cancellationToken);
				isLastPage = startIndex + list.Count == overallCount;
				return list;
			}
			cancellationToken.ThrowIfCancellationRequested();
			bool flag = false;
			num = 1;
			if (_cacheQueryCount < 1 && minRowId < 0)
			{
				_cacheQueryCount = -1;
				if (_rowFilter.IsNullOrEmpty() || _rowFilter.Replace(" ", "").EqualsIgnoreCase("cat<9") || _rowFilter.Replace(" ", "").EqualsIgnoreCase("cat!=0"))
				{
					_cacheQueryCount = checked((int)Settings.Default.DatabaseFilter);
					flag = true;
				}
			}
			num = 3;
			string text = Favorites.ReplaceWithFavoritesQuery(_rowFilter);
			text = (AppHelper.IsSearchQuery(text) ? CreateSearchQuery(text, startIndex, count, minRowId, out var countQuery) : CreateQuery(text, startIndex, count, minRowId, out countQuery));
			num = 4;
			cancellationToken.ThrowIfCancellationRequested();
			num = 5;
			list2 = ReadRows(text, cancellationToken, out var countBeforeFilter);
			num = 6;
			if (flag)
			{
				_cacheQueryCounts[countQuery] = _cacheQueryCount;
			}
			isLastPage = UpdateQueryCount(countBeforeFilter, startIndex, count, countQuery);
			overallCount = startIndex + list2.Count;
			if (overallCount > _cacheQueryCount)
			{
				overallCount = _cacheQueryCount;
			}
		}
		catch (Exception ex)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Log.Exception(ex);
			string text2 = "LoadRange #" + num + " query: " + _rowFilter + ". Error: " + ex.Message;
			Log.Debug(text2);
			AppHelper.Error(text2);
			if (ex is SQLiteException { ResultCode: var resultCode } && (resultCode == SQLiteErrorCode.Corrupt || resultCode == SQLiteErrorCode.NotADb))
			{
				Corrupted = true;
			}
			ResetCache();
			overallCount = 0;
			isLastPage = true;
			list2 = null;
		}
		return list2;
	}

	private bool UpdateQueryCount(int countBeforeFilter, int startIndex, int count, string countQuery)
	{
		IsCacheQueryCountPrecise = true;
		if (_cacheQueryCount > startIndex + count)
		{
			return false;
		}
		if (_cacheQueryCounts.ContainsKey(countQuery))
		{
			_cacheQueryCount = _cacheQueryCounts[countQuery];
			return _cacheQueryCount <= startIndex + count;
		}
		IsCacheQueryCountPrecise = false;
		_cacheQueryCount = ((countBeforeFilter > 0) ? (startIndex + count + 1) : (startIndex + countBeforeFilter));
		ScheduleSlowCountCalculationAsync(countQuery);
		return countBeforeFilter == 0;
	}

	private void ScheduleSlowCountCalculationAsync(string countQuery)
	{
		Task.Run(delegate
		{
			_lastCountQueryRequested = countQuery;
			if (!Monitor.TryEnter(_lockSlowCountCalculation))
			{
				return;
			}
			try
			{
				using ISqlDb sqlDb = SqlDbFactory.CreateSqlDbSpots();
				using ISqlDbTransaction transaction = sqlDb.BeginReadTransaction();
				while (true)
				{
					_cacheQueryCount = (int)sqlDb.ExecuteScalar(countQuery, transaction);
					if (_cacheQueryCount >= 0 && !Favorites.IsFavoritesQuery(countQuery))
					{
						_cacheQueryCounts[countQuery] = _cacheQueryCount;
					}
					if (_lastCountQueryRequested.Equals(countQuery))
					{
						break;
					}
					countQuery = _lastCountQueryRequested;
				}
				IsCacheQueryCountPrecise = true;
				UpdateQueryCount();
			}
			finally
			{
				Monitor.Exit(_lockSlowCountCalculation);
			}
		});
	}

	internal bool OpenDb()
	{
		try
		{
			Connected = false;
			string queryName = QueryName;
			string rowFilter = RowFilter;
			if (Connect())
			{
				QueryName = queryName;
				RowFilter = rowFilter;
				Connected = true;
			}
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
		return Connected;
	}

	private string CreateQuery(string filter, int startIndex, int countRequested, long minRowId, out string countQuery)
	{
		string text = Settings.Default.SortColumn.Trim().ToLower();
		if (text.EqualsIgnoreCase("rowid"))
		{
			text = "date";
		}
		string text2 = "";
		string text3 = "";
		if (filter != null && filter.StartsWith("PosterIdent"))
		{
			Regex regex = new Regex("^PosterIdent[ ]+IN[ ]+\\(([W|B|F|T|N|,| ]+)\\)", RegexOptions.IgnoreCase);
			filter = filter.Substring(regex.Match(filter.Trim()).Groups[0].Value.Length).Trim();
			filter = (filter.ToUpper().StartsWith("AND ") ? filter.Substring(4) : "cat<10");
		}
		if (!filter.IsNullOrWhiteSpace())
		{
			string text4 = filter.Replace(" ", "").ToLower();
			string text5 = ((Settings.Default.ShowEroticaInSearchResults || text4.Contains("cat=") || text4.Contains("cat<")) ? "" : "cat<9 AND ");
			string text6 = ((minRowId >= 0) ? "rowid>={0} AND ".Format(minRowId) : "");
			text2 = " WHERE " + text6 + "(" + text5 + filter + " AND key != 2 AND key != 5)";
			text2 = text2.Replace("[SN:DATE]", FDate().ToStringSafely());
			text2 = ((RowNew <= 1) ? text2.Replace("[SN:NEW]", Convert.ToString(Settings.Default.DatabaseMax + 1)) : text2.Replace("[SN:NEW]", Convert.ToString(RowNew)));
			if (filter.Replace(" ", "").ToLower().Contains("date>"))
			{
				text3 = " INDEXED BY dateidx ";
			}
		}
		string text7 = " ORDER BY " + text + " " + SortOrder + " ";
		string result;
		if (text.ToUpper().Equals("DATE") && SortOrder.ToUpper().Equals("DESC"))
		{
			result = string.Format("SELECT rowid,{0} FROM spots {1} WHERE rowid IN (SELECT rowid FROM spots {6}{1} {2} ORDER BY rowid DESC LIMIT {4} OFFSET {5}){3}", "subcat,extcat,date,filesize,subject,sender,tag,modulus,spots.msgid,IFNULL(cnt,0),cat,cats", "LEFT JOIN spamgroup s USING (msgid)", text2, text7, countRequested, startIndex, text3);
		}
		else
		{
			if (!text.ToUpper().Equals("DATE"))
			{
				text7 += ", date DESC ";
			}
			result = string.Format("SELECT rowid,{0} FROM spots {1}{2}{3} LIMIT {4} OFFSET {5}", "subcat,extcat,date,filesize,subject,sender,tag,modulus,spots.msgid,IFNULL(cnt,0),cat,cats", "LEFT JOIN spamgroup s USING (msgid)", text2, text7, countRequested, startIndex);
		}
		countQuery = "SELECT COUNT(1) FROM spots LEFT JOIN spamgroup s USING (msgid)" + text2;
		return result;
	}

	private string CreateSearchQuery(string filter, int startIndex, int countRequested, long minRowId, out string countQuery)
	{
		string text = Settings.Default.SortColumn.Trim().ToLower();
		if (text.EqualsIgnoreCase("rowid"))
		{
			text = "date";
		}
		string text2 = "";
		if (!filter.IsNullOrWhiteSpace())
		{
			string text3 = ((minRowId >= 0) ? "docid>={0} AND ".Format(minRowId) : "");
			string text4 = ((Settings.Default.ShowEroticaInSearchResults || filter.ToLower().Contains("cats match ")) ? "" : "cats NOT LIKE '9 %' AND ");
			text2 = " WHERE " + text3 + "(" + text4 + filter + ")";
			text2 = text2.Replace("[SN:DATE]", FDate().ToStringSafely());
			text2 = ((RowNew <= 1) ? text2.Replace("[SN:NEW]", Convert.ToString(Settings.Default.DatabaseMax + 1)) : text2.Replace("[SN:NEW]", Convert.ToString(RowNew)));
		}
		string text5 = " ORDER BY " + text + " " + SortOrder + " ";
		if (!text.ToUpper().Equals("DATE"))
		{
			text5 += ", date DESC ";
		}
		string text6 = "SELECT rowid,subcat,extcat,date,filesize,subject,sender,tag,modulus,spots.msgid,IFNULL(cnt,0),cat,cats FROM spots LEFT JOIN spamgroup s USING (msgid) WHERE rowid IN ";
		string text7 = $" LIMIT {countRequested} OFFSET {startIndex}";
		string text8 = "";
		if (text.ToUpper().Equals("DATE") && SortOrder.ToUpper().Equals("DESC"))
		{
			text8 = " ORDER BY rowid DESC " + text7 + " ";
			text7 = "";
		}
		string result = text6 + " (SELECT docid FROM search" + text2 + text8 + ") AND key != 2 AND key != 5" + text5 + text7;
		countQuery = "SELECT COUNT(1) FROM search" + text2;
		return result;
	}

	private string CreateQueryCountNew(string filter)
	{
		if (filter.IsNullOrWhiteSpace())
		{
			return null;
		}
		if (filter.StartsWith("PosterIdent"))
		{
			Regex regex = new Regex("^PosterIdent[ ]+IN[ ]+\\(([W|B|F|T|N|,| ]+)\\)", RegexOptions.IgnoreCase);
			filter = filter.Substring(regex.Match(filter.Trim()).Groups[0].Value.Length).Trim();
			filter = (filter.ToUpper().StartsWith("AND ") ? filter.Substring(4) : "cat<10");
		}
		string text = filter.Replace(" ", "").ToLower();
		string text2 = ((Settings.Default.ShowEroticaInSearchResults || text.Contains("cat=") || text.Contains("cat<")) ? "" : "cat<9 AND ");
		return string.Format("SELECT COUNT(1) FROM spots WHERE rowid>{0} AND ({1}{2}{3})", RowNew, text2, filter, " AND key != 2 AND key != 5").Replace("[SN:DATE]", FDate().ToStringSafely()).Replace("[SN:NEW]", Convert.ToString(RowNew));
	}

	private string CreateSearchQueryCountNew(string filter)
	{
		if (filter.IsNullOrWhiteSpace())
		{
			return null;
		}
		string arg = ((Settings.Default.ShowEroticaInSearchResults || filter.ToLower().Contains("cats match ")) ? "" : "cats NOT LIKE '9 %' AND ");
		return $"SELECT COUNT(1) FROM search WHERE docid>{RowNew} AND ({arg}{filter})".Replace("[SN:DATE]", FDate().ToStringSafely()).Replace("[SN:NEW]", Convert.ToString(RowNew));
	}

	private void CreateSpotsTablesOnEmptyDatabase(ISqlDb db)
	{
		if (new string[4] { "PRAGMA page_size = 4096;", "PRAGMA journal_mode = DELETE", "PRAGMA locking_mode = NORMAL", "PRAGMA user_version = 0" }.Any((string command) => db.ExecuteNonQuery(command, null) != 0))
		{
			throw new Exception("Pragma exception");
		}
		using ISqlDbTransaction sqlDbTransaction = db.BeginWriteTransaction();
		if (db.ExecuteNonQuery("CREATE TABLE spots(rowid INTEGER PRIMARY KEY, key INT, cat INT, subcat INT, extcat INT, date INT, filesize INTEGER, cats TEXT, sender TEXT, tag TEXT, subject TEXT, msgid TEXT, modulus TEXT)", sqlDbTransaction) != 0)
		{
			throw new Exception("CREATE TABLE spots");
		}
		if (db.ExecuteNonQuery("CREATE VIRTUAL TABLE search USING fts4(content=\"spots\",cats TEXT, sender TEXT, tag TEXT, subject TEXT,order=desc,matchinfo=fts3)", sqlDbTransaction) != 0)
		{
			throw new Exception("CREATE TABLE search");
		}
		if (db.ExecuteNonQuery("CREATE TABLE spamreports(rowid INTEGER PRIMARY KEY, msgid TEXT, modulus TEXT, date INT, reportmsgid TEXT, sender TEXT)", sqlDbTransaction) != 0)
		{
			throw new Exception("CREATE TABLE spamreports");
		}
		if (db.ExecuteNonQuery("CREATE TABLE spamgroup(msgid TEXT PRIMARY KEY NOT NULL, cnt INT DEFAULT 0)", sqlDbTransaction) != 0)
		{
			throw new Exception("CREATE TABLE spamgroup");
		}
		if (db.ExecuteNonQuery("PRAGMA user_version = 2", sqlDbTransaction) != 0)
		{
			throw new Exception("PRAGMA user_version");
		}
		sqlDbTransaction.Commit();
	}

	private long FDate()
	{
		return DateTime.UtcNow.ToUnixTime();
	}

	private static string GetString(object obj)
	{
		return (obj as string).ToStringSafely();
	}

	private List<ISpotRow> ReadRows(string query, CancellationToken cancellationToken, out int countBeforeFilter)
	{
		List<ISpotRow> list = new List<ISpotRow>();
		using (ISqlDb sqlDb = SqlDbFactory.CreateSqlDbSpots())
		{
			using ISqlDbTransaction transaction = sqlDb.BeginReadTransaction();
			DbCommand dbCommand = sqlDb.CreateCommand(transaction);
			dbCommand.CommandText = query;
			using DbDataReader dbDataReader = dbCommand.ExecuteReader();
			while (dbDataReader.Read())
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					string @string = GetString(RuntimeHelpers.GetObjectValue(dbDataReader[5]));
					string string2 = GetString(RuntimeHelpers.GetObjectValue(dbDataReader[8]));
					int @int = dbDataReader.GetInt32(1);
					int int2 = dbDataReader.GetInt32(2);
					int int3 = dbDataReader.GetInt32(11);
					SpotRowChild spot = default(SpotRowChild);
					spot.ID = dbDataReader.GetInt64(0);
					spot.SubCat = @int;
					spot.ExtCat = int2;
					spot.Stamp = dbDataReader.GetInt32(3);
					spot.Filesize = dbDataReader.GetInt64(4);
					spot.Title = @string;
					spot.Poster = GetString(RuntimeHelpers.GetObjectValue(dbDataReader[6]));
					spot.Tag = GetString(RuntimeHelpers.GetObjectValue(dbDataReader[7]));
					spot.Modulus = string2;
					spot.MessageId = GetString(RuntimeHelpers.GetObjectValue(dbDataReader[9]));
					spot.NumberOfSpamReports = dbDataReader.GetInt32(10);
					spot.Cat = int3;
					spot.Cats = GetString(RuntimeHelpers.GetObjectValue(dbDataReader[12]));
					SpotRowViewModel item = SpotRowViewModel.InitializeNewSpotRow(spot);
					list.Add(item);
				}
				catch (Exception ex)
				{
					Log.Exception(ex);
					throw new Exception("Reader Error: " + ex.Message);
				}
			}
		}
		countBeforeFilter = list.Count;
		List<char> posterIdentFilter = PosterIdentFilter;
		list.RemoveAll(delegate(ISpotRow row)
		{
			bool num = !ShouldRowBeVisible(row, posterIdentFilter);
			if (num)
			{
				row.Dispose();
			}
			return num;
		});
		return list;
	}

	private bool ShouldRowBeVisible(ISpotRow row, List<char> posterIdentFilter)
	{
		if (!row.Modulus.IsNullOrEmpty() && row.Modulus.Equals(UserKeyHelper.GetModulus()))
		{
			return true;
		}
		if (posterIdentFilter != null && posterIdentFilter.Any())
		{
			PosterIdentType posterIdent = row.PosterIdent;
			using (List<char>.Enumerator enumerator = posterIdentFilter.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					switch (char.ToUpper(enumerator.Current))
					{
					case 'B':
						if (posterIdent == PosterIdentType.Black)
						{
							return true;
						}
						break;
					case 'W':
						if (posterIdent == PosterIdentType.White)
						{
							return true;
						}
						break;
					case 'T':
						if (posterIdent == PosterIdentType.Verified)
						{
							return true;
						}
						break;
					case 'F':
						if (posterIdent == PosterIdentType.Fake)
						{
							return true;
						}
						break;
					case 'N':
						if (posterIdent == PosterIdentType.None)
						{
							return true;
						}
						break;
					}
				}
			}
			return false;
		}
		if (Settings.Default.NumOfSpamReportsToSpotHide > 0 && row.NumberOfSpamReports >= Settings.Default.NumOfSpamReportsToSpotHide)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(row.Titel))
		{
			return false;
		}
		if (MainWindowVm.ShowTrustedOnlyMode && row.PosterIdent != PosterIdentType.White && row.PosterIdent != PosterIdentType.SpotWhite && row.PosterIdent != PosterIdentType.Verified)
		{
			return false;
		}
		if (Settings.Default.HideBlacklistedSpots && (row.PosterIdent == PosterIdentType.Black || row.PosterIdent == PosterIdentType.SpotBlack))
		{
			return false;
		}
		return true;
	}

	internal void ResetCache()
	{
		ResetCount();
		_cacheQueryCounts = new Dictionary<string, int>();
	}

	internal string GetMessageId(long id)
	{
		using ISqlDb sqlDb = SqlDbFactory.CreateSqlDbSpots(isReadOnly: true);
		using ISqlDbTransaction transaction = sqlDb.BeginReadTransaction();
		DbCommand dbCommand = sqlDb.CreateCommand(transaction);
		dbCommand.CommandText = "SELECT msgid FROM spots WHERE rowid = " + id;
		return sqlDb.ExecuteCommand(dbCommand).Replace("\r\n", "").Trim();
	}

	public void ResetCount()
	{
		_cacheQueryCount = -1;
	}

	public bool Connect()
	{
		bool flag = false;
		Corrupted = false;
		try
		{
			using ISqlDb sqlDb = SqlDbFactory.CreateSqlDbSpots();
			if (!Filename.IsNullOrEmpty() && !Filename.EqualsIgnoreCase(sqlDb.Filename))
			{
				flag = true;
			}
			Filename = sqlDb.Filename;
			if (!File.Exists(Filename) || new FileInfo(Filename).Length < 1)
			{
				flag = true;
				CreateSpotsTablesOnEmptyDatabase(sqlDb);
				if (new FileInfo(Filename).Length < 1)
				{
					throw new Exception("db creation failed");
				}
			}
			DatabaseUpgrade(sqlDb);
			using (ISqlDbTransaction sqlDbTransaction = sqlDb.BeginWriteTransaction(exclusive: true))
			{
				sqlDb.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS dateidx ON spots(date)", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS catidx ON spots(cat)", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS msgidx ON spots(msgid)", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS subjidx ON spots(subject)", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS msgidx ON spamreports(msgid)", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS modidx ON spamreports(modulus)", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE TRIGGER IF NOT EXISTS search_bu BEFORE UPDATE ON spots BEGIN DELETE FROM search WHERE docid = old.rowid; END;", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE TRIGGER IF NOT EXISTS search_bd BEFORE DELETE ON spots BEGIN DELETE FROM search WHERE docid = old.rowid; END;", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE TRIGGER IF NOT EXISTS search_au AFTER UPDATE ON spots BEGIN INSERT INTO search(docid, cats, sender, tag, subject) VALUES(new.rowid, new.cats, new.sender, new.tag, new.subject); END;", sqlDbTransaction);
				sqlDb.ExecuteNonQuery("CREATE TRIGGER IF NOT EXISTS search_ai AFTER INSERT ON spots BEGIN INSERT INTO search(docid, cats, sender, tag, subject) VALUES(new.rowid, new.cats, new.sender, new.tag, new.subject); END;", sqlDbTransaction);
				sqlDbTransaction.Commit();
			}
			if (Settings.Default.DatabaseMax < 1 || Settings.Default.DatabaseMin < 1 || Settings.Default.DatabaseCount < 1 || Settings.Default.DatabaseFilter < 1)
			{
				flag = true;
			}
			using (ISqlDbTransaction transaction = sqlDb.BeginReadTransaction())
			{
				long num = sqlDb.ExecuteScalar("SELECT MAX(rowid) FROM spots", transaction);
				if (num < 1 || num != Settings.Default.DatabaseMax)
				{
					flag = true;
				}
				long num2 = sqlDb.ExecuteScalar("SELECT MIN(rowid) FROM spots", transaction);
				if (num2 < 1 || num2 != Settings.Default.DatabaseMin)
				{
					flag = true;
				}
				if (flag)
				{
					Settings.Default.DatabaseMax = num;
					Settings.Default.DatabaseMin = num2;
					long databaseCount = sqlDb.ExecuteScalar("SELECT COUNT(1) FROM spots", transaction);
					Settings.Default.DatabaseCount = databaseCount;
					long databaseCount2 = Settings.Default.DatabaseCount;
					long num3 = Math.Abs(sqlDb.ExecuteScalar("SELECT COUNT(1) FROM spots WHERE cat=9", transaction));
					long databaseFilter = databaseCount2 - num3;
					Settings.Default.DatabaseFilter = databaseFilter;
					Settings.Default.Save();
					SpotProvider.OnDbSettingsUpdate?.Invoke();
				}
			}
			return true;
		}
		catch (AccessViolationException ex)
		{
			Log.Exception(ex, showToClient: true);
			return false;
		}
		catch (Exception ex2)
		{
			Log.Exception(ex2);
			if (ex2 is SQLiteException { ResultCode: var resultCode } && (resultCode == SQLiteErrorCode.Corrupt || resultCode == SQLiteErrorCode.NotADb))
			{
				Corrupted = true;
			}
			return false;
		}
	}

	private int DatabaseUpgrade(ISqlDb db)
	{
		long num = db.ExecuteScalar("PRAGMA user_version", null);
		int num2 = 1;
		if (num < num2)
		{
			Log.Debug("Upgrade DB to version " + num2);
			using (ISqlDbTransaction sqlDbTransaction = db.BeginWriteTransaction())
			{
				if (db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS spamreports(rowid INTEGER PRIMARY KEY, msgid TEXT, modulus TEXT, date INT)", sqlDbTransaction) != 0)
				{
					throw new Exception("CREATE TABLE spamreports");
				}
				if (db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS spamgroup(msgid TEXT PRIMARY KEY NOT NULL, cnt INT DEFAULT 0)", sqlDbTransaction) != 0)
				{
					throw new Exception("CREATE TABLE spamgroup");
				}
				if (db.ExecuteNonQuery("PRAGMA user_version = " + num2, sqlDbTransaction) != 0)
				{
					throw new Exception("PRAGMA user_version");
				}
				sqlDbTransaction.Commit();
			}
			Log.Debug("DB upgraded to " + num2);
		}
		num2 = 2;
		if (num < num2)
		{
			Log.Debug("Upgrade DB to version " + num2);
			using (ISqlDbTransaction sqlDbTransaction2 = db.BeginWriteTransaction())
			{
				if (db.ExecuteNonQuery("ALTER TABLE spamreports ADD COLUMN reportmsgid TEXT", sqlDbTransaction2) != 0)
				{
					throw new Exception("ADD COLUMN reportmsgid");
				}
				if (db.ExecuteNonQuery("ALTER TABLE spamreports ADD COLUMN sender TEXT", sqlDbTransaction2) != 0)
				{
					throw new Exception("ADD COLUMN sender");
				}
				if (db.ExecuteNonQuery("PRAGMA user_version = " + num2, sqlDbTransaction2) != 0)
				{
					throw new Exception("PRAGMA user_version");
				}
				sqlDbTransaction2.Commit();
			}
			Log.Debug("DB upgraded to " + num2);
		}
		return num2;
	}

	public IdPosition GetIdPosition(string sTable)
	{
		using ISqlDb db = SqlDbFactory.CreateSqlDbSpots(isReadOnly: true);
		return AppHelper.GetIdPosition(db, sTable);
	}

	public int GetTheNumberOfSpamReports(string messageId)
	{
		if (messageId.IsNullOrEmpty())
		{
			return 0;
		}
		using ISqlDb sqlDb = SqlDbFactory.CreateSqlDbSpots(isReadOnly: true);
		int num;
		using (ISqlDbTransaction transaction = sqlDb.BeginReadTransaction())
		{
			num = (int)sqlDb.ExecuteScalar("SELECT cnt FROM spamgroup WHERE msgid='" + SpotHelper.MakeMsg(messageId, tag: false) + "'", transaction);
		}
		return (num >= 0) ? num : 0;
	}

	public void ClearDbFilesIfMalformed()
	{
		if (Settings.Default.SpotsDbFileMalformed || Settings.Default.RecreateDbScheduled)
		{
			AppHelper.ClearSpotsDb();
			Settings.Default.SpotsDbFileMalformed = false;
		}
		if (Settings.Default.CommentsDbFileMalformed || Settings.Default.RecreateDbScheduled)
		{
			AppHelper.ClearCommentsDb();
			Settings.Default.CommentsDbFileMalformed = false;
		}
		Settings.Default.RecreateDbScheduled = false;
		Settings.Default.Save();
	}
}
