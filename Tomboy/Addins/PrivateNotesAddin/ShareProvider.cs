using System;
using System.Collections.Generic;
using System.Text;

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
	}

	public delegate void ShareAdded(String noteid, String with);
	public delegate void ShareRemoved(String noteid, String with);

	public interface ShareProvider
	{

		event ShareAdded OnShareAdded;
		event ShareRemoved OnShareRemoved;

		bool AddShare(String noteuid, String shareWith);

		bool RemoveShare(String noteuid, String shareWith);

		/// <summary>
		/// removes this note completely from the shares list
		/// </summary>
		/// <param name="noteuid"></param>
		/// <returns></returns>
		bool RemoveShare(String noteuid);

		List<NoteShare> GetShares();

		bool IsNoteShared(String noteuid);
	}

	class WebDavShareProvider : ShareProvider
	{
		private List<NoteShare> shares;
		public event ShareAdded OnShareAdded;
		public event ShareRemoved OnShareRemoved;

		public WebDavShareProvider()
		{
			shares = new List<NoteShare>();
			// adds 1 default test share
			foreach (Note note in Tomboy.DefaultNoteManager.Notes)
			{
				Logger.Info("got note {0}", note.Id);
				AddShare(note.Id, "felix");
				break;
			}
		}

		public bool AddShare(String noteuid, String shareWith)
		{
			// check if this note is already shared
			NoteShare share = GetNoteShare(noteuid);
			bool shared = (share != null);

			bool wasAdded = false;
			String sharePath = "http://testuser3:test@localhost/webdav/testuser3";

			if (!shared)
			{
				// not yet shared, request new webdav space to put share
				share = new NoteShare(noteuid, shareWith, sharePath);
				shares.Add(share);
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
					if (OnShareAdded != null)
						OnShareAdded(noteuid, shareWith);
					wasAdded = true;
				}
			}

			return wasAdded;
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

		public bool IsNoteShared(String noteId)
		{
			return GetNoteShare(noteId) != null;
		}

		public NoteShare GetNoteShare(String noteId)
		{
			foreach (NoteShare share in shares)
			{
				if (share.noteId.Equals(noteId))
					return share;
			}
			return null;
		}
	}
}
