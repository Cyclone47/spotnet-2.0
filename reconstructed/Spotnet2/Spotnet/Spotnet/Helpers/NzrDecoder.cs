using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NLog;

namespace Spotnet.Helpers;

internal static class NzrDecoder
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private static readonly string[] PassPhrase = new string[2] { "ie81uiodi3dil!@#$)_~!+DWCKQEC>w/,c.e;ric03", "e4r&2yq8%GLcZg3v?v+G5JVqVvbk?eZ5n*Z2*ehDdU" };

	private static readonly byte[] InitVectorBytes = Encoding.ASCII.GetBytes("tu8922jkde2j0t89u2");

	internal static string Decode(string cipherText, int key)
	{
		if (key > PassPhrase.Length - 1)
		{
			Log.Debug("Key is out of range: " + key);
			return null;
		}
		try
		{
			byte[] array = Convert.FromBase64String(cipherText);
			string @string;
			using (Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(PassPhrase[key], new byte[8] { 5, 31, 2, 123, 82, 23, 1, 100 }))
			{
				byte[] bytes = rfc2898DeriveBytes.GetBytes(32);
				using RijndaelManaged rijndaelManaged = new RijndaelManaged();
				using ICryptoTransform transform = rijndaelManaged.CreateDecryptor(bytes, InitVectorBytes);
				using MemoryStream stream = new MemoryStream(array);
				using CryptoStream cryptoStream = new CryptoStream(stream, transform, CryptoStreamMode.Read);
				byte[] array2 = new byte[array.Length - 1];
				int count = cryptoStream.Read(array2, 0, array2.Length);
				@string = Encoding.UTF8.GetString(array2, 0, count);
			}
			return @string;
		}
		catch (Exception ex)
		{
			Log.Error("Decoding nzr: " + ex.Message);
			return null;
		}
	}

	internal static string Encode(string plainText, int key)
	{
		if (key > PassPhrase.Length - 1)
		{
			Log.Debug("Key is out of range: " + key);
			return null;
		}
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(plainText);
			string result;
			using (Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(PassPhrase[key], new byte[8] { 5, 31, 2, 123, 82, 23, 1, 100 }))
			{
				byte[] bytes2 = rfc2898DeriveBytes.GetBytes(32);
				using RijndaelManaged rijndaelManaged = new RijndaelManaged();
				rijndaelManaged.Mode = CipherMode.CBC;
				using ICryptoTransform transform = rijndaelManaged.CreateEncryptor(bytes2, InitVectorBytes);
				using MemoryStream memoryStream = new MemoryStream();
				using CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
				cryptoStream.Write(bytes, 0, bytes.Length);
				cryptoStream.FlushFinalBlock();
				result = Convert.ToBase64String(memoryStream.ToArray());
			}
			return result;
		}
		catch (Exception ex)
		{
			Log.Error("Encoding: " + ex.Message);
			return null;
		}
	}
}
