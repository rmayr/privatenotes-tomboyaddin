using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Tomboy.PrivateNotes.Adress;

namespace Tomboy.PrivateNotes.Crypto
{

	/// <summary>
	/// a crypto format which does no encryption at all. This is _ONLY_ for debuggin purposes!
	/// </summary>
	class NullCryptoFormat : CryptoFormat
	{
		/// <summary>
		/// for nullcryptoformat it doesn't matter, since we don't use the password anyway
		/// </summary>
		/// <returns></returns>
		public bool PreHashedPasswordSupported()
		{
			return true;
		}

		public bool WriteCompatibleFile(String _filename, byte[] _content, byte[] _key, byte[] _salt)
		{
			FileStream fout = File.Create(_filename);
			fout.Write(_content, 0, _content.Length);
			fout.Close();
			return true;
		}

		public byte[] DecryptFromStream(String _filename, Stream fin, byte[] _key, out bool _wasOk)
		{
			int read = fin.ReadByte();
			MemoryStream membuf = new MemoryStream();
			while (read >= 0)
			{
				membuf.WriteByte((byte)read);
				read = fin.ReadByte();
			}
			_wasOk = true;
			return membuf.ToArray();
		}

		public byte[] DecryptFile(String _filename, byte[] _key, out bool _wasOk)
		{
			FileStream fin = File.OpenRead(_filename);
			return DecryptFromStream(_filename, fin, _key, out _wasOk);
		}

		public int Version()
		{
			return 9;
		}
	}

	/// <summary>
	/// a crypto format which uses the gpg utility (program) for encryption and decryption
	/// </summary>
	class GpgCryptoFormat : ShareCryptoFormat
	{
		static String gpgExe = null;
		static String tempDir = null;

		public GpgCryptoFormat()
		{
			// only needed once
			if (gpgExe == null)
			{
				gpgExe = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG) as String;
				if (gpgExe == null)
				{
					throw new InvalidOperationException("gpg not configured yet!");
				}
			}
			if (tempDir == null)
			{
				tempDir = Path.Combine(Services.NativeApplication.CacheDirectory, "gpg_" + System.Guid.NewGuid().ToString());
				Directory.CreateDirectory(tempDir);
			}
		}

		public bool PreHashedPasswordSupported()
		{
			return false;
		}


		public bool WriteCompatibleFile(String _filename, byte[] _content, byte[] _key, byte[] _salt)
		{
			String tempFileName = Path.Combine(tempDir, _filename + ".clear");
			FileStream fout = File.Create(tempFileName);
			fout.Write(_content, 0, _content.Length);
			fout.Close();
			
			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = gpgExe;
			proc.StartInfo.Arguments = "--batch --symmetric --passphrase " + Util.FromBytes(_key) + " --output " + _filename + " " + tempFileName ;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.Start();
			String data = proc.StandardOutput.ReadToEnd();
			Logger.Info(data);

			File.Delete(tempFileName);

			return true;
		}

		public byte[] DecryptFromStream(String _filename, Stream fin, byte[] _key, out bool _wasOk)
		{
			String tempfile = Path.Combine(tempDir, _filename + ".enc");
			File.Delete(tempfile);
			FileStream fout = File.Create(tempfile);
			int bt = fin.ReadByte();
			while (bt >= 0)
			{
				fout.WriteByte((byte)bt);
				bt = fin.ReadByte();
			}
			fout.Close();

			// ERROR FIXME: this would normally not work, because the filename is different now!!!
			byte[] decrypted = DecryptFile(tempfile, _key, out _wasOk);
			
			File.Delete(tempfile);

			return decrypted;
		}

		public byte[] DecryptFile(String _filename, byte[] _key, out bool _wasOk)
		{
			String tempfile = Path.Combine(tempDir, _filename + ".dec");
			File.Delete(tempfile);

			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = gpgExe;
			proc.StartInfo.Arguments = "--batch -d --passphrase " + Util.FromBytes(_key) + " --output " + tempfile + " " + _filename;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.Start();
			String data = proc.StandardOutput.ReadToEnd();
			Logger.Info(data);

			BufferedStream fin = new BufferedStream(File.OpenRead(tempfile));
			long len = fin.Length;
			if (len > Int32.MaxValue)
			{
				File.Delete(tempfile);
				throw new Exception("huge files not supported!!!");
			}
			byte[] buffer = new byte[len];
			int read = fin.Read(buffer, 0, (int)len);
			fin.Close();

			File.Delete(tempfile);

			if (read != len)
			{
				throw new Exception("not the whole file was read!");
			}

			_wasOk = true;
			return buffer;
		}



		public bool WriteCompatibleFile(AdressBook _adressProvider, String _filename, byte[] _content, List<String> _recipients)
		{
			// TODO include the filename!
			String tempFileName = Path.Combine(tempDir, _filename + ".clear");
			FileStream fout = File.Create(tempFileName);
			fout.Write(_content, 0, _content.Length);
			fout.Close();

			// build the commandline arguments
			StringBuilder args = new StringBuilder();
			args.Append("--batch");
			foreach (String r in _recipients)
			{
				args.Append(" -r ");
				args.Append(r);
			}
			args.Append(" --output ");
			args.Append(_filename);
			args.Append(" ");
			args.Append(tempFileName);

			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = gpgExe;
			proc.StartInfo.Arguments = args.ToString();
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.Start();
			String data = proc.StandardOutput.ReadToEnd();
			Logger.Info(data);

			File.Delete(tempFileName);

			return true;
		}

		public byte[] DecryptFile(AdressBook _adressProvider, String _filename, out List<String> _recipients, out bool _wasOk)
		{
			// TODO use temp file
			String tempFileName = _filename + ".decrypted";

			StringBuilder args = new StringBuilder("-d ");
			args.Append("--output ");
			args.Append(tempFileName);
			args.Append(" ");
			args.Append(_filename);

			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = gpgExe;
			proc.StartInfo.Arguments = args.ToString();
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.Start();
			String data = proc.StandardOutput.ReadToEnd();
			Logger.Info(data);

			_recipients = new List<String>();

			BufferedStream fin = new BufferedStream(File.OpenRead(tempFileName));
			long len = fin.Length;
			if (len > Int32.MaxValue)
			{
				File.Delete(tempFileName);
				throw new Exception("huge files not supported!!!");
			}
			byte[] buffer = new byte[len];
			int read = fin.Read(buffer, 0, (int)len);
			fin.Close();

			File.Delete(tempFileName);

			if (read != len)
			{
				_wasOk = false;
				return null;
			}

			// TODO: parse recipients!
			_recipients.Add("not implemented yet");

			_wasOk = true;
			return buffer;

		}


		public int Version()
		{
			return 10;
		}

	}


	class CryptoFileFormatRev1 : CryptoFormat
	{

		public const ushort CRYPTO_VERSION = 1;

		public int Version()
		{
			return CRYPTO_VERSION;
		}

		public bool PreHashedPasswordSupported()
		{
			return true;
		}

		/// <summary>
		/// writes the given data in the PrivateNotes-crypt-format to the harddrive
		/// </summary>
		/// <param name="_filename"></param>
		/// <param name="_content"></param>
		/// <param name="_key"></param>
		/// <returns></returns>
		public bool WriteCompatibleFile(String _filename, byte[] _content, byte[] _key, byte[] _salt)
		{
			Console.WriteLine("writing " + _filename);

			byte[] paddedContent = Util.padWithLengthInfo(_content, 16);


			byte[] doubleHashedKey = AESUtil.CalculateSaltedHash(_key, _salt);

			Util.AssureFileExists(_filename);
			FileStream fout = new FileStream(_filename, FileMode.Truncate, FileAccess.Write);
			
			byte[] version = System.BitConverter.GetBytes(CRYPTO_VERSION);
			
			long now = Util.ConvertToUnixTimestamp(DateTime.Now.ToUniversalTime());
			Console.WriteLine("long value: " + now);
			byte[] dateTime = System.BitConverter.GetBytes(now);

			// calculate check-hash (to verify nothing has been altered)
			MemoryStream membuf = new MemoryStream();
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));
			membuf.Write(version, 0, version.Length);
			membuf.Write(dateTime, 0, dateTime.Length);
			membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
			membuf.Write(paddedContent, 0, paddedContent.Length);
			byte[] dataHashValue = AESUtil.CalculateHash(membuf.ToArray());

			MemoryStream cryptMe = new MemoryStream();
			cryptMe.Write(dataHashValue, 0, dataHashValue.Length);
			cryptMe.Write(paddedContent, 0, paddedContent.Length);

			byte[] cryptedData = AESUtil.Encrypt(_key, cryptMe.ToArray());

			fout.Write(version, 0, version.Length);
			fout.Write(dateTime, 0, dateTime.Length);
			fout.Write(doubleHashedKey, 0, doubleHashedKey.Length);
			fout.Write(_salt, 0, _salt.Length);
			fout.Write(cryptedData, 0, cryptedData.Length);
			fout.Close();

			Console.WriteLine("wrote file");
			return true;
		}

		public byte[] DecryptFromStream(String _filename, Stream fin, byte[] _key, out bool _wasOk)
		{
			_wasOk = false;
			Console.WriteLine();
			Console.WriteLine("checking " + _filename);

			byte[] version = new byte[2];
			byte[] datetime = new byte[8];
			byte[] keyVerifyValue = new byte[32];
			byte[] keySaltValue = new byte[32];
			byte[] dataHashValue = new byte[32];
			byte[] cryptedData = null;
			MemoryStream membuf = new MemoryStream();

			if (fin.Read(version, 0, version.Length) != version.Length)
				throw new FormatException("file seems to be corrupt (version)");
			if (fin.Read(datetime, 0, datetime.Length) != datetime.Length)
				throw new FormatException("file seems to be corrupt (date)");
			if (fin.Read(keyVerifyValue, 0, keyVerifyValue.Length) != keyVerifyValue.Length)
				throw new FormatException("file seems to be corrupt (keyVerify)");
			if (fin.Read(keySaltValue, 0, keySaltValue.Length) != keySaltValue.Length)
				throw new FormatException("file seems to be corrupt (keySalt)");
				
			byte[] singleHashedKey = AESUtil.CalculateSaltedHash(_key, keySaltValue);
			byte[] doubleHashedKey = AESUtil.CalculateSaltedHash(singleHashedKey, keySaltValue);

			if (System.BitConverter.ToUInt16(version, 0) != CRYPTO_VERSION)
			{
				throw new EncryptionException("Wrong Version");
			}

			if (!Util.ArraysAreEqual(keyVerifyValue, doubleHashedKey))
			{
				throw new PasswordException("Wrong Password");
			}

			// read rest of file (encrypted data)
			int data = fin.ReadByte();
			while (data >= 0)
			{
				membuf.WriteByte((byte)data);
				data = fin.ReadByte();
			}
			cryptedData = membuf.ToArray();

			// build data that should be in hash
			membuf = new MemoryStream();
			// filename (without path) as byte[]
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));

			byte[] realData = null;
			{
				byte[] decryptedData = AESUtil.Decrypt(singleHashedKey, cryptedData);
				// get first 32 bytes of decrypted data, this is the control-hash (dataHashValue)
				System.Array.Copy(decryptedData, dataHashValue, dataHashValue.Length);

				byte[] otherData = new byte[decryptedData.Length - dataHashValue.Length];
				System.Array.Copy(decryptedData, dataHashValue.Length, otherData, 0, otherData.Length);
				realData = Util.getDataFromPaddedWithLengthInfo(otherData);

				// write things that are used for the verification-hash into the membuf buffer
				membuf.Write(version, 0, version.Length);
				membuf.Write(datetime, 0, datetime.Length);
				membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
				membuf.Write(otherData, 0, otherData.Length);
			}

			byte[] dataToHash = membuf.ToArray();
			byte[] dataHashToCompare = AESUtil.CalculateHash(dataToHash);
			if (!Util.ArraysAreEqual(dataHashValue, dataHashToCompare))
			{
				Console.WriteLine("Hashes don't match!!!! Data may have been manipulated!");
				return null;
			}

			long fileDate = System.BitConverter.ToInt64(datetime, 0);
			DateTime dateTimeObj = Util.ConvertFromUnixTimestamp(fileDate);
			dateTimeObj = dateTimeObj.ToLocalTime(); // because it's stored in utc
			Console.WriteLine("data seems ok, file is from " + dateTimeObj.ToShortDateString() + " " + dateTimeObj.ToShortTimeString());
			Console.WriteLine("note data:");
			Console.WriteLine(Util.FromBytes(realData));
			Console.WriteLine("-=END OF NOTE=-");

			_wasOk = true;
			return realData;
		}

		public byte[] DecryptFile(String _filename, byte[] _key, out bool _wasOk)
		{
			Stream s = new FileStream(_filename, FileMode.Open, FileAccess.Read);
			try
			{
				byte[] result = DecryptFromStream(_filename, s, _key, out _wasOk);
				return result;
			}
			finally
			{
				s.Close();
			}
		}

	}

	/// <summary>
	/// this is the old version which doesn't use the salted hashes and shouldn't be used any more
	/// </summary>
	class CryptoFileFormatRev0 : CryptoFormat
	{
		public const ushort CRYPTO_VERSION = 0;

		public int Version()
		{
			return CRYPTO_VERSION;
		}

		public bool PreHashedPasswordSupported()
		{
			return true;
		}

		/// <summary>
		/// writes the given data in the PrivateNotes-crypt-format to the harddrive
		/// </summary>
		/// <param name="_filename"></param>
		/// <param name="_content"></param>
		/// <param name="_key"></param>
		/// <param name="_salt">ignored</param>
		/// <returns></returns>
		public bool WriteCompatibleFile(String _filename, byte[] _content, byte[] _pw, byte[] _salt)
		{
			Console.WriteLine("writing " + _filename);

			byte[] paddedContent = Util.padWithLengthInfo(_content, 16);

			byte[] singleHashedKey = AESUtil.CalculateHash(_pw);
			byte[] doubleHashedKey = AESUtil.CalculateHash(singleHashedKey);

			Util.AssureFileExists(_filename);
			FileStream fout = new FileStream(_filename, FileMode.Truncate, FileAccess.Write);
			long now = Util.ConvertToUnixTimestamp(DateTime.Now.ToUniversalTime());
			Console.WriteLine("long value: " + now);
			byte[] dateTime = System.BitConverter.GetBytes(now);

			// calculate check-hash (to verify nothing has been altered)
			MemoryStream membuf = new MemoryStream();
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));
			membuf.Write(dateTime, 0, dateTime.Length);
			membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
			membuf.Write(paddedContent, 0, paddedContent.Length);
			byte[] dataHashValue = AESUtil.CalculateHash(membuf.ToArray());

			MemoryStream cryptMe = new MemoryStream();
			cryptMe.Write(dataHashValue, 0, dataHashValue.Length);
			cryptMe.Write(paddedContent, 0, paddedContent.Length);

			byte[] cryptedData = AESUtil.Encrypt(singleHashedKey, cryptMe.ToArray());

			fout.Write(dateTime, 0, dateTime.Length);
			fout.Write(doubleHashedKey, 0, doubleHashedKey.Length);
			fout.Write(cryptedData, 0, cryptedData.Length);
			fout.Close();

			Console.WriteLine("wrote file");
			return true;
		}

		public byte[] DecryptFromStream(String _filename, Stream fin, byte[] _pw, out bool _wasOk)
		{
			_wasOk = false;
			Console.WriteLine();
			Console.WriteLine("checking " + _filename);

			byte[] singleHashedKey = AESUtil.CalculateHash(_pw);
			byte[] doubleHashedKey = AESUtil.CalculateHash(singleHashedKey);

			byte[] datetime = new byte[8];
			byte[] keyVerifyValue = new byte[32];
			byte[] dataHashValue = new byte[32];
			byte[] cryptedData = null;
			MemoryStream membuf = new MemoryStream();

			if (fin.Read(datetime, 0, datetime.Length) != datetime.Length)
				throw new FormatException("file seems to be corrupt");
			if (fin.Read(keyVerifyValue, 0, keyVerifyValue.Length) != keyVerifyValue.Length)
				throw new FormatException("file seems to be corrupt");
			if (!Util.ArraysAreEqual(keyVerifyValue, doubleHashedKey))
			{
				throw new PasswordException("Wrong Password");
			}

			// read rest of file (encrypted data)
			int data = fin.ReadByte();
			while (data >= 0)
			{
				membuf.WriteByte((byte)data);
				data = fin.ReadByte();
			}
			cryptedData = membuf.ToArray();

			// build data that should be in hash
			membuf = new MemoryStream();
			// filename (without path) as byte[]
			byte[] fileNameBytes = Util.GetBytes(Path.GetFileName(_filename));

			byte[] realData = null;
			{
				byte[] decryptedData = AESUtil.Decrypt(singleHashedKey, cryptedData);
				// get first 32 bytes of decrypted data, this is the control-hash (dataHashValue)
				System.Array.Copy(decryptedData, dataHashValue, dataHashValue.Length);

				byte[] otherData = new byte[decryptedData.Length - dataHashValue.Length];
				System.Array.Copy(decryptedData, dataHashValue.Length, otherData, 0, otherData.Length);
				realData = Util.getDataFromPaddedWithLengthInfo(otherData);

				// write things that are used for the verification-hash into the membuf buffer
				membuf.Write(datetime, 0, datetime.Length);
				membuf.Write(fileNameBytes, 0, fileNameBytes.Length);
				membuf.Write(otherData, 0, otherData.Length);
			}

			byte[] dataToHash = membuf.ToArray();
			byte[] dataHashToCompare = AESUtil.CalculateHash(dataToHash);
			if (!Util.ArraysAreEqual(dataHashValue, dataHashToCompare))
			{
				Console.WriteLine("Hashes don't match!!!! Data may have been manipulated!");
				return null;
			}

			long fileDate = System.BitConverter.ToInt64(datetime, 0);
			DateTime dateTimeObj = Util.ConvertFromUnixTimestamp(fileDate);
			dateTimeObj = dateTimeObj.ToLocalTime(); // because it's stored in utc
			Console.WriteLine("data seems ok, file is from " + dateTimeObj.ToShortDateString() + " " + dateTimeObj.ToShortTimeString());
			Console.WriteLine("note data:");
			Console.WriteLine(Util.FromBytes(realData));
			Console.WriteLine("-=END OF NOTE=-");

			_wasOk = true;
			return realData;

		}


		public byte[] DecryptFile(String _filename, byte[] _pw, out bool _wasOk)
		{
			Stream s = new FileStream(_filename, FileMode.Open, FileAccess.Read);
			try
			{
				byte[] result = DecryptFromStream(_filename, s, _pw, out _wasOk);
				return result;
			}
			finally
			{
				s.Close();
			}
		}

	}
}
