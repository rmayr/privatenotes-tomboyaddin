﻿#define RANDOM_PADDING

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// util class
	/// filesystem and byte-conversion related helpers
	/// </summary>
	public class Util
	{
#if !NO_RANDOM_PADDING
		private static Random random = new Random();
#endif

		/// <summary>
		/// makes sure that a file exists
		/// </summary>
		/// <param name="_path"></param>
		public static void AssureFileExists(String _path)
		{
			if (!File.Exists(_path))
				File.Create(_path).Close();
		}

		/// <summary>
		/// utility method which parses the note id from the filename
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static String GetNoteIdFromFileName(String fileName)
		{
			String noteid = null;
			if (fileName.EndsWith(".note"))
			{
				FileInfo file = new System.IO.FileInfo(fileName);
				noteid = file.Name.Replace(".note", "");
			}
			else
				Logger.Warn("filename not a note! {0}", fileName);
			return noteid;
		}

		/// <summary>
		/// deletes all files in a directory (not sub-directories!)
		/// </summary>
		/// <param name="_path"></param>
		public static void DelelteFilesInDirectory(String _path)
		{
			DirectoryInfo info = new DirectoryInfo(_path);
			foreach (FileInfo file in info.GetFiles())
			{
				file.Delete();
			}
		}

		/// <summary>
		/// convert from a unix timestamp to a c# dateTime object
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		public static DateTime ConvertFromUnixTimestamp(long timestamp)
		{
			DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
			return origin.AddSeconds(timestamp);
		}

		/// <summary>
		/// converts a c# dateTime object to a unix timestamp
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		public static long ConvertToUnixTimestamp(DateTime date)
		{
			DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
			TimeSpan diff = date - origin;
			return (long)Math.Floor(diff.TotalSeconds);
		}

		/// <summary>
		/// string to bytes (to have one central place where the codepage is defined)
		/// </summary>
		/// <param name="_s"></param>
		/// <returns></returns>
		public static byte[] GetBytes(String _s)
		{
			return Encoding.UTF8.GetBytes(_s);
		}

		/// <summary>
		/// bytes to stirng (to have one central place where the codepage is defined)
		/// </summary>
		/// <param name="_data"></param>
		/// <returns></returns>
		public static String FromBytes(byte[] _data)
		{
			return Encoding.UTF8.GetString(_data);
		}

		/// <summary>
		/// check if 2 byte arrays are equal
		/// </summary>
		/// <param name="_array1"></param>
		/// <param name="_array2"></param>
		/// <returns></returns>
		public static bool ArraysAreEqual(byte[] _array1, byte[] _array2)
		{
			if (_array1 == null || _array2 == null)
				return false;
			if (_array1 == _array2)
				return true;
			if (_array1.Length != _array2.Length)
				return false;

			for (int i = 0; i < _array1.Length; i++)
				if (_array1[i] != _array2[i])
					return false;

			return true;
		}

		/// <summary>
		/// pad some byte-data to a certain length
		/// </summary>
		/// <param name="_data"></param>
		/// <param name="_multipleOf"></param>
		/// <returns></returns>
		public static byte[] padData(byte[] _data, int _multipleOf)
		{
			int tooMuch = _data.Length % _multipleOf;
			int padBytes = _multipleOf - tooMuch;
			byte[] newData = new byte[_data.Length + padBytes];
			System.Array.Copy(_data, newData, _data.Length);
#if !NO_RANDOM_PADDING
			// fill rest with random data
			byte[] randomPad = new byte[padBytes];
			random.NextBytes(randomPad);
			System.Array.Copy(randomPad, 0, newData, _data.Length, padBytes);
#endif
			return newData;
		}

		/// <summary>
		/// adds 4 byte length info at the beginning, supports max. length of the max value of int32
		/// </summary>
		/// <param name="_data"></param>
		/// <param name="_multipleOf"></param>
		/// <returns></returns>
		public static byte[] padWithLengthInfo(byte[] _data, int _multipleOf)
		{
			int tooMuch = (_data.Length + 4) % _multipleOf;
			int padBytes = _multipleOf - tooMuch;
			byte[] newData = new byte[_data.Length + padBytes + 4];
			if (_data.LongLength > Int32.MaxValue)
			{
				throw new InvalidOperationException("you can't use this much of data, because the length information only uses 4 bytes");
			}
			// get length info
			byte[] lengthInfo = System.BitConverter.GetBytes((int)_data.Length);
			// write length info
			System.Array.Copy(lengthInfo, 0, newData, 0, lengthInfo.Length);
			// write data
			System.Array.Copy(_data, 0, newData, 4, _data.Length);
#if !NO_RANDOM_PADDING
			// fill rest with random data
			byte[] randomPad = new byte[padBytes];
			random.NextBytes(randomPad);
			System.Array.Copy(randomPad, 0, newData, lengthInfo.Length + _data.Length, padBytes);
#endif
			return newData;
		}

		/// <summary>
		/// reads the first 4 bytes of an array, converts that to an int, and reads that many following bytes of
		/// the array and returns them
		/// </summary>
		/// <param name="_data"></param>
		/// <returns></returns>
		public static byte[] getDataFromPaddedWithLengthInfo(byte[] _data)
		{
			if (_data.Length < 4)
				throw new InvalidOperationException("the data must at least contain the length info");

			int lenghtInfo = BitConverter.ToInt32(_data, 0);
			if (_data.Length < 4 + lenghtInfo)
				throw new InvalidOperationException("length info invalid, array not long enough to hold that much data");

			byte[] realData = new byte[lenghtInfo];
			System.Array.Copy(_data, 4, realData, 0, lenghtInfo);
			return realData;
		}

		/// <summary>
		/// makes a http-get-request and returns the contents
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public static string HttpGet(string url)
		{
			HttpWebRequest req = WebRequest.Create(url)
								 as HttpWebRequest;
			string result = null;
			using (HttpWebResponse resp = req.GetResponse()
										  as HttpWebResponse)
			{
				StreamReader reader =
					new StreamReader(resp.GetResponseStream());
				result = reader.ReadToEnd();
			}
			return result;
		}
		
		/// <summary>
		/// checks if the current platform we are running on is windows
		/// </summary>
		/// <returns>true if on windows</returns>
		public static bool IsWindows() {
			int p = (int) Environment.OSVersion.Platform;
			bool isUnix = (p == 4) || (p == 6) || (p == 128);
			return !isUnix;
		}
	}

#if WIN32 && DPAPI
	// DPAPI stuff, only exists on windows:

	/// <summary>
	/// Windows Data Protection API. Data is protected in a way, that it is only
	/// accessible by the currently logged in user
	/// </summary>
	public class DPAPIUtil
	{

		/// <summary>
		/// stores the password in a protected file
		/// </summary>
		/// <param name="_pw"></param>
		public static void storePassword(String _pw)
		{
			String dataFile = getDataFilePath();
			byte[] toEncrypt = Util.GetBytes(_pw);
			byte[] encrypted = System.Security.Cryptography.ProtectedData.Protect(toEncrypt, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
		 
			Util.AssureFileExists(dataFile);

			FileStream fout = File.OpenWrite(dataFile);
			fout.Write(encrypted, 0, encrypted.Length);
			fout.Close();
		}

		/// <summary>
		/// gets the password from the protected file
		/// </summary>
		/// <returns></returns>
		public static String getPassword()
		{
			byte[] todecrypt;
			try
			{
				String dataFile = getDataFilePath();
				FileStream fin = File.OpenRead(dataFile);
				{
					MemoryStream buf = new MemoryStream();
					int b = fin.ReadByte();
					while (b >= 0)
					{
						buf.WriteByte((byte)b);
						b = fin.ReadByte();
					}
					todecrypt = buf.ToArray();
				}
			}
			catch (Exception _e)
			{
				Logger.Info("Could not retrieve key from dpapi, maybe the file doesn't exist.", _e);
				return null;
			}

			byte[] decrypted = System.Security.Cryptography.ProtectedData.Unprotect(todecrypt, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);

			return Util.FromBytes(decrypted);
		}

		private static String getDataFilePath()
		{
			return Path.Combine(Services.NativeApplication.ConfigurationDirectory, "dpapi_file.dat");
		}

	}
	
#endif


	/// <summary>
	/// gtk helper
	/// some small useful helpers that make certain things easier with the gtk lib
	/// </summary>
	public class GtkUtil
	{
		public static void ShowHintWindow(Gtk.Widget parent, String caption, String text)
		{
			Gtk.Dialog dialog = new Gtk.Dialog();
			dialog.ParentWindow = parent.GdkWindow;
			dialog.Parent = parent;
			dialog.Title = caption;
			dialog.VBox.PackStart(new Gtk.Label(text), true, true, 12);
			
			Gtk.Button closeButton = (Gtk.Button)dialog.AddButton(Gtk.Stock.Ok, Gtk.ResponseType.Close);
			closeButton.Clicked += delegate(object sender, EventArgs ea) { dialog.Hide(); dialog.Dispose(); };

			EventHandler showDelegate = delegate(object s, EventArgs ea) { dialog.ShowAll(); dialog.Present(); };
			Gtk.Application.Invoke(showDelegate);
		}

		/// <summary>
		/// quick wrapper to simplify label creation when you need to set markup, not the text (because there is no such constructor)
		/// </summary>
		/// <param name="_markup"></param>
		/// <returns></returns>
		public static Gtk.Label newMarkupLabel(String _markup)
		{
			var l = new Gtk.Label();
			l.Markup = _markup;
			return l;
		}


	}
	
	/// <summary>
	/// utility that helps us register for the note:// protocol
	/// 
	/// currently this is only implemented for windows!
	/// </summary>
	public class NoteProtocolRegisterUtility {
		
		public static bool Register() {
			if (Util.IsWindows()) {
				string registerCommand =
@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\note\shell\open\command]
@=""\""__PROGRAM__PATH__\"" \""%1\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\note]
@=""Tomboy Notes""
""URL Protocol""=""""
";
				string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
				exePath = exePath.Replace("\\", "\\\\"); // escape it for reg file
				string customized = registerCommand.Replace("__PROGRAM__PATH__", exePath);
				string tempPath = Path.GetTempPath();
				try {
					String regFile = Path.Combine(tempPath, "register.reg");
					StreamWriter fout = File.CreateText(regFile);
					fout.Write(customized);
					fout.Close();
					// now start:
					System.Diagnostics.Process.Start(regFile);
					return true;
				} catch (Exception e) {
					Logger.Warn("could Note Register because ", e);
					return false;
				}
			} else {
				return false;
			}
		}
		
		
	}

	/// <summary>
	/// utility that helps us configure the GPG utility on different platforms
	/// </summary>
	public class GpgConfigUtility {

		private static bool? configured = null;
		
		/// <summary>
		/// checks if it is already configured
		/// </summary>
		/// <returns></returns>
		public static bool CheckConfigured() {
			if (configured == null) {
				String gpgExe = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG) as String;
				configured = gpgExe != null && File.Exists(gpgExe);
			}
			return configured.Value;
		}

		/// <summary>
		/// configures the gpg utility if necessary. Tries automatically, if unsuccessful
		/// the user will be asked
		/// </summary>
		/// <param name="parentWindow">winow that use as parent window if we need to ask user and display a dialog</param>
		public static void ConfigureIfNecessary(Gtk.Window parentWindow)
		{
			bool configured = CheckConfigured();
			if (!configured) {
				ConfigureGpg(true, parentWindow);
			}
		}

		/// <summary>
		/// configures the gpg utility, first it tries automatically, if unsuccessful
		/// the user will be asked
		/// </summary>
		/// <param name="parentWindow"></param>
		public static void ConfigureGpg(bool tryAutomatic, Gtk.Window parentWindow) {
			String foundPath = null;

			if (tryAutomatic) {
				List<String> defaultPaths = new List<string>();
				bool isUnix = !Util.IsWindows();

				if (isUnix) {
					// unix / osx
					defaultPaths.Add("gpg");
					defaultPaths.Add("/usr/bin/gpg");
				} else {
					// windows
					String programsDir = Environment.GetEnvironmentVariable("PROGRAMFILES");
					String programsDir2 = Environment.GetEnvironmentVariable("PROGRAMFILES(x86)");
					// possible sub-dirs+exe-name on windows
					String[] winPaths = new String[]{@"GNU\GnuPG\gpg2.exe", @"GNU\GnuPG\gpg.exe",
						@"GnuPG\gpg2.exe", @"GnuPG\gpg.exe", @"gpg\gpg.exe", @"gpg\gpg2.exe"};

					if (programsDir != null)
					{
						foreach (String path in winPaths)
							defaultPaths.Add(Path.Combine(programsDir, path));
					}
					if (programsDir2 != null)
					{
						foreach (String path in winPaths)
							defaultPaths.Add(Path.Combine(programsDir2, path));
					}
				}

				// test for default paths:
				foreach (String path in defaultPaths)
				{
					if (File.Exists(path)) {
						// TODO try execute with --version or sth
						foundPath = path;
						break;
					}
				}

			}

			if (foundPath == null) {

				// let user choose				
				Gtk.FileChooserDialog dialog = new Gtk.FileChooserDialog("Please choose GPG exe", parentWindow,
					Gtk.FileChooserAction.Open, "Cancel", Gtk.ResponseType.Cancel, "Use", Gtk.ResponseType.Accept);

				String previous = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG) as String;
				if (previous != null)
				{
					dialog.SetFilename(previous);
				}

				if (dialog.Run() == (int)Gtk.ResponseType.Accept)
				{
					foundPath = dialog.Filename;
				}

				dialog.Destroy();

			}

			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG, (String)foundPath);
		}

	}


}