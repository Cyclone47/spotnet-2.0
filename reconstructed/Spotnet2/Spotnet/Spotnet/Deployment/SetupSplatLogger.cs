using System;
using System.IO;
using NLog;
using Pri.LongPath;
using Splat;
using Spotnet.Helpers;

namespace Spotnet.Deployment;

internal class SetupSplatLogger : ILogger, IDisposable
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private readonly object _gate = 42;

	private FileStream _fs;

	private StreamWriter _sw;

	public Splat.LogLevel Level { get; set; }

	public SetupSplatLogger()
	{
		try
		{
			string text = Pri.LongPath.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SquirrelTemp\\SquirrelSetup.log");
			AppHelper.EnsureDirectoryExist(Pri.LongPath.Path.GetDirectoryName(text));
			if (!Pri.LongPath.File.Exists(text))
			{
				Pri.LongPath.File.WriteAllText(text, "");
			}
			long length = new Pri.LongPath.FileInfo(text).Length;
			_fs = ((length < 5248000) ? Pri.LongPath.File.Open(text, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite) : Pri.LongPath.File.Open(text, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
			_sw = new StreamWriter(_fs);
		}
		catch (Exception ex)
		{
			Log.Debug(ex.Message);
		}
	}

	public void Dispose()
	{
		if (_sw != null)
		{
			_sw.Dispose();
			_sw = null;
			_fs.Dispose();
			_fs = null;
		}
	}

	public void Write(string message, Splat.LogLevel logLevel)
	{
		if (_sw != null)
		{
			string value = $"{DateTime.Now:MM/dd/yy H:mm:ss zzz} [{logLevel}] {message}";
			lock (_gate)
			{
				_sw.WriteLine(value);
			}
		}
	}
}
