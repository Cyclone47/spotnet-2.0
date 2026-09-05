using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Spotnet.Helpers;
using Spotnet.Properties;
using Xunit;

namespace Spotnet.Tests;

/// <summary>
/// Covers the WebP handling added to <see cref="ImageHelper" />. Decoding leans on the Windows
/// WIC codec, which is in-box on Windows 11 but can be absent, so the tests that need it assert
/// the documented fallback instead of failing when it is missing.
/// </summary>
public sealed class WebPSupportTests
{
    /// <summary>The 34 byte lossless 1x1 WebP used for feature detection everywhere.</summary>
    private static readonly byte[] TinyWebP =
    {
        0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45,
        0x42, 0x50, 0x56, 0x50, 0x38, 0x4C, 0x0D, 0x00, 0x00, 0x00,
        0x2F, 0x00, 0x00, 0x00, 0x10, 0x07, 0x10, 0x11, 0x11, 0x88,
        0x88, 0xFE, 0x07, 0x00
    };

    private static byte[] MakePng(int width = 40, int height = 40)
    {
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null,
            Enumerable.Repeat((byte)0x80, width * height * 4).ToArray(), width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    [Fact]
    public void IsWebP_RecognisesTheRiffWebpContainerAndNothingElse()
    {
        Assert.True(ImageHelper.IsWebP(TinyWebP));
        Assert.False(ImageHelper.IsWebP(MakePng()));
        Assert.False(ImageHelper.IsWebP(null));
        Assert.False(ImageHelper.IsWebP(Array.Empty<byte>()));
        // A RIFF container that is not WebP - the form type at offset 8 is what decides.
        var wav = (byte[])TinyWebP.Clone();
        wav[8] = (byte)'W';
        wav[9] = (byte)'A';
        wav[10] = (byte)'V';
        wav[11] = (byte)'E';
        Assert.False(ImageHelper.IsWebP(wav));
    }

    [Fact]
    public void IsSupportedImageMimeType_AcceptsWebpAndIgnoresContentTypeParameters()
    {
        Assert.True(ImageHelper.IsSupportedImageMimeType("image/webp"));
        Assert.True(ImageHelper.IsSupportedImageMimeType("IMAGE/WEBP"));
        Assert.True(ImageHelper.IsSupportedImageMimeType("image/jpeg; charset=binary"));
        Assert.True(ImageHelper.IsSupportedImageMimeType("image/png"));
        Assert.False(ImageHelper.IsSupportedImageMimeType("text/html"));
        Assert.False(ImageHelper.IsSupportedImageMimeType("application/octet-stream"));
        Assert.False(ImageHelper.IsSupportedImageMimeType(null));
        Assert.False(ImageHelper.IsSupportedImageMimeType(""));
    }

    [Fact]
    public void EnsurePostableFormat_LeavesFormatsTheOtherClientsAlreadyReadUntouched()
    {
        byte[] png = MakePng();
        Assert.Same(png, ImageHelper.EnsurePostableFormat(png));
        Assert.Null(ImageHelper.EnsurePostableFormat(null));
    }

    [Fact]
    public void EnsurePostableFormat_RewritesWebPToJpegSoOtherClientsCanReadTheSpot()
    {
        byte[] posted = ImageHelper.EnsurePostableFormat(TinyWebP);
        if (!ImageHelper.IsWebPDecodingAvailable)
        {
            // Documented fallback: refuse the image rather than post something unreadable.
            Assert.Null(posted);
            return;
        }
        Assert.NotNull(posted);
        Assert.False(ImageHelper.IsWebP(posted));
        Assert.Equal(0xFF, posted[0]);
        Assert.Equal(0xD8, posted[1]);
    }

    [Fact]
    public void ToJpeg_ReEncodesWithoutChangingThePixelDimensions()
    {
        byte[] jpeg = ImageHelper.ToJpeg(MakePng(64, 48));
        var frame = BitmapDecoder.Create(new MemoryStream(jpeg), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(64, frame.PixelWidth);
        Assert.Equal(48, frame.PixelHeight);
    }

    [Fact]
    public void LoadDrawingImage_ReadsWebPThroughWicAndNeverThrowsOnJunk()
    {
        string folder = Path.Combine(Path.GetTempPath(), "spotnet-webp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string webp = Path.Combine(folder, "spot.webp");
            File.WriteAllBytes(webp, TinyWebP);
            using (System.Drawing.Image image = ImageHelper.LoadDrawingImage(webp))
            {
                if (ImageHelper.IsWebPDecodingAvailable)
                {
                    Assert.NotNull(image);
                    Assert.Equal(1, image.Width);
                }
                else
                {
                    // No codec: the toolbar loses its clipboard copy, the caller keeps the spot.
                    Assert.Null(image);
                }
            }

            // A GDI+ format still goes straight through GDI+.
            string png = Path.Combine(folder, "spot.png");
            File.WriteAllBytes(png, MakePng(20, 20));
            using (System.Drawing.Image image = ImageHelper.LoadDrawingImage(png))
            {
                Assert.NotNull(image);
                Assert.Equal(20, image.Width);
            }

            string junk = Path.Combine(folder, "spot.bin");
            File.WriteAllBytes(junk, new byte[64]);
            Assert.Null(ImageHelper.LoadDrawingImage(junk));
            Assert.Null(ImageHelper.LoadDrawingImage(Path.Combine(folder, "missing.png")));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("nl")]
    public void PictureFileFilterOffersWebPAndTheMissingCodecMessageIsTranslated(string culture)
    {
        var requestedCulture = CultureInfo.GetCultureInfo(culture);
        string filter = Words.ResourceManager.GetString("FilterToPicture", requestedCulture);
        string codecMissing = Words.ResourceManager.GetString("WebPCodecMissing", requestedCulture);
        Assert.Contains("*.webp", filter);
        // The filter is pipe separated pairs of description and pattern; an odd count means
        // the file dialog would throw when it is opened.
        Assert.Equal(0, filter.Split('|').Length % 2);
        Assert.False(string.IsNullOrWhiteSpace(codecMissing));
        Assert.Contains("WebP", codecMissing, StringComparison.OrdinalIgnoreCase);
    }
}
