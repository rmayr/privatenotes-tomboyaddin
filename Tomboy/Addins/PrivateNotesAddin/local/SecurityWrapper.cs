using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Tomboy.PrivateNotes.Crypto;
using Tomboy.PrivateNotes;
using Tomboy.PrivateNotes.Adress;

namespace Tomboy.Sync
{
	/// <summary>
	/// security wrapper for the local encrypted FileSystemSynchronization
	/// </summary>
	class SecurityWrapper
	{
#region shared versions
		/// <summary>
		/// TODO
		/// </summary>
		/// <param name="_inputFile"></param>
		/// <param name="_toFile"></param>
		/// <param name="_password">currently not used</param>
		/// <param name="_recipients"></param>
		public static void CopyAndEncryptShared(String _inputFile, String _toFile, byte[] _password, List<String> _recipients)
		{
			ShareCryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat() as ShareCryptoFormat;
			if (ccf == null)
			{
				Logger.Warn("wrong encryption format!");
				throw new Exception("For sharing, the crypto format has to be a ShareCryptoFormat instance!");
			}

			FileStream input = File.OpenRead(_inputFile);
			MemoryStream membuf = new MemoryStream();
			int b = input.ReadByte();
			while (b >= 0)
			{
				membuf.WriteByte((byte)b);
				b = input.ReadByte();
			}
			input.Close();

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(_password, out salt);

			
			if (!ccf.PreHashedPasswordSupported())
			{
				// reset the key to the plain password
				key = _password;
			}

			// use shared method
			ccf.WriteCompatibleFile(AdressBookFactory.Instance().GetDefault(), _toFile, membuf.ToArray(), _recipients);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_fileName"></param>
		/// <param name="_data"></param>
		/// <param name="_password">currently not used!</param>
		/// <param name="_recipients"></param>
		public static void SaveAsSharedEncryptedFile(String _fileName, byte[] _data, byte[] _password, List<String> _recipients)
		{
			ShareCryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat() as ShareCryptoFormat;
			if (ccf == null)
			{
				Logger.Warn("wrong encryption format!");
				throw new Exception("For sharing, the crypto format has to be a ShareCryptoFormat instance!");
			}

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(_password, out salt);
			if (!ccf.PreHashedPasswordSupported())
			{
				// reset the key to the plain password
				key = _password;
			}
			ccf.WriteCompatibleFile(AdressBookFactory.Instance().GetDefault(), _fileName, _data, _recipients);
		}

#endregion

#region normal wrappers

		public static void CopyAndEncrypt(String _inputFile, String _toFile, byte[] _password)
		{
			FileStream input = File.OpenRead(_inputFile);
			MemoryStream membuf = new MemoryStream();
			int b = input.ReadByte();
			while (b >= 0)
			{
				membuf.WriteByte((byte)b);
				b = input.ReadByte();
			}
			input.Close();

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(_password, out salt);

			CryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();
			if (!ccf.PreHashedPasswordSupported())
			{
				// reset the key to the plain password
				key = _password;
			}

			ccf.WriteCompatibleFile(_toFile, membuf.ToArray(), key, salt);
		}

		public static void SaveAsEncryptedFile(String _fileName, byte[] _data, byte[] _password)
		{
			CryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();

			byte[] salt;
			byte[] key = AESUtil.CalculateSaltedHash(_password, out salt);
			if (!ccf.PreHashedPasswordSupported())
			{
				// reset the key to the plain password
				key = _password;
			}
			ccf.WriteCompatibleFile(_fileName, _data, key, salt);
		}

		public static Stream DecryptFromStream(String _inputFile, Stream _s, byte[] _key, out bool	_wasOk)
		{
			CryptoFormat ccf = CryptoFormatProviderFactory.INSTANCE.GetCryptoFormat();
			byte[] data = ccf.DecryptFromStream(_inputFile, _s, _key, out _wasOk);
			if (!_wasOk)
				return null;

			return new MemoryStream(data);
		}

#endregion
	}
}
