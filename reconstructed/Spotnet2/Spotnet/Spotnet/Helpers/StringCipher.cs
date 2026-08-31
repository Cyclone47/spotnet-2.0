using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Spotnet.Helpers;

internal static class StringCipher
{
	private const int Keysize = 256;

	private static readonly byte[] InitVectorBytes = Encoding.ASCII.GetBytes("tu8922jkde2j0t89u2");

	private const string PassPhrase = "ie81uiodi3dil!@#$)_~!+DWCKQEC>w/,c.e;ric03";

	public static string Encrypt(string plainText)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(plainText);
		using Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes("ie81uiodi3dil!@#$)_~!+DWCKQEC>w/,c.e;ric03", new byte[8] { 5, 31, 2, 123, 82, 23, 1, 100 });
		byte[] bytes2 = rfc2898DeriveBytes.GetBytes(32);
		using RijndaelManaged rijndaelManaged = new RijndaelManaged();
		rijndaelManaged.Mode = CipherMode.CBC;
		using ICryptoTransform transform = rijndaelManaged.CreateEncryptor(bytes2, InitVectorBytes);
		using MemoryStream memoryStream = new MemoryStream();
		using CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
		cryptoStream.Write(bytes, 0, bytes.Length);
		cryptoStream.FlushFinalBlock();
		return Convert.ToBase64String(memoryStream.ToArray());
	}

	public static string Decrypt(string cipherText)
	{
		byte[] array = Convert.FromBase64String(cipherText);
		using Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes("ie81uiodi3dil!@#$)_~!+DWCKQEC>w/,c.e;ric03", new byte[8] { 5, 31, 2, 123, 82, 23, 1, 100 });
		byte[] bytes = rfc2898DeriveBytes.GetBytes(32);
		using RijndaelManaged rijndaelManaged = new RijndaelManaged();
		rijndaelManaged.Mode = CipherMode.CBC;
		using ICryptoTransform transform = rijndaelManaged.CreateDecryptor(bytes, InitVectorBytes);
		using MemoryStream stream = new MemoryStream(array);
		using CryptoStream cryptoStream = new CryptoStream(stream, transform, CryptoStreamMode.Read);
		byte[] array2 = new byte[array.Length];
		int count = cryptoStream.Read(array2, 0, array2.Length);
		return Encoding.UTF8.GetString(array2, 0, count);
	}
}
