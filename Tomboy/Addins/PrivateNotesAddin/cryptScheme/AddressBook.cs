using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Tomboy.PrivateNotes.Adress
{
	public class AdressBookEntry
	{
		public AdressBookEntry(String id, String name, String mail)
		{
			this.id = id;
			this.name = name;
			this.mail = mail;
		}

		public String name;
		public String mail;
		public String id;
	}

	public interface AdressBook
	{
		bool Load();

		List<AdressBookEntry> getEntries();
	}

	public class AdressBookFactory
	{
		private static AdressBookFactory INSTANCE = new AdressBookFactory();

		private AdressBook defaultAdressBook = new PgpAdressBook();

		public static AdressBookFactory Instance()
		{
			return INSTANCE;
		}

		public AdressBook GetDefault()
		{
			return defaultAdressBook;
		}
	}

	public class PgpAdressBook : AdressBook 
	{
		List<AdressBookEntry> entries = null;
		String gpgExe = null;

		static String GPG_LIST = "--list-keys";

		public PgpAdressBook()
		{
			gpgExe = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG) as String;
			if (gpgExe == null)
			{
				throw new InvalidOperationException("gpg not configured yet!");
			}
		}

		public bool Load()
		{
			entries = new List<AdressBookEntry>();



			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = gpgExe;
			proc.StartInfo.Arguments = GPG_LIST;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.Start();
			//StringBuilder sb = new StringBuilder(1000);
			String data = proc.StandardOutput.ReadToEnd();
			
			// parse output!
			String[] lines = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			entries = ParseGpgOutput(lines);
			
			
			proc.WaitForExit(50);


			// TODO parse from pgp output!
			return true;
		}

		public List<AdressBookEntry> getEntries()
		{
			if (entries == null)
				throw new InvalidOperationException("not initialized yet!");

			return entries;
		}

		/// <summary>
		/// quick & dirty parsing of gpg output
		/// </summary>
		/// <param name="lines"></param>
		/// <returns></returns>
		private List<AdressBookEntry> ParseGpgOutput(String[] lines)
		{
			List<AdressBookEntry> results = new List<AdressBookEntry>();
			String currentId = null;
			char[] spaceSplitter = new char[]{' ', '\t'};
			foreach (String l in lines)
			{
				if (l.StartsWith("pub"))
				{
					string[] parts = l.Split(spaceSplitter, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length < 3)
					{
						throw new InvalidDataException("cannot process pgp output!");
					}
					currentId = parts[1];
				}
				else if (l.StartsWith("uid"))
				{
					String name = l.Substring(3);
					name = name.Trim();
					results.Add(new AdressBookEntry(currentId, name, ""));
				}
			}

			return results;
		}
	}

}