using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Spotnet.Phuse.NNTP.Net;

internal class SSLSocket : SocketBase
{
	private static bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors policyErrors)
	{
		return true;
	}

	protected override void InitSocketStream()
	{
		((SslStream)(SocketStream = new SslStream(SocketClient.GetStream(), leaveInnerStreamOpen: false, ValidateRemoteCertificate, null, EncryptionPolicy.AllowNoEncryption))).AuthenticateAsClient(DestinationServer.Host, new X509CertificateCollection(), SslProtocols.Default, checkCertificateRevocation: false);
	}
}
