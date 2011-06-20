using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using Tomboy.PrivateNotes.Adress;

namespace Tomboy.PrivateNotes
{
	/// <summary>
	/// Stores information about a shared note
	/// </summary>
	public class NoteShare {
		public String noteId;
		public List<String> sharedWith = new List<string>();
		public String shareTarget;

		public NoteShare() : this("", "", "")
		{

		}

		public NoteShare(String id, String with, String target)
		{
			noteId = id;
			sharedWith.Add(with);
			shareTarget = target;
		}

		public NoteShare(String id, List<String> with, String target)
		{
			noteId = id;
			sharedWith.AddRange(with);
			shareTarget = target;
		}

		public bool IsSharedWith(String with)
		{
			String cleanedId = NoteShare.GetIdOnlyFromVariousFormats(with);
			string match = sharedWith.Find(delegate(string element) { return cleanedId.Equals(NoteShare.GetIdOnlyFromVariousFormats(element)); });
			return match != null;
		}

		public void Serialize(XmlWriter writer)
		{
			writer.WriteStartElement(null, "noteshare", null);
			writer.WriteAttributeString("id", noteId);
			writer.WriteAttributeString("target", shareTarget);
			foreach (String with in sharedWith)
			{
				writer.WriteStartElement(null, "with", null);
				writer.WriteAttributeString("partner", with);
				writer.WriteEndElement();
			}
			writer.WriteEndElement();
		}

		public static NoteShare Deserialize(XmlNode share)
		{
			String id = share.Attributes["id"].Value;
			String target = share.Attributes["target"].Value;
			List<String> with = new List<string>();
			foreach (XmlNode n in share.ChildNodes)
			{
				with.Add(n.Attributes["partner"].Value);
			}
			return new NoteShare(id, with, target);
		}


		/// <summary>
		/// sometimes we have data in the format:
		/// somebody &lt;somebodysemail@something.com&gt; - thisis/theid - in some hex format
		/// but we only want the last part (after the &gt; which again isn't always there)
		/// </summary>
		/// <param name="_idOrMore"></param>
		/// <returns></returns>
		public static String GetIdOnlyFromVariousFormats(String _idOrMore)
		{
			String TAG = " - ";
			int idx1 = _idOrMore.LastIndexOf(TAG);
			if (idx1 > 0)
			{
				int idx2 = _idOrMore.LastIndexOf(TAG, idx1);
				if (idx2 > 0)
				{
					// we have the un-desired format, transform it:
					return _idOrMore.Substring(idx2 + TAG.Length);
				}
			}
			else
				Logger.Warn("we probably have the wrong id format: {0}", _idOrMore);
			return _idOrMore;
		}

	}

	public delegate void ShareAdded(String noteid, String with);
	public delegate void ShareRemoved(String noteid, String with);

	/// <summary>
	/// class for managing shares (locally)
	/// knows what is shared with whom and where it is stored (server/path etc)
	/// with this shares can be added/removed
	/// </summary>
	public interface ShareProvider
	{
		/// <summary>
		/// you can register for this event, it will be triggered when a new share is added
		/// </summary>
		event ShareAdded OnShareAdded;

		/// <summary>
		/// event will be triggered when a share is removed
		/// </summary>
		event ShareRemoved OnShareRemoved;

		/// <summary>
		/// adds a new share. In other words: a note that already exists locally is added
		/// to the shares. so the next time you sync, it will be put on the shared
		/// location
		/// </summary>
		/// <param name="noteuid"></param>
		/// <param name="shareWith"></param>
		/// <returns></returns>
		bool AddShare(String noteuid, String shareWith);

		/// <summary>
		/// add a share via a path/location that sb has sent you
		/// this first has to acquire the share and will probably add it
		/// to your notes and then store all the necessary information
		/// that the note will be synced with that share from now on
		/// </summary>
		/// <param name="share"></param>
		/// <returns></returns>
		bool ImportShare(String share);

		/// <summary>
		/// removes a share. that means the next time you sync, the note
		/// will no longer be put into the shared dir, but synced with
		/// your normal sync-folder
		/// </summary>
		/// <param name="noteuid"></param>
		/// <param name="shareWith"></param>
		/// <returns></returns>
		bool RemoveShare(String noteuid, String shareWith);

		/// <summary>
		/// removes this note completely from the shares list
		/// </summary>
		/// <param name="noteuid"></param>
		/// <returns></returns>
		bool RemoveShare(String noteuid);

		/// <summary>
		/// get all share-info items
		/// </summary>
		/// <returns></returns>
		List<NoteShare> GetShares();

		/// <summary>
		/// get the share-info object for a specific note
		/// if the note isn't shared, null will be returned
		/// </summary>
		/// <param name="noteid"></param>
		/// <returns></returns>
		NoteShare GetNoteShare(String noteid);

		/// <summary>
		/// checks if a note is shared by its id
		/// </summary>
		/// <param name="noteuid"></param>
		/// <returns></returns>
		bool IsNoteShared(String noteuid);

		/// <summary>
		/// saves added/removed shares. Until you call this method, all the information
		/// is only held in memory
		/// </summary>
		/// <returns></returns>
		bool SaveShares();
	}

	/// <summary>
	/// share provider that manages shares for webdav sync
	/// </summary>
	class WebDavShareProvider : ShareProvider
	{
		// list of all shares
		private List<NoteShare> shares;
		// config file path
		private String configFile = null;
		// triggered when new share is added
		public event ShareAdded OnShareAdded;
		// triggered when a share is removed
		public event ShareRemoved OnShareRemoved;

		public WebDavShareProvider()
		{
			configFile = Path.Combine(Services.NativeApplication.ConfigurationDirectory, "shares.xml");
			shares = new List<NoteShare>();
			LoadFromConfig();
		}

		public bool SaveShares()
		{
			XmlWriter writer = XmlWriter.Create(configFile, XmlEncoder.DocumentSettings);
			writer.WriteStartDocument();
			writer.WriteStartElement("shares");
			foreach (NoteShare share in shares)
			{
				share.Serialize(writer);
			}
			writer.WriteEndElement();
			writer.WriteEndDocument();
			writer.Flush();
			writer.Close();
			return true;
		}

		public bool AddShare(String noteuid, String shareWith)
		{
			// check if this note is already shared
			NoteShare share = GetNoteShare(noteuid);
			bool shared = (share != null);
			bool wasAdded = false;

			if (!shared)
			{
				// not yet shared, request new webdav space to put share
				share = CreateNewShare(noteuid, shareWith);
				// now also add ourself! (because it makes no sense to not share it
				// with us, because then we couldn't decrypt our own files)
				share.sharedWith.Add(AddressBookFactory.Instance().GetDefault().GetOwnAddress().id);
				shares.Add(share);
				SaveShares();
				if (OnShareAdded != null)
					OnShareAdded(noteuid, shareWith);
				wasAdded = true;
			}
			else
			{
				if (share.IsSharedWith(shareWith))
				{
					// already shared with this person
					Logger.Info("note {0} already shared with {1}", noteuid, shareWith);
					wasAdded = true; // set to true anyway because we want to report that it was successful
				}
				else
				{
					share.sharedWith.Add(shareWith);
					SaveShares();
					if (OnShareAdded != null)
						OnShareAdded(noteuid, shareWith);
					wasAdded = true;
				}
			}

			return wasAdded;
		}

		public bool ImportShare(String share)
		{
			// download the stuff, then we should try to sync it...

			ShareSync sync = WebDAVShareSync.GetInstance(this);
			sync.Import(share);
			Dictionary<String, DirectoryInfo> imported = sync.GetShareCopies();

			// add all notes
			foreach (String id in imported.Keys)
			{
				// figure out with whom the notes are shared
				String filePath = Path.Combine(imported[id].FullName, id + ".note");
				List<String> sharers = new List<string>();

				if (sync.GetSharePartners(filePath, out sharers))
				{
					// TODO check sharers
					if (IsNoteShared(id))
					{
						// update the share-people with the one from the import
						NoteShare s = GetNoteShare(id);
						s.sharedWith.Clear();
						s.sharedWith.AddRange(sharers);
					}
					else
					{
						NoteShare s = new NoteShare(id, sharers, share);
						shares.Add(s);
					}
				}
				else
				{
					// some error
					Logger.Warn("some error while trying to get share partners for file {0}", filePath);
					return false;
				}
			}
			SaveShares();
			return true;
		}

		public bool RemoveShare(String noteuid, String shareWith)
		{
			bool removed = false;
			NoteShare share = GetNoteShare(noteuid);
			if (share != null)
			{
				if (share.sharedWith.Contains(shareWith))
				{
					share.sharedWith.Remove(shareWith);
				}
				if (share.sharedWith.Count <= 0)
					shares.Remove(share);
			}
			if (removed)
			{
				SaveShares();
				if (OnShareRemoved != null)
				{
					OnShareRemoved(noteuid, shareWith);
				}
			}
			return removed;
		}

		public bool RemoveShare(String noteuid)
		{
			NoteShare share = GetNoteShare(noteuid);
			bool removed = false;
			if (share != null)
			{
				shares.Remove(share);
				SaveShares();
				removed = true;
				foreach (String sharedwith in share.sharedWith)
				{
					if (OnShareRemoved != null)
						OnShareRemoved(noteuid, sharedwith);
				}
				share.sharedWith.Clear();
			}
			return removed;
		}

		public List<NoteShare> GetShares()
		{
			return shares;
		}

		public NoteShare GetNoteShare(String noteid)
		{
			foreach (NoteShare s in shares)
			{
				if (s.noteId.Equals(noteid))
					return s;
			}
			return null;
		}

		public bool IsNoteShared(String noteId)
		{
			return GetNoteShare(noteId) != null;
		}

		/// <summary>
		/// creates a new noteShare, it is also responsible for allocating a new shared
		/// space (sharePath location)
		/// </summary>
		/// <param name="noteuid"></param>
		/// <param name="shareWith"></param>
		/// <returns></returns>
		virtual internal NoteShare CreateNewShare(String noteuid, String shareWith)
		{
			String sharePath = "http://testuser3:test@testhost/webdav/testuser3";
			return new NoteShare(noteuid, shareWith, sharePath);
		}

		private void LoadFromConfig()
		{
			if (!File.Exists(configFile))
				return;
			XmlDocument xml = new XmlDocument();
			xml.Load(configFile);
			XmlNodeList nodes = xml.GetElementsByTagName("noteshare");
			// get a list of all existing note ids:
			List<String> existingIds = new List<string>();
			foreach (Note n in Tomboy.DefaultNoteManager.Notes)
				existingIds.Add(n.Id);

			foreach (XmlNode n in nodes)
			{
				NoteShare s = NoteShare.Deserialize(n);
				if (existingIds.Contains(s.noteId))
					shares.Add(s);
				else
					Logger.Debug("removing note {0} from shared notes because it doesn't exist any longer", s.noteId);
			}
			xml = null; // is there no .close() .dispose()?
		}

	}

}
