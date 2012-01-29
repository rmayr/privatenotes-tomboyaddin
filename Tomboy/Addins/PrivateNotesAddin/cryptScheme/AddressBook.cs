using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Tomboy.PrivateNotes.Adress
{
	public class AddressBookEntry
	{
		public AddressBookEntry(String id, String name, String mail)
		{
			this.id = id;
			this.name = name;
			this.mail = mail;
		}

		public String name;
		public String mail;
		public String id;
	}

	public interface AddressBook
	{
		bool Load();

		List<AddressBookEntry> GetEntries();

		AddressBookEntry GetOwnAddress();
	}

	public class AddressBookHelper
	{
		private static Dictionary<AddressBook, AddressBookHelper> instances = new Dictionary<AddressBook, AddressBookHelper>();

		private static Dictionary<String, AddressBookEntry> entries = new Dictionary<String, AddressBookEntry>(); 

		private AddressBookHelper(AddressBook book)
		{
			var content = book.GetEntries();
			foreach (var item in content)
			{
				String fingerprint = Util.GetFingerprintFromGpgId(item.id);
				if (!entries.ContainsKey(fingerprint))
				{
					entries.Add(fingerprint, item);
				}
			}
		}

		public static AddressBookHelper GetInstance(AddressBook forBook)
		{
			if (instances.ContainsKey(forBook))
				return instances[forBook];

			var instance = new AddressBookHelper(forBook);
            instances.Add(forBook, instance);
			return instance;
		}

		public AddressBookEntry GetByFingerprint(String fingerPrint)
		{
			AddressBookEntry result;
			entries.TryGetValue(fingerPrint, out result);
			return result;
		}

	}

	public class AddressBookFactory
	{
		private static AddressBookFactory INSTANCE = new AddressBookFactory();

		private AddressBook defaultAdressBook = null;

		public static AddressBookFactory Instance()
		{
			return INSTANCE;
		}

		public AddressBook GetDefault()
		{
			if (defaultAdressBook == null)
			{
				defaultAdressBook = new PgpAddressBook();
				defaultAdressBook.Load();
			}
			return defaultAdressBook;
		}
	}

	public class PgpAddressBook : AddressBook 
	{
		List<AddressBookEntry> entries = null;
		AddressBookEntry own = null;
		String gpgExe = null;

		//static String GPG_LIST = "--list-keys";
		static String GPG_LIST = "--fingerprint";
		static String GPG_LIST_OWN = "--list-secret-keys --fingerprint";

		public PgpAddressBook()
		{
			gpgExe = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SHARE_GPG) as String;
			if (gpgExe == null)
			{
				throw new InvalidOperationException("gpg not configured yet!");
			}
		}

		public bool Load()
		{
			entries = new List<AddressBookEntry>();
			{
				System.Diagnostics.Process proc = new System.Diagnostics.Process();
				proc.StartInfo.FileName = gpgExe;
				proc.StartInfo.Arguments = GPG_LIST;
				proc.StartInfo.UseShellExecute = false;
				proc.StartInfo.CreateNoWindow = true;
				proc.StartInfo.RedirectStandardOutput = true;
				proc.Start();
				String data = proc.StandardOutput.ReadToEnd();

				// parse output!
				String[] lines = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

				entries = ParseGpgOutput(lines);

				proc.WaitForExit(50);
			}

			// now get our own address(es)
			{
			System.Diagnostics.Process proc = new System.Diagnostics.Process();
			proc.StartInfo.FileName = gpgExe;
			proc.StartInfo.Arguments = GPG_LIST_OWN;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.Start();
			String data = proc.StandardOutput.ReadToEnd();

			// parse output!
			String[] lines = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			List<AddressBookEntry> myKeys = ParseGpgSecretKeysOutput(lines);
			if (myKeys.Count > 1)
			{
				// TODO alert the user, that he can choose which private key to use!
			}
			own = myKeys[0];

			proc.WaitForExit(50);
			}

			// TODO parse from pgp output!
			return true;
		}

		public List<AddressBookEntry> GetEntries()
		{
			if (entries == null)
				throw new InvalidOperationException("not initialized yet!");

			return entries;
		}

		public AddressBookEntry GetOwnAddress()
		{
			if (entries == null)
				throw new InvalidOperationException("not initialized yet!");

			if (own == null)
				throw new Exception("private key needed for this! You have to have some private key to be able to share notes!");

			return own;
		}

		/// <summary>
		/// quick & dirty parsing of gpg output
		/// </summary>
		/// <param name="lines"></param>
		/// <returns></returns>
		private List<AddressBookEntry> ParseGpgOutputWithoutFingerprints(String[] lines)
		{
			List<AddressBookEntry> results = new List<AddressBookEntry>();
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
					results.Add(new AddressBookEntry(currentId, name, ""));
				}
			}

			return results;
		}

		/// <summary>
		/// quick & dirty parsing of gpg output
		/// </summary>
		/// <param name="lines"></param>
		/// <returns></returns>
		private List<AddressBookEntry> ParseGpgOutput(String[] lines)
		{
			List<AddressBookEntry> results = new List<AddressBookEntry>();
			String currentId = null;
			bool expectFingerprint = false; // if true, coming line is fingerprint
			char[] spaceSplitter = new char[] { ' ', '\t' };
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
					expectFingerprint = true;
				}
				else if (expectFingerprint == true)
				{
					expectFingerprint = false;
					string[] parts = l.Split(new char[]{'='}, StringSplitOptions.None);
					if (parts.Length < 2)
					{
						throw new InvalidDataException("cannot process pgp output!");
					}
					currentId += " - " + parts[1].Trim();
				}
				else if (l.StartsWith("uid"))
				{
					expectFingerprint = false;
					String name = l.Substring(3);
					name = name.Trim();
					results.Add(new AddressBookEntry(currentId, name, ""));
				}
				else
				{
					expectFingerprint = false;
				}
			}

			return results;
		}

		/// <summary>
		/// parsing of the secret keys available (to know which one is our own key)
		/// </summary>
		/// <param name="lines"></param>
		/// <returns></returns>
		private List<AddressBookEntry> ParseGpgSecretKeysOutput(String[] lines)
		{
			List<AddressBookEntry> results = new List<AddressBookEntry>();
			String currentId = null;
			bool expectFingerprint = false; // if true, coming line is fingerprint
			char[] spaceSplitter = new char[] { ' ', '\t' };
			foreach (String l in lines)
			{
				if (l.StartsWith("sec"))
				{
					string[] parts = l.Split(spaceSplitter, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length < 3)
					{
						throw new InvalidDataException("cannot process pgp output!");
					}
					currentId = parts[1];
					expectFingerprint = true;
				}
				else if (expectFingerprint == true)
				{
					expectFingerprint = false;
					string[] parts = l.Split(new char[] { '=' }, StringSplitOptions.None);
					if (parts.Length < 2)
					{
						throw new InvalidDataException("cannot process pgp output!");
					}
					currentId += " - " + parts[1].Trim();
				}
				else if (l.StartsWith("uid"))
				{
					expectFingerprint = false;
					String name = l.Substring(3);
					name = name.Trim();
					results.Add(new AddressBookEntry(currentId, name, ""));
				}
				else
				{
					expectFingerprint = false;
				}
			}

			return results;
		}
	}

}