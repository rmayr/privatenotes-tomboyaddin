using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using Tomboy.PrivateNotes.Adress;

namespace Tomboy.PrivateNotes
{
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
			return sharedWith.Contains(with);
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
		
	}

	public delegate void ShareAdded(String noteid, String with);
	public delegate void ShareRemoved(String noteid, String with);

	public interface ShareProvider
	{

		event ShareAdded OnShareAdded;
		event ShareRemoved OnShareRemoved;

		bool AddShare(String noteuid, String shareWith);

		/// <summary>
		/// add a share via a path/location that sb has sent you55
		/// </summary>
		/// <param name="share"></param>
		/// <returns></returns>
		bool ImportShare(String share);

		bool RemoveShare(String noteuid, String shareWith);

		/// <summary>
		/// removes this note completely from the shares list
		/// </summary>
		/// <param name="noteuid"></param>
		/// <returns></returns>
		bool RemoveShare(String noteuid);

		List<NoteShare> GetShares();

		NoteShare GetNoteShare(String noteid);

		bool IsNoteShared(String noteuid);

		bool SaveShares();
	}

	public class ShareProviderFactory
	{
		static WebDavShareProvider provider = null;
		public static ShareProvider GetShareProvider()
		{
			if (provider == null) {
				provider = new WebDavShareProvider();
			}
			return provider;
		}
	}

	class WebDavShareProvider : ShareProvider
	{
		private List<NoteShare> shares;
		private String configFile = null;
		public event ShareAdded OnShareAdded;
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
			String sharePath = "http://testuser3:test@testhost/webdav/testuser3";

			if (!shared)
			{
				// not yet shared, request new webdav space to put share
				share = new NoteShare(noteuid, shareWith, sharePath);
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

		private void LoadFromConfig()
		{
			if (!File.Exists(configFile))
				return;
			XmlDocument xml = new XmlDocument();
			xml.Load(configFile);
			XmlNodeList nodes = xml.GetElementsByTagName("noteshare");
			foreach (XmlNode n in nodes)
				shares.Add(NoteShare.Deserialize(n));
			xml = null; // is there no .close() .dispose()?
		}

	}
}
