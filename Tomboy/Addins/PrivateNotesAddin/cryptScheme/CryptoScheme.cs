﻿// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
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
	class NullCryptoFormat : ShareCryptoFormat
	{
		/// <summary>
		/// for nullcryptoformat it doesn't matter, since we don't use the password anyway
		/// </summary>
		/// <returns></returns>
		public bool PreHashedPasswordSupported()
		{
			return true;
		}

		public bool WriteCompatibleFile(AddressBook _adressProvider, String _filename, byte[] _content, List<String> _recipients)
		{
			_recipients = new List<string>();
			return WriteCompatibleFile(_filename, _content, new byte[0], new byte[0]);
		}


		public byte[] DecryptFile(AddressBook _adressProvider, String _filename, out List<String> _recipients, out bool _wasOk)
		{
			_recipients = new List<string>();
			return DecryptFile(_filename, new byte[0], out _wasOk);
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
			using (FileStream fin = File.OpenRead(_filename)) {
				return DecryptFromStream(_filename, fin, _key, out _wasOk);
			}
		}

		public int Version()
		{
			return 9;
		}
	}

	class GpgInvocationException : Exception
	{
		public int ExitCode { get; set; }
		public String ErrorData { get; set; }

		public GpgInvocationException(int code, String errData, String msg)
			: base(msg)
		{
			ExitCode = code;
			ErrorData = errData;
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
				tempDir = Path.Combine(Services.NativeApplication.CacheDirectory, "gp/g_" + System.Guid.NewGuid().ToString());
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

			
			//after gpg is started, we have to send the pw:
			Action<StreamWriter> writePw = new Action<StreamWriter>(delegate(StreamWriter sw)
				{
					sw.Write(Util.FromBytes(_key));
					sw.Flush();
					sw.Close();
				});
			// --yes is for confirming overwriting a possibly already existant file
			//InvokeGpg("--batch --yes --symmetric --personal-cipher-preferences AES --passphrase " + Util.FromBytes(_key) + " --output \"" + _filename + "\" \"" + tempFileName + '"');
			// passphrase-fd 0 means file descriptor 0 which is stdin
			InvokeGpg("--batch --yes --symmetric --personal-cipher-preferences AES --passphrase-fd 0 --output \"" + _filename + "\" \"" + tempFileName + '"', writePw);

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

			//FIXME: this would normally not work, because the filename is different now!!!
			byte[] decrypted = DecryptFile(tempfile, _key, out _wasOk);
			
			File.Delete(tempfile);

			return decrypted;
		}

		public byte[] DecryptFile(String _filename, byte[] _key, out bool _wasOk)
		{
			String tempfile = Path.Combine(tempDir, _filename + ".dec");
			File.Delete(tempfile);

			//after gpg is started, we have to send the pw:
			Action<StreamWriter> writePw = new Action<StreamWriter>(delegate(StreamWriter sw)
				{
					sw.Write(Util.FromBytes(_key));
					sw.Flush();
					sw.Close();
				});
			//InvokeGpg("--batch -d --passphrase " + Util.FromBytes(_key) + " --output \"" + tempfile + "\" \"" + _filename + "\"");
			// passphrase-fd 0 means file descriptor 0 which is stdin
			InvokeGpg("--batch -d --passphrase-fd " + "0" + " --output \"" + tempfile + "\" \"" + _filename + "\"", writePw);

			byte[] buffer = readFile(tempfile, out _wasOk);

			if (buffer == null)
			{
				throw new Exception("not the whole file was read!");
			}

			_wasOk = true;
			return buffer;
		}

		public bool WriteCompatibleFile(AddressBook _adressProvider, String _filename, byte[] _content, List<String> _recipients)
		{
			// TODO include the filename!
			String tempFileName = Path.Combine(tempDir, _filename + ".clear");
			FileStream fout = File.Create(tempFileName);
			fout.Write(_content, 0, _content.Length);
			fout.Close();

			// build the commandline arguments
			StringBuilder args = new StringBuilder();
			args.Append("--batch --yes -e --always-trust"); // batch encrypt, make it possible to use any keys, so there won't be a warning for the user
			args.Append(" --personal-cipher-preferences AES");
			foreach (String r in _recipients)
			{
				String userid = r;
				if (userid.Contains("/"))
				{
					// use only the id at the end, example:
					// "Somebody <test@asdf.com>  - 2048R/B1645EE8"
					// transformed to "B1645EE8" otherwise gpg doesn't recognize it
					userid = userid.Substring(userid.LastIndexOf('/') + 1);
					if (userid.Contains(" "))
						userid = userid.Substring(0, userid.IndexOf(' '));
				}

				args.Append(" -r \"");
				args.Append(userid);
				args.Append('"');
			}
			args.Append(" --output \"");
			args.Append(_filename);
			args.Append("\" \"");
			args.Append(tempFileName);
			args.Append('"');

			InvokeGpg(args.ToString());

			File.Delete(tempFileName);

			return true;
		}

		public byte[] DecryptFile(AddressBook _adressProvider, String _filename, out List<String> _recipients, out bool _wasOk)
		{
			// TODO use temp file
			String tempFileName = _filename + ".decrypted";

			StringBuilder args = new StringBuilder("-d ");
			args.Append("--output \"");
			args.Append(tempFileName);
			args.Append("\" \"");
			args.Append(_filename);
			args.Append('"');

			InvokeGpg(args.ToString());
			
			_recipients = new List<String>();

			byte[] buffer = readFile(tempFileName, out _wasOk);

			// TODO: parse recipients!
			_recipients.Add("not implemented yet");

			_wasOk = true;
			return buffer;

		}

		public String SignData(String data)
		{
			String unique = Guid.NewGuid().ToString();
			String tempFileName = Path.Combine(tempDir, unique + ".clear");
			FileStream fout = File.Create(tempFileName);
			byte[] content = Util.GetBytes(data);
			fout.Write(content, 0, content.Length);
			fout.Close();

			String outputFile = tempFileName + ".sig";

			StringBuilder args = new StringBuilder("--batch --clearsign ");
			args.Append("--output \"");
			args.Append(outputFile);
			args.Append("\" \"");
			args.Append(tempFileName);
			args.Append("\"");

			InvokeGpg(args.ToString());

			bool ok = false;
			byte[] encoded = readFile(outputFile, out ok);

			File.Delete(tempFileName);
			File.Delete(outputFile);

			String result = Util.FromBytes(encoded);

			return result;
		}

		public bool VerifySigned(String data, String expectedData, String signer)
		{
			String unique = Guid.NewGuid().ToString();
			String tempFileName = Path.Combine(tempDir, unique + ".signed");
			FileStream fout = File.Create(tempFileName);
			byte[] content = Util.GetBytes(data);
			fout.Write(content, 0, content.Length);
			fout.Close();

			String outputFile = tempFileName + ".clear";

			StringBuilder args = new StringBuilder("--batch --decrypt ");
			args.Append("--output \"");
			args.Append(outputFile);
			args.Append("\" \"");
			args.Append(tempFileName);
			args.Append("\"");

			try
			{
				String gpgOutput = InvokeGpg(args.ToString());

				if (!gpgOutput.Contains(signer))
				{
					Logger.Warn("Wrong signature, not signed by " + signer);
					File.Delete(tempFileName);
					File.Delete(outputFile);
					return false;
				}
			}
			catch (GpgInvocationException _e)
			{
				if (_e.ExitCode == 1)
				{
					Logger.Warn("Wrong signature, changed or not signed by " + signer);
				}
				File.Delete(tempFileName);
				File.Delete(outputFile);
				return false;
			}

			bool ok = false;
			byte[] encoded = readFile(outputFile, out ok);
			File.Delete(tempFileName);
			File.Delete(outputFile);

			String result = Util.FromBytes(encoded);

			// trimming needed because signing can add a new line at the end
			return result.Trim().Equals(expectedData.Trim());
		}

		private static byte[] readFile(String _fileName, out bool _wasOk)
		{
			BufferedStream fin = new BufferedStream(File.OpenRead(_fileName));
			long len = fin.Length;
			if (len > Int32.MaxValue)
			{
				File.Delete(_fileName);
				throw new Exception("huge files not supported!!!");
			}
			byte[] buffer = new byte[len];
			int read = fin.Read(buffer, 0, (int)len);
			fin.Close();

			File.Delete(_fileName);

			if (read != len)
			{
				_wasOk = false;
				return null;
			}

			_wasOk = true;
			return buffer;
		}

		private static String InvokeGpg(String _arguments)
		{
			return InvokeGpg(_arguments, null);
		}

		/// <summary>
		/// Invokes GPG and allows to execute some action as soon
		/// as it is started (can be used for example for writing
		/// arguments to its input via a file descriptor)
		/// </summary>
		/// <param name="_arguments"></param>
		/// <param name="afterStart">action to invoke after gpg is started, may be null</param>
		/// <returns></returns>
		private static String InvokeGpg(String _arguments, Action<StreamWriter> afterStart) {
			Statistics.Instance.StartCrypto();
			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = gpgExe;
			proc.StartInfo.Arguments = _arguments;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.RedirectStandardError = true;
			if (afterStart != null)
			{
				proc.StartInfo.RedirectStandardInput = true;
			}
			proc.Start();
			if (afterStart != null)
			{
				afterStart(proc.StandardInput);
			}
			String data = proc.StandardOutput.ReadToEnd();
			String errdata = proc.StandardError.ReadToEnd();
			proc.WaitForExit();
			Statistics.Instance.EndCrypto();
			if (proc.ExitCode != 0)
			{
				Logger.Info(data);
				if (!String.IsNullOrEmpty(errdata))
				{
					Logger.Info("ERRORS:");
					Logger.Info(errdata);
				}
				throw new GpgInvocationException(proc.ExitCode, errdata, "openPgp invocation exception: " + errdata);
			}
			return errdata;
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
