using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic;

namespace Spotnet.Extensions;

public static class StringExtension
{
	private static readonly byte[] Entropy = Encoding.Unicode.GetBytes("just a salt, it is not a password");

	public static bool IsNullOrEmpty(this string str)
	{
		return string.IsNullOrEmpty(str);
	}

	public static bool IsNullOrWhiteSpace(this string str)
	{
		return string.IsNullOrWhiteSpace(str);
	}

	public static bool IsNullOrEmpty(this Array array)
	{
		if (array != null)
		{
			return array.Length == 0;
		}
		return true;
	}

	public static bool EqualsIgnoreCase(this string str1, string str2)
	{
		if (str1 == null || str2 == null)
		{
			return false;
		}
		return str1.ToUpperInvariant().Equals(str2.ToUpperInvariant());
	}

	public static bool EqualsIgnoreCase(this string str1, object obj)
	{
		if (str1 == null || obj == null)
		{
			return false;
		}
		return str1.ToUpperInvariant().Equals(obj.ToString().ToUpperInvariant());
	}

	public static string Format(this string str, params object[] parameters)
	{
		return string.Format(str, parameters);
	}

	public static string Format(this string str, object param1)
	{
		return string.Format(str, param1);
	}

	public static string Format(this string str, object param1, object param2)
	{
		return string.Format(str, param1, param2);
	}

	public static string Format(this string str, object param1, object param2, object param3)
	{
		return string.Format(str, param1, param2, param3);
	}

	public static string FormatFromDictionary(this string formatString, Dictionary<string, string> valueDict)
	{
		int num = 0;
		StringBuilder stringBuilder = new StringBuilder(formatString);
		Dictionary<string, int> keyToInt = new Dictionary<string, int>();
		foreach (KeyValuePair<string, string> item in valueDict)
		{
			stringBuilder = stringBuilder.Replace("{" + item.Key + "}", "{" + num + "}");
			keyToInt.Add(item.Key, num);
			num++;
		}
		return string.Format(stringBuilder.ToString(), ((IEnumerable<object>)(from x in valueDict
			orderby keyToInt[x.Key]
			select x.Value)).ToArray());
	}

	public static byte[] ToByteArray(this string str)
	{
		byte[] array = new byte[str.Length * 2];
		Buffer.BlockCopy(str.ToCharArray(), 0, array, 0, array.Length);
		return array;
	}

	public static string ToString(this byte[] bytes)
	{
		char[] array = new char[bytes.Length / 2];
		Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
		return new string(array);
	}

	public static string Repeat(this string s, int n)
	{
		return new string(Enumerable.Range(0, n).SelectMany((int x) => s).ToArray());
	}

	public static string Repeat(this char c, int n)
	{
		return new string(c, n);
	}

	public static string ReplaceIgnoreCase(this string s, string oldValue, string newValue)
	{
		return Strings.Replace(s, oldValue, newValue, 1, -1, CompareMethod.Text);
	}

	public static string DecodeFromUtf8(this string utf8String)
	{
		if (utf8String.IsNullOrWhiteSpace())
		{
			return utf8String;
		}
		byte[] array = new byte[utf8String.Length];
		for (int i = 0; i < utf8String.Length; i++)
		{
			if ('\0' <= utf8String[i] && utf8String[i] <= 'ÿ')
			{
				array[i] = (byte)utf8String[i];
				continue;
			}
			throw new Exception("The char must be in byte's range");
		}
		return Encoding.Default.GetString(array, 0, array.Length);
	}

	public static string ReadLine(this string text, int lineNumber)
	{
		StringReader stringReader = new StringReader(text);
		int num = 0;
		string text2;
		do
		{
			num++;
			text2 = stringReader.ReadLine();
		}
		while (text2 != null && num < lineNumber);
		if (num != lineNumber)
		{
			return string.Empty;
		}
		return text2;
	}

	public static int CountLines(this string str)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		if (str == string.Empty)
		{
			return 0;
		}
		int num = -1;
		int num2 = 0;
		while (-1 != (num = str.IndexOf(Environment.NewLine, num + 1, StringComparison.Ordinal)))
		{
			num2++;
		}
		return num2 + 1;
	}

	public static string EncryptString(SecureString input)
	{
		return Convert.ToBase64String(ProtectedData.Protect(Encoding.Unicode.GetBytes(input.ToInsecureString()), Entropy, DataProtectionScope.CurrentUser));
	}

	public static SecureString DecryptString(string encryptedData)
	{
		try
		{
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), Entropy, DataProtectionScope.CurrentUser);
			return Encoding.Unicode.GetString(bytes).ToSecureString();
		}
		catch
		{
			return new SecureString();
		}
	}

	public static SecureString ToSecureString(this string input)
	{
		SecureString secureString = new SecureString();
		foreach (char c in input)
		{
			secureString.AppendChar(c);
		}
		secureString.MakeReadOnly();
		return secureString;
	}

	public static string ToInsecureString(this SecureString input)
	{
		IntPtr intPtr = Marshal.SecureStringToBSTR(input);
		try
		{
			return Marshal.PtrToStringBSTR(intPtr);
		}
		finally
		{
			Marshal.ZeroFreeBSTR(intPtr);
		}
	}
}
