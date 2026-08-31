namespace Spotnet.Model;

internal class ProviderItem
{
	public string Name;

	public string Download;

	public string Upload;

	public string Headers;

	public int DownloadPort;

	public int UploadPort;

	public int HeadersPort;

	public ProviderItem()
	{
		Name = "";
		Download = "";
		Upload = "";
		Headers = "";
	}

	public override string ToString()
	{
		return Name;
	}
}
