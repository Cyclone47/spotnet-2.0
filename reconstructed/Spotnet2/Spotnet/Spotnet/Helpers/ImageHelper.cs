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
