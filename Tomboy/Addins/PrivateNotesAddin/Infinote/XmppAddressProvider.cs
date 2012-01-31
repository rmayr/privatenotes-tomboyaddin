using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tomboy;
using Tomboy.PrivateNotes;
using Tomboy.PrivateNotes.Adress;

namespace PrivateNotes.Infinote
{

	/// <summary>
	/// stores the xmpp-ids for our AddressBookEntries (from the AddressBook class)
	/// </summary>
	public class XmppAddressProvider
	{
		const string SEPCHAR = ";";
		private const string COMMENTCHAR = "#";
		public String AddressFile { get; private set; }

		private Dictionary<String, XmppEntry> contacts = new Dictionary<string, XmppEntry>();  

		private AddressBook addressBook;

		/// <summary>
		/// load entries from the file
		/// </summary>
		public void Load()
		{
			addressBook = AddressBookFactory.Instance().GetDefault();
			AddressFile = Path.Combine(Services.NativeApplication.ConfigurationDirectory, "xmppContacts.txt");
			ParseAddressFile(AddressFile);
		}

		/// <summary>
		/// update the entries (saved addresses by the user won't be removed!)
		/// new contacts will be added + all entries are sorted so that entries
		/// where the user added an address, will appear at the top
		/// </summary>
		public void UpdateAddressBookFile()
		{
			Load();
			var all = addressBook.GetEntries();
			List<String> toAdd = new List<string>();
			List<String> contained = new List<string>();
			foreach (var entry in all)
			{
				String fingerPrint = Util.GetFingerprintFromGpgId(entry.id);
				var storedEntry = GetEntryForExactGpgId(entry.id);
				// make all names the same length (and no goofy chars)
				String cleanName = Util.ToLength(Util.GetCleanText(entry.name), 45);
				if (storedEntry == null)
				{
					toAdd.Add(cleanName + SEPCHAR + fingerPrint + SEPCHAR);
				}
				else
				{
					contained.Add(cleanName + SEPCHAR + fingerPrint + SEPCHAR + storedEntry.XmppId);
				}
			}

			string[] toStore = new string[toAdd.Count + contained.Count + 1];
			toStore[0] = COMMENTCHAR + "Some name"+SEPCHAR+"GpgFingerprint"+SEPCHAR+" Put the Xmpp-id here, example: theodore@jabber.org";
			for (int i=0; i<contained.Count; i++)
			{
				toStore[i + 1] = contained[i];
			}
			for (int i=0; i<toAdd.Count; i++)
			{
				toStore[i + 1 + contained.Count] = toAdd[i];
			}

			// save
			try {
				File.WriteAllLines(AddressFile, toStore);	
			}catch (Exception e)
			{
				Logger.Warn("cannot save xmpp contacts file", e);
			}
		}

		/// <summary>
		/// get an addressbook-entry by an xmpp-name
		/// </summary>
		/// <param name="xmppId"></param>
		/// <returns></returns>
		public XmppEntry GetEntryForXmppId(String xmppId)
		{
			XmppEntry result;
			contacts.TryGetValue(xmppId, out result);
			return result;
		}

		/// <summary>
		/// get an entry by a gpg-fingerprint (the string will be filtered)
		/// </summary>
		/// <param name="gpgId"></param>
		/// <returns></returns>
		public XmppEntry GetEntryForGpgFingerprint(String gpgId)
		{
			foreach (var entry in contacts)
			{
				if (entry.Value.Person != null) {
					String fingerPrint = Util.GetFingerprintFromGpgId(entry.Value.Person.id);
					if (fingerPrint == gpgId)
					{
						return entry.Value;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// get an entry for a gpg-id (key-fingerprint, will not be filtered)
		/// </summary>
		/// <param name="gpgId"></param>
		/// <returns></returns>
		private XmppEntry GetEntryForExactGpgId(String gpgId)
		{
			foreach (var entry in contacts)
			{
				if (entry.Value.Person != null && entry.Value.Person.id == gpgId)
				{
					return entry.Value;
				}
			}
			return null;
		}

		/// <summary>
		/// retrieves all entries
		/// </summary>
		/// <returns></returns>
		public List<XmppEntry> GetAll()
		{
			return new List<XmppEntry>(contacts.Values);
		} 

		/// <summary>
		/// gets the share-partners for a note
		/// </summary>
		/// <param name="noteId"></param>
		/// <returns></returns>
		public List<XmppEntry> GetAppropriateForNote(String noteId)
		{
			List<XmppEntry> result = new List<XmppEntry>();
			NoteShare share = SecureSharingFactory.Get().GetShareProvider().GetNoteShare(noteId);
			if (share != null)
			{
				foreach (String partner in share.sharedWith)
				{
					String fingerPrint = Util.GetFingerprintFromGpgId(NoteShare.GetIdOnlyFromVariousFormats(partner));
					XmppEntry equivalent = GetEntryForGpgFingerprint(fingerPrint);
					if (equivalent != null)
					{
						result.Add(equivalent);
					}
				}
			}
			return result;
		} 

#region privates

		private void ParseAddressFile(String file)
		{
			try
			{
				string[] lines = File.ReadAllLines(file);

				AddressBookHelper helper = AddressBookHelper.GetInstance(addressBook);

				foreach (var line in lines)
				{
					String gpgId;
					String xmppId = ReadLine(line, out gpgId);
					if (gpgId != null && xmppId != null)
					{
						if (!contacts.ContainsKey(xmppId))
						{
							var item = helper.GetByFingerprint(gpgId);
							if (item != null)
							{
								contacts.Add(xmppId, new XmppEntry(){Person = item, XmppId = xmppId});
							}
							else
							{
								Logger.Warn("We don't know any person by gpgid {0}", gpgId);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Logger.Warn("could not read xmpp contacts", e);
			}

		}

		/// <summary>
		/// parse a line, the gpg id will be returned by out parameter gpgId, the added xmpp-id will be returned as return value
		/// if the user hasn't added any address, null will be returned
		/// </summary>
		/// <param name="line"></param>
		/// <param name="gpgId"></param>
		/// <returns></returns>
		private String ReadLine(String line, out String gpgId)
		{
			gpgId = null;

			if (line.StartsWith(COMMENTCHAR))
				return null;

			int idx1 = line.IndexOf(SEPCHAR);
			if (idx1 == -1 || idx1 >= line.Length - 1)
				return null;
			int idx2 = line.IndexOf(SEPCHAR, idx1 + 1);
			if (idx2 == -1 || idx2 >= line.Length - 1)
				return null;

			String parsedGpgId = line.Substring(idx1 + 1, idx2 - idx1 - 1);
			String xmppId = line.Substring(idx2 + 1).Trim();
			if (String.IsNullOrEmpty(parsedGpgId) || String.IsNullOrEmpty(xmppId))
			{
				return null;
			}
			gpgId = parsedGpgId;
			return xmppId;
		}
#endregion
	}

	public class XmppEntry
	{
		public String XmppId { get; set; }
		public AddressBookEntry Person { get; set; }

		public override string ToString()
		{
			return XmppId ?? "no-xmpp-id";
		}
	}


}
