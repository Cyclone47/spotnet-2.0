using System;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using NLog;
using System.IO;

namespace Spotnet.Model;

internal class VPNStatusChecker
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private bool started;

	private bool stopped;

	private bool initialized;

	private bool socketInitializedRecently;

	private Socket client;

	private EndPoint remoteEndPoint;

	private readonly string topicName = "VPNNederlandStatus";

	public static event Action<bool> StateChanged;

	public static event Action<bool> OnVPNInstalled;

	~VPNStatusChecker()
	{
		Stop();
	}

	private void Run()
	{
		EndPoint remoteEP = null;
		byte[] array = new byte[1024];
		SetStoppedValue(value: false);
		while (!IsStopped())
		{
			if (!IsInitialized())
			{
				Initialize();
				remoteEP = client?.LocalEndPoint;
				Thread.Sleep(50);
				continue;
			}
			if (remoteEP == null)
			{
				Thread.Sleep(50);
				continue;
			}
			try
			{
				int num = client.ReceiveFrom(array, ref remoteEP);
				if (num <= 0)
				{
					continue;
				}
				string text = Encoding.ASCII.GetString(array, 0, num) + "," + remoteEP.ToString();
				socketInitializedRecently = false;
				Log.Log(LogLevel.Info, "New VPNStatusEvent Received: " + text);
				string[] array2 = text.Split(",".ToCharArray());
				if (array2.Length != 0 && !(array2[1] != topicName))
				{
					if (array2[0] == "SERVICE_STOPPED")
					{
						Log.Log(LogLevel.Error, "Spotnet was removed from " + topicName + ": Service was stopped");
						SetInitializedValue(value: false);
						client = null;
						remoteEndPoint = null;
						Thread.Sleep(500);
					}
					else
					{
						VPNStatusChecker.StateChanged?.Invoke(array2[0] == "CONNECTED");
					}
				}
			}
			catch (SocketException)
			{
				if (socketInitializedRecently || !IsVPNNederlandServiceRunning())
				{
					socketInitializedRecently = false;
					SetInitializedValue(value: false);
					client = null;
					remoteEndPoint = null;
				}
			}
		}
		SetStoppedValue(value: true);
		SetStartedValue(value: false);
	}

	private void Initialize()
	{
		bool flag = IsVPNNederlandInstalled();
		if (!flag)
		{
			VPNStatusChecker.OnVPNInstalled?.Invoke(flag);
		}
		while (!IsStopped() && (!IsVPNNederlandInstalled() || !IsVPNNederlandServiceRunning()))
		{
			Thread.Sleep(500);
			flag = IsVPNNederlandInstalled();
		}
		VPNStatusChecker.OnVPNInstalled?.Invoke(flag);
		if (!IsStopped())
		{
			Log.Log(LogLevel.Info, "VPNNederland is installed and service is running");
			IPAddress address = IPAddress.Parse("127.0.0.1");
			int port = 6834;
			client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
			{
				SendTimeout = 3000
			};
			remoteEndPoint = new IPEndPoint(address, port);
			Subscribe();
		}
	}

	private void Subscribe()
	{
		string s = "subscribe" + "," + topicName;
		try
		{
			client.SendTo(Encoding.ASCII.GetBytes(s), remoteEndPoint);
			SetInitializedValue(value: true);
			socketInitializedRecently = true;
			Log.Log(LogLevel.Info, "Spotnet was subscribed to " + topicName + " topic ");
		}
		catch (SocketException)
		{
			SetInitializedValue(value: false);
			socketInitializedRecently = false;
			client = null;
			remoteEndPoint = null;
			Log.Log(LogLevel.Error, "Spotnet subscription to " + topicName + " result in error");
		}
	}

	private void Unsubscribe()
	{
		string s = "unsubscribe" + "," + topicName;
		try
		{
			client.SendTo(Encoding.ASCII.GetBytes(s), remoteEndPoint);
			SetInitializedValue(value: false);
		}
		catch (SocketException)
		{
			SetInitializedValue(value: false);
		}
		Log.Log(LogLevel.Info, "Spotnet unsubscribed from " + topicName + " topic");
	}

	public void Start()
	{
		if (!IsStarted())
		{
			SetStartedValue(value: true);
			Thread thread = new Thread(Run);
			thread.IsBackground = true;
			thread.Start();
		}
	}

	private void SetStartedValue(bool value)
	{
		lock (this)
		{
			started = value;
		}
	}

	private void SetStoppedValue(bool value)
	{
		lock (this)
		{
			stopped = value;
		}
	}

	private void SetInitializedValue(bool value)
	{
		lock (this)
		{
			initialized = value;
		}
	}

	public bool IsInitialized()
	{
		lock (this)
		{
			return initialized;
		}
	}

	public bool IsStarted()
	{
		lock (this)
		{
			return started;
		}
	}

	public void Stop()
	{
		lock (this)
		{
			stopped = true;
		}
		if (IsInitialized())
		{
			Unsubscribe();
		}
	}

	public bool IsStopped()
	{
		lock (this)
		{
			return stopped;
		}
	}

	private bool IsVPNNederlandServiceRunning()
	{
		try
		{
			return new ServiceController("VPNNederland Service").Status == ServiceControllerStatus.Running;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	public static bool IsVPNNederlandInstalled()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string path = folderPath + Path.DirectorySeparatorChar + "VPNNederland" + Path.DirectorySeparatorChar + "VPNNederland.exe";
		string path2 = folderPath + Path.DirectorySeparatorChar + "VPNNederlandCore" + Path.DirectorySeparatorChar + "VPNNederlandService.exe";
		if (File.Exists(path))
		{
			return File.Exists(path2);
		}
		return false;
	}
}
