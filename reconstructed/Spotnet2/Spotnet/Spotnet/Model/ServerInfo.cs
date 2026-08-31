using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using NLog;

namespace Spotnet.Model;

public class ServerInfo : ICloneable
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	public int Connections;

	public string Password;

	public int Port;

	public bool SSL;

	public string Server;

	public string Username;

	public ServerInfo()
	{
		Port = 119;
		SSL = false;
		Username = "";
		Password = "";
		Server = "";
		Connections = 2;
	}

	public object Clone()
	{
		return new ServerInfo
		{
			Port = Port,
			SSL = SSL,
			Username = Username,
			Password = Password,
			Server = Server,
			Connections = Connections
		};
	}

	internal bool DoesProviderUseSsl()
	{
		TcpClient tcpClient = null;
		try
		{
			tcpClient = new TcpClient(Server, Port);
			using SslStream sslStream = new SslStream(tcpClient.GetStream(), leaveInnerStreamOpen: false, (object _003Cp0_003E, X509Certificate _003Cp1_003E, X509Chain _003Cp2_003E, SslPolicyErrors _003Cp3_003E) => true, null);
			sslStream.AuthenticateAsClient(Server);
		}
		catch (IOException)
		{
			return false;
		}
		catch (SocketException)
		{
			return false;
		}
		finally
		{
			tcpClient?.Close();
		}
		return true;
	}
}
