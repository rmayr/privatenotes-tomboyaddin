#define RANDOM_PADDING
// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Gtk;
using Mono.Unix;
using Pango;
using com.google.zxing;
using com.google.zxing.common;
using com.google.zxing.qrcode;

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
		/// tries to remove a directory, fails siltently
		/// </summary>
		/// <param name="_path"></param>
		public static void TryDeleteDirectory(String _path)
		{
			try
			{
				Directory.Delete(_path, true);
			}
			catch (Exception)
			{
				// noone cares
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
		/// converts a byte array to a hex string
		/// </summary>
		/// <param name="ba"></param>
		/// <returns></returns>
		public static string ByteArrayToHexString(byte[] ba)
		{
			StringBuilder hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
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

		public static String GetFingerprintFromGpgId(String id)
		{
			string[] elements = id.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
			return (elements.Length > 1) ? elements[1].Trim() : id.Trim();
		}

		public static String GetUserIdFromGpgId(String id)
		{
			String fingerPrint = GetFingerprintFromGpgId(id);
			fingerPrint = fingerPrint.Replace(" ", "").Trim();
			return fingerPrint.Substring(fingerPrint.Length - 8);
		}

		public static String GetCleanText(String text)
		{
			return Regex.Replace(text, "[^a-zA-Z0-9 _\\.@]", "");
		}

		/// <summary>
		/// if string is longer it will be cropped and "..." will be added
		/// if string is shorter, spaces are appended
		/// </summary>
		/// <param name="text"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public static String ToLength(String text, int length)
		{
			if (text.Length < length)
			{
				return text + new string(' ', length - text.Length);
			}
			else if (text.Length == length)
			{
				return text;
			}
			else
			{
				// text is longer
				return text.Substring(0, length - 3) + new string('.', 3);
			}
		}

		/// <summary>
		/// separates the user and server part from an email address
		/// </summary>
		/// <param name="mail"></param>
		/// <param name="user"></param>
		/// <param name="server"></param>
		/// <returns></returns>
		public static bool SeparateMail(String mail, out String user, out String server)
		{
			bool success = false;
			int idx = mail.LastIndexOf("@");
			user = "";
			server = "";
			if (idx > 0 && idx < mail.Length - 1)
			{
				user = mail.Substring(0, idx);
				server = mail.Substring(idx + 1, mail.Length - idx - 1);
				success = true;
			}
			return success;
		}

        /// <summary>
        /// convenience method that allows updating of dictionaries no matter if
        /// the item (by key) already exists or not.
        /// </summary>
        /// <typeparam name="KT"></typeparam>
        /// <typeparam name="VT"></typeparam>
        /// <param name="dict">dictionary to update</param>
        /// <param name="key">key to put it under</param>
        /// <param name="value">value to insert (or update if key already exists)</param>
        public static void PutInDict<KT,VT>(Dictionary<KT,VT> dict, KT key, VT value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
            }
            else
            {
                dict.Add(key, value);
            }
        }

		public static Gdk.Image CreateQrCode(String link)
		{
			QRCodeWriter writer = new QRCodeWriter();
			ByteMatrix matrix;

			int size = 400;
			try
			{
				matrix = writer.encode(link, BarcodeFormat.QR_CODE, size, size);
			}
			catch (Exception e)
			{
				Logger.Warn("could not create qr " + e.Message);
				return new Gdk.Image(Gdk.ImageType.Normal, Gdk.Visual.Best, size, size);
			}
			Gdk.Image bmp = new Gdk.Image(Gdk.ImageType.Normal, Gdk.Visual.Best, size, size);

			for (int y = 0; y < matrix.Height; ++y)
			{
				for (int x = 0; x < matrix.Width; ++x)
				{
					uint pixelColor = bmp.GetPixel(x, y);

					//Find the colour of the dot
					if (matrix.get_Renamed(x, y) == -1)
					{
						bmp.PutPixel(x, y, 0xFFFFFF);
					}
					else
					{
						bmp.PutPixel(x, y, 0x000000);
					}
				}
			}

			return bmp;
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
		/// <summary>
		/// creates a dummy-parent for the hint window
		/// WARNING: this places the window anywhere on the screen, might be confusing for the user
		/// </summary>
		/// <param name="caption"></param>
		/// <param name="text"></param>
		public static void ShowHintWindow(String caption, String text)
		{
			// dummy parent
			Gtk.Widget wid = new Gtk.Label();
			ShowHintWindow(wid, caption, text);
		}

		/// <summary>
		/// shows a hint window (dialog) with a caption and a text
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="caption"></param>
		/// <param name="text"></param>
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

		public static void ShowFirstLaunchWindow()
		{
			Gtk.Dialog dialog = new Gtk.Dialog();
			dialog.SetPosition(WindowPosition.CenterAlways);
			dialog.Title = Catalog.GetString("First run");
			dialog.BorderWidth = 6;
			Gtk.Label label = newMarkupLabel("<span size=\"x-large\">Welcome to PrivateNotes</span>");
			dialog.VBox.PackStart(label, true, true, 0);

			label = newMarkupLabel("It seems this is the first launch, care for a short introduction?");
			dialog.VBox.PackStart(label, true, true, 12);

			Gtk.Button btn = (Gtk.Button)dialog.AddButton(Gtk.Stock.Yes, Gtk.ResponseType.Accept);
			btn.Clicked += delegate(object sender, EventArgs ea)
				{
					System.Diagnostics.Process.Start("http://tiny.cc/privatenotes#page2");
					dialog.Hide();
					dialog.Dispose();
				};

			btn = (Gtk.Button)dialog.AddButton(Gtk.Stock.No, Gtk.ResponseType.Reject);
			btn.Clicked += delegate(object sender, EventArgs ea) { dialog.Hide(); dialog.Dispose(); };

			EventHandler showDelegate = delegate(object s, EventArgs ea) { dialog.ShowAll(); dialog.Present(); };
			Gtk.Application.Invoke(showDelegate);
		}

		private static Gtk.Window oldQrWindow = null;
		/// <summary>
		/// shows a link encoded as a qr code with an optional explanation text above
		/// </summary>
		/// <param name="title">window title</param>
		/// <param name="text">explanation text, may be null</param>
		/// <param name="link">the data to encode in the qr code</param>
		public static void ShowQrCode(String title, String text, String link)
		{
			Gdk.Image bitmap = Util.CreateQrCode(link);
			Gtk.Window window = new Gtk.Window(Catalog.GetString(title));
			Gtk.VBox box = new VBox(false, 5);
			if (text != null)
			{
				Gtk.Label lbl = newMarkupLabel("<span size=\"large\">" + text + "</span>");
				box.BorderWidth = (uint)box.Spacing;
				box.Add(lbl);
			}
			Gtk.Image image = new Gtk.Image();
			image.SetFromImage(bitmap, null);
			box.Add(image);
			window.Add(box);

			if (oldQrWindow != null)
			{
				try
				{
					oldQrWindow.Destroy();
				}
				catch
				{/*irngore*/ }
			}

			window.ShowAll();
			oldQrWindow = window;
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

#region infoWindow
		// the infoWindow should only be used for testing, it displays a window 
		// with the last 5 messages that you sent to ShowInfo(string)
		// not very nice, don't use for msgs for the end-user

		private static Gtk.Label infoLabel;

		private static List<string> logs = new List<string>(); 

		public static void ShowInfo(String txt)
		{
			if (infoLabel == null || infoLabel.Parent == null)
			{
				var window = new Gtk.Window("info");
				infoLabel = new Gtk.Label("infoLabel");
				window.Child = infoLabel;
			}

			logs.Insert(0, txt);
			String infoTxt = "";
			for (int i = 0; i < 5 && i < logs.Count; i++)
			{
				if (i > 0)
					infoTxt += "\n";
				infoTxt += logs[i];
			}

			infoLabel.Text = infoTxt;

			EventHandler showDelegate = delegate(object s, EventArgs ea) { ((Gtk.Window)infoLabel.Parent).ShowAll(); ((Gtk.Window)infoLabel.Parent).Present(); };
			Gtk.Application.Invoke(showDelegate);
		}

#endregion

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

				// if there is one set already, let the user start from that directory
				String previous = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG) as String;
				if (previous != null)
				{
					dialog.SetFilename(previous);
				}

				// get back the user selection
				if (dialog.Run() == (int)Gtk.ResponseType.Accept)
				{
					foundPath = dialog.Filename;
				}

				dialog.Destroy();
			}

			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG, (String)foundPath);
		}

	}


	public class TaggedValue<K,V>
	{
		public K Value { get; set; }
		public V Tag { get; set; }
		public TaggedValue(K value, V tag)
		{
			Value = value;
			Tag = tag;
		}

		public override string ToString()
		{
			return Value.ToString();
		}
	}

	/// <summary>
	/// some icons from famfamfam
	/// www.famfamfam.com/lab/icons/silk
	/// </summary>
	public static class Icons
	{
		private static byte[] phoneIcon = {
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 
			0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 
			0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0xF3, 0xFF, 0x61, 0x00, 0x00, 0x00, 
			0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00, 0xAF, 0xC8, 0x37, 0x05, 0x8A, 
			0xE9, 0x00, 0x00, 0x00, 0x19, 0x74, 0x45, 0x58, 0x74, 0x53, 0x6F, 0x66, 
			0x74, 0x77, 0x61, 0x72, 0x65, 0x00, 0x41, 0x64, 0x6F, 0x62, 0x65, 0x20, 
			0x49, 0x6D, 0x61, 0x67, 0x65, 0x52, 0x65, 0x61, 0x64, 0x79, 0x71, 0xC9, 
			0x65, 0x3C, 0x00, 0x00, 0x01, 0x7A, 0x49, 0x44, 0x41, 0x54, 0x38, 0xCB, 
			0x8D, 0x93, 0xBF, 0x4B, 0xC3, 0x40, 0x14, 0xC7, 0xBF, 0x97, 0xC4, 0x34, 
			0x01, 0x25, 0x14, 0x5D, 0x1C, 0x0A, 0xF5, 0x1F, 0x70, 0x14, 0x37, 0xDD, 
			0xBA, 0x75, 0x75, 0xF5, 0x0F, 0x70, 0x72, 0x68, 0x37, 0xFF, 0x01, 0xE9, 
			0xE0, 0x52, 0xE8, 0xE0, 0x20, 0x38, 0x09, 0xED, 0xE8, 0xE8, 0xE2, 0xD6, 
			0x51, 0x9C, 0x0A, 0xA5, 0xB4, 0x4B, 0x31, 0xD0, 0xC5, 0x08, 0xA1, 0xF9, 
			0x75, 0xDE, 0xF7, 0x4A, 0x4B, 0xD0, 0x92, 0xF4, 0xE0, 0x78, 0xB9, 0xCB, 
			0x7B, 0x9F, 0xF7, 0xBD, 0x77, 0xEF, 0x84, 0x94, 0x12, 0xBB, 0x8C, 0x6E, 
			0xB7, 0x2B, 0x83, 0x20, 0x40, 0xBB, 0xDD, 0x16, 0xF9, 0x7D, 0x03, 0x3B, 
			0x8E, 0x2C, 0xCB, 0x30, 0x1C, 0x0E, 0xFF, 0xED, 0x5B, 0xF9, 0x45, 0xAF, 
			0xD7, 0xEB, 0x28, 0x45, 0x97, 0x42, 0x88, 0x63, 0xB5, 0x3C, 0x52, 0x73, 
			0x4F, 0x7D, 0xEB, 0xE0, 0x7A, 0xBD, 0x0E, 0xCB, 0xB2, 0x8A, 0x01, 0x51, 
			0x14, 0xDD, 0x36, 0x1A, 0x0D, 0x54, 0x2A, 0x95, 0x8D, 0x73, 0x9A, 0xA6, 
			0x7A, 0x12, 0x62, 0x9A, 0x66, 0x31, 0x80, 0x4E, 0x9E, 0xE7, 0xE1, 0xAC, 
			0x5F, 0x85, 0x6B, 0x65, 0x08, 0x33, 0x1E, 0x57, 0xE2, 0xC0, 0x8E, 0xF1, 
			0x74, 0xFE, 0x55, 0xAE, 0x80, 0x0E, 0xCC, 0x26, 0x4C, 0x89, 0xD6, 0x85, 
			0x80, 0x65, 0x48, 0x10, 0xD1, 0xF9, 0xB0, 0xF0, 0x1D, 0x65, 0xE5, 0x00, 
			0xC3, 0x30, 0x10, 0xC7, 0x31, 0x84, 0x0A, 0xB4, 0xA9, 0x56, 0xDD, 0x10, 
			0xAB, 0x4C, 0xA0, 0x12, 0xB2, 0x9B, 0x02, 0x1E, 0x43, 0xA8, 0xE0, 0xBB, 
			0x77, 0x15, 0xA3, 0xD2, 0x0B, 0x4A, 0x10, 0x64, 0xA5, 0xE5, 0x00, 0x16, 
			0x89, 0x80, 0x97, 0xD3, 0x4F, 0x6D, 0xD7, 0xC5, 0xA3, 0xF5, 0x4C, 0x6F, 
			0x2B, 0xC0, 0xD8, 0x06, 0xE8, 0x07, 0xCF, 0x78, 0x4D, 0xFA, 0x18, 0x8D, 
			0x46, 0x7A, 0x7D, 0xF8, 0xF0, 0x08, 0x36, 0x5C, 0xE9, 0x2D, 0xAC, 0x01, 
			0x4D, 0xE7, 0x6A, 0x95, 0xF9, 0x64, 0xA5, 0xC0, 0xBF, 0xB9, 0x46, 0x15, 
			0x28, 0x57, 0xB0, 0xAE, 0xC1, 0x78, 0x3C, 0xD6, 0x76, 0x32, 0x99, 0xE8, 
			0xCC, 0xB3, 0xD9, 0x6C, 0x93, 0xA0, 0xF4, 0x08, 0x0C, 0x60, 0xD7, 0x71, 
			0xD0, 0x12, 0x54, 0xAB, 0xD5, 0x36, 0x09, 0x0A, 0x01, 0xBC, 0x46, 0x06, 
			0xCC, 0xE7, 0x73, 0xD8, 0xB6, 0x0D, 0xDF, 0xF7, 0xE1, 0x38, 0x0E, 0x16, 
			0x8B, 0x85, 0x06, 0xF3, 0x7F, 0x61, 0x0D, 0x54, 0xDF, 0xFF, 0x84, 0x61, 
			0xB8, 0x9F, 0x57, 0xC0, 0xB7, 0x40, 0xCB, 0xFE, 0x50, 0xAF, 0x51, 0x16, 
			0x02, 0xA6, 0xD3, 0xE9, 0xFD, 0x60, 0x30, 0x68, 0x2E, 0x97, 0x4B, 0x1D, 
			0x98, 0x03, 0x23, 0x49, 0x12, 0xB8, 0xAE, 0xFB, 0xF6, 0x17, 0xF0, 0x0B, 
			0x42, 0x68, 0xB9, 0xC1, 0xBA, 0x43, 0x6B, 0x54, 0x00, 0x00, 0x00, 0x00, 
			0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
		};

		private static byte[] copyIcon = {
		0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 
		0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 
		0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0xFA, 0x37, 0xEA, 0x00, 0x00, 0x00, 
		0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00, 0xAF, 0xC8, 0x37, 0x05, 0x8A, 
		0xE9, 0x00, 0x00, 0x00, 0x19, 0x74, 0x45, 0x58, 0x74, 0x53, 0x6F, 0x66, 
		0x74, 0x77, 0x61, 0x72, 0x65, 0x00, 0x41, 0x64, 0x6F, 0x62, 0x65, 0x20, 
		0x49, 0x6D, 0x61, 0x67, 0x65, 0x52, 0x65, 0x61, 0x64, 0x79, 0x71, 0xC9, 
		0x65, 0x3C, 0x00, 0x00, 0x00, 0xC7, 0x49, 0x44, 0x41, 0x54, 0x28, 0xCF, 
		0x75, 0x91, 0x4D, 0x6E, 0xC2, 0x30, 0x14, 0x06, 0xBF, 0xE4, 0x1E, 0x45, 
		0xB9, 0x03, 0xC2, 0x39, 0x0F, 0x47, 0x42, 0xEA, 0x82, 0xAB, 0x74, 0x87, 
		0x50, 0x0B, 0x6B, 0xAE, 0x01, 0x42, 0x48, 0xA4, 0x24, 0xF1, 0x8F, 0x34, 
		0x5D, 0x38, 0xD8, 0x26, 0xA8, 0x9A, 0x8D, 0x17, 0xE3, 0xE7, 0x91, 0x9F, 
		0xB4, 0xD0, 0x4A, 0x26, 0xD1, 0xA8, 0x46, 0x25, 0xD2, 0xCA, 0xD9, 0x40, 
		0x64, 0xE4, 0x73, 0x33, 0x57, 0x24, 0x13, 0x18, 0xE8, 0xB8, 0x71, 0x26, 
		0xD0, 0xB3, 0xDD, 0xA8, 0x79, 0x13, 0x46, 0x7A, 0xEE, 0x5C, 0x08, 0x04, 
		0x3C, 0x6A, 0x55, 0xAB, 0x52, 0x55, 0x08, 0x9E, 0x91, 0x07, 0x77, 0x4E, 
		0x7C, 0x71, 0x44, 0x6B, 0xB5, 0xB9, 0x47, 0x32, 0xF1, 0xDE, 0x48, 0xCF, 
		0x0E, 0xC7, 0xBC, 0x67, 0x12, 0xA2, 0xB2, 0xE7, 0xBD, 0x27, 0x09, 0x01, 
		0xCB, 0x37, 0xAF, 0x3D, 0x01, 0x99, 0x42, 0x70, 0xFC, 0x90, 0x7B, 0xAE, 
		0x9C, 0x4B, 0xC1, 0xE3, 0x71, 0x1C, 0xC8, 0x3D, 0x1D, 0xB7, 0xF9, 0x04, 
		0xCF, 0x31, 0x9D, 0x2C, 0x3D, 0xBF, 0x93, 0xD0, 0xA4, 0x6F, 0x6E, 0xB5, 
		0xCE, 0xCF, 0x59, 0x86, 0x28, 0x20, 0x55, 0x13, 0xB5, 0xDA, 0x3C, 0xCD, 
		0xE1, 0x9E, 0x42, 0xFA, 0xD4, 0x2A, 0xF7, 0x44, 0xFE, 0x11, 0x32, 0xB6, 
		0xD3, 0xF2, 0x75, 0xB5, 0x4D, 0xB1, 0x78, 0x23, 0xA3, 0xA5, 0x3E, 0xFE, 
		0x00, 0xF7, 0xDB, 0x4D, 0xA0, 0xE7, 0x4D, 0xBF, 0xE6, 0x00, 0x00, 0x00, 
		0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
		};

		// yes, the below getters create a new image instance every time, but this is necessary
		// because Gtk.Image seems to do some memory management that leads to app crashes otherwise

		public static Gtk.Image PhoneIcon
		{
			get { return new Gtk.Image(new MemoryStream(phoneIcon)); }
		}

		public static Gtk.Image CopyIcon
		{
			get { return new Gtk.Image(new MemoryStream(copyIcon)); }
		}

	}
}
