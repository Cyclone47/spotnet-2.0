using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic;
using NLog;
using Spotnet.Extensions;
using Spotnet.Model;
using Spotnet.Properties;

namespace Spotnet.Helpers;

internal class ImageHelper
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	/// <summary>
	/// Asks the user for an avatar image and returns it as a 32x32 base64 string.
	/// </summary>
	/// <remarks>
	/// Lives here rather than on a browser page because both the settings screen and the
	/// spot page's author menu offer it.
	/// </remarks>
	internal static bool ChangeAvatar(out string newAvatar)
	{
		newAvatar = "";
		try
		{
			string initialDirectory = (Settings.Default.AvatarFolder.IsNullOrEmpty() ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : Settings.Default.AvatarFolder);
			System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog
			{
				Title = Words.ChangeAvatar,
				InitialDirectory = initialDirectory,
				Filter = Words.FilterToAvatar,
				FilterIndex = 1,
				RestoreDirectory = true,
				CheckFileExists = true,
				ShowReadOnly = false,
				DefaultExt = "gif",
				Multiselect = false
			};
			if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			{
				return false;
			}
			System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(openFileDialog.FileName);
			if (bitmap.Width > 32 || bitmap.Height > 32)
			{
				bitmap = bitmap.Resize(32, 32);
			}
			newAvatar = Convert.ToBase64String(bitmap.ToByteArray());
			Settings.Default.AvatarFolder = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
			Settings.Default.Save();
			return true;
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
		}
		return false;
	}

	internal static BitmapImage BytesToBitmapImage(byte[] imageData)
	{
		if (imageData == null || imageData.Length == 0)
		{
			return null;
		}
		BitmapImage bitmapImage = new BitmapImage();
		using (MemoryStream memoryStream = new MemoryStream(imageData))
		{
			memoryStream.Position = 0L;
			bitmapImage.BeginInit();
			bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.UriSource = null;
			bitmapImage.StreamSource = memoryStream;
			bitmapImage.EndInit();
		}
		bitmapImage.Freeze();
		return bitmapImage;
	}

	internal static byte[] ImageResize(byte[] imageBytes, int width, int height)
	{
		if (imageBytes == null || imageBytes.Length == 0)
		{
			return null;
		}
		BitmapFrame bitmapFrame = BitmapDecoder.Create(new MemoryStream(imageBytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None).Frames[0];
		double num = Math.Min((double)width / bitmapFrame.Width, (double)height / bitmapFrame.Height);
		BitmapFrame item = BitmapFrame.Create(new TransformedBitmap(bitmapFrame, new ScaleTransform(num * 96.0 / bitmapFrame.DpiX, num * 96.0 / bitmapFrame.DpiY, 0.0, 0.0)));
		JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder();
		jpegBitmapEncoder.Frames.Add(item);
		using MemoryStream memoryStream = new MemoryStream();
		jpegBitmapEncoder.Save(memoryStream);
		byte[] array = memoryStream.ToArray();
		return (array.Length > imageBytes.Length) ? imageBytes : array;
	}

	/// <summary>
	/// The 34 byte lossless WebP the browsers use for feature detection - a single pixel.
	/// Decoding it is the cheapest way to ask WIC whether a WebP codec is registered at all.
	/// </summary>
	private static readonly byte[] WebPProbe = new byte[34]
	{
		0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45,
		0x42, 0x50, 0x56, 0x50, 0x38, 0x4C, 0x0D, 0x00, 0x00, 0x00,
		0x2F, 0x00, 0x00, 0x00, 0x10, 0x07, 0x10, 0x11, 0x11, 0x88,
		0x88, 0xFE, 0x07, 0x00
	};

	/// <summary>Content types we accept for a spot image.</summary>
	/// <remarks>
	/// WebP only decodes when Windows has the codec (see <see cref="IsWebPDecodingAvailable" />),
	/// and <see cref="EnsurePostableFormat" /> normalises it away again before anything is posted.
	/// </remarks>
	private static readonly string[] ImageMimeTypes = new string[5] { "image/png", "image/gif", "image/jpeg", "image/bmp", "image/webp" };

	private static bool? _isWebPDecodingAvailable;

	/// <summary>
	/// Whether Windows can decode WebP. The codec is in-box on Windows 11 and a free
	/// "Webp Image Extensions" download from the Store on Windows 10; we ship none of our own,
	/// so every WebP path in the client hangs off this.
	/// </summary>
	internal static bool IsWebPDecodingAvailable
	{
		get
		{
			if (!_isWebPDecodingAvailable.HasValue)
			{
				_isWebPDecodingAvailable = ProbeWebPCodec();
			}
			return _isWebPDecodingAvailable.Value;
		}
	}

	private static bool ProbeWebPCodec()
	{
		try
		{
			BitmapFrame bitmapFrame = BitmapDecoder.Create(new MemoryStream(WebPProbe), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
			bool flag = bitmapFrame.PixelWidth == 1 && bitmapFrame.PixelHeight == 1;
			Log.Info("WebP decoding is " + (flag ? "available" : "not available") + " on this system");
			return flag;
		}
		catch (Exception ex)
		{
			Log.Info("No WebP codec registered on this system: " + ex.Message);
			return false;
		}
	}

	/// <summary>A WebP file is a RIFF container whose form type is "WEBP".</summary>
	internal static bool IsWebP(byte[] imageBytes)
	{
		if (imageBytes == null || imageBytes.Length < 12)
		{
			return false;
		}
		return imageBytes[0] == (byte)'R' && imageBytes[1] == (byte)'I' && imageBytes[2] == (byte)'F' && imageBytes[3] == (byte)'F' && imageBytes[8] == (byte)'W' && imageBytes[9] == (byte)'E' && imageBytes[10] == (byte)'B' && imageBytes[11] == (byte)'P';
	}

	internal static bool IsSupportedImageMimeType(string contentType)
	{
		if (contentType.IsNullOrEmpty())
		{
			return false;
		}
		// "image/jpeg; charset=binary" and friends - the parameters are not ours to care about.
		string mime = contentType.Split(';')[0].Trim();
		return ImageMimeTypes.Any((string t) => string.Equals(t, mime, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Decodes an image into a GDI+ bitmap, falling back to the WIC decoders for anything GDI+
	/// cannot open itself. GDI+ knows BMP, GIF, JPEG, PNG and TIFF and nothing beyond that, so
	/// WebP needs the detour.
	/// </summary>
	/// <returns>Null when the image cannot be decoded at all - callers have to cope.</returns>
	internal static System.Drawing.Image LoadDrawingImage(string file)
	{
		try
		{
			return System.Drawing.Image.FromFile(file);
		}
		catch (Exception ex)
		{
			Log.Debug("GDI+ cannot read " + file + ": " + ex.Message + " - retrying with the WIC decoders");
		}
		try
		{
			return WicDecodeToDrawingImage(File.ReadAllBytes(file));
		}
		catch (Exception ex2)
		{
			Log.Warn("Failed to decode " + file + ": " + ex2.Message);
			return null;
		}
	}

	private static System.Drawing.Image WicDecodeToDrawingImage(byte[] imageBytes)
	{
		if (IsWebP(imageBytes) && !IsWebPDecodingAvailable)
		{
			throw new NotSupportedException(Words.WebPCodecMissing);
		}
		BitmapFrame bitmapFrame = BitmapDecoder.Create(new MemoryStream(imageBytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapFrame));
		using MemoryStream memoryStream = new MemoryStream();
		pngBitmapEncoder.Save(memoryStream);
		memoryStream.Position = 0L;
		// Image.FromStream keeps reading from the stream it was handed, so return a copy that owns
		// its own pixels - the stream is long gone by the time the toolbar draws it.
		using System.Drawing.Image image = System.Drawing.Image.FromStream(memoryStream);
		return new System.Drawing.Bitmap(image);
	}

	/// <summary>
	/// Normalises an image to something every Spotnet client can read before it goes out on
	/// Usenet. Spotnet 2.x reads spot images with GDI+ and Spotweb with PHP-GD, neither of which
	/// knows WebP, so a WebP the user picked is re-encoded to JPEG rather than posted as-is.
	/// </summary>
	/// <returns>Null when the image cannot be decoded, so the caller can refuse the post.</returns>
	internal static byte[] EnsurePostableFormat(byte[] imageBytes)
	{
		if (imageBytes.IsNullOrEmpty() || !IsWebP(imageBytes))
		{
			return imageBytes;
		}
		if (!IsWebPDecodingAvailable)
		{
			Log.Warn("Cannot convert the WebP image: no WebP codec on this system");
			return null;
		}
		try
		{
			return ToJpeg(imageBytes);
		}
		catch (Exception ex)
		{
			Log.Exception(ex);
			return null;
		}
	}

	/// <summary>Re-encodes an image as JPEG at its original size. Transparency is lost.</summary>
	internal static byte[] ToJpeg(byte[] imageBytes, int quality = 90)
	{
		BitmapFrame bitmapFrame = BitmapDecoder.Create(new MemoryStream(imageBytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
		JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder
		{
			QualityLevel = quality
		};
		jpegBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapFrame));
		using MemoryStream memoryStream = new MemoryStream();
		jpegBitmapEncoder.Save(memoryStream);
		return memoryStream.ToArray();
	}

	internal static byte[] LoadSpotThumb(SpotEx spotEx)
	{
		try
		{
			byte[] array = spotEx.ImageSource;
			if (array.IsNullOrEmpty() && (SpotsSourceEnum.FileCache | SpotsSourceEnum.ImageFromFullImagesGroup | SpotsSourceEnum.ImageByUrl | SpotsSourceEnum.ImageFromThumbsGroup).HasFlag(SpotsSourceEnum.ImageFromThumbsGroup))
			{
				array = LoadSpotImageFromUsenetThumb(spotEx);
			}
			if (array.IsNullOrEmpty())
			{
				return null;
			}
			return ImageResize(array, 143, 210);
		}
		catch (Exception ex)
		{
			Log.Error(ex.Message);
			return null;
		}
	}

	internal static byte[] LoadSpotFullImage(SpotEx spotEx)
	{
		try
		{
			byte[] array = null;
			if ((SpotsSourceEnum.FileCache | SpotsSourceEnum.ImageFromFullImagesGroup | SpotsSourceEnum.ImageByUrl | SpotsSourceEnum.ImageFromThumbsGroup).HasFlag(SpotsSourceEnum.ImageByUrl))
			{
				array = LoadSpotImageFromUrl(spotEx);
			}
			if (array.IsNullOrEmpty() && (SpotsSourceEnum.FileCache | SpotsSourceEnum.ImageFromFullImagesGroup | SpotsSourceEnum.ImageByUrl | SpotsSourceEnum.ImageFromThumbsGroup).HasFlag(SpotsSourceEnum.ImageFromFullImagesGroup))
			{
				array = LoadSpotImageFromUsenet(spotEx);
			}
			if (array.IsNullOrEmpty())
			{
				return null;
			}
			return array;
		}
		catch (Exception ex)
		{
			Log.Error(ex.Message);
			return null;
		}
	}

	private static byte[] LoadSpotImageFromUrl(SpotEx spotEx)
	{
		if (spotEx.Image.IsNullOrEmpty())
		{
			return null;
		}
		return new WebClient().DownloadData(spotEx.Image);
	}

	private static byte[] LoadSpotImageFromUsenet(SpotEx spotEx)
	{
		if (spotEx.ImageID.IsNullOrEmpty())
		{
			return null;
		}
		List<string> list = (from t in Strings.Split(spotEx.ImageID)
			select SpotHelper.MakeMsg(t)).ToList();
		if (list.Count == 0)
		{
			throw new Exception(Words.NoSegments);
		}
		if (SpotHelper.GetFullImageBinary(list, out var imageBytes, out var sError))
		{
			return imageBytes;
		}
		Log.Warn("Failed to load image: " + sError);
		return null;
	}

	private static byte[] LoadSpotImageFromUsenetThumb(SpotEx spotEx)
	{
		string text = SpotHelper.MakeMsg(ThumbsUploader.GetThumbMessageId(spotEx.MessageId));
		Log.Trace("Loading thumb {0}", text);
		if (SpotHelper.GetThumbImageBinary(new List<string> { text }, out var imageBytes, out var _))
		{
			return imageBytes;
		}
		return null;
	}
}
