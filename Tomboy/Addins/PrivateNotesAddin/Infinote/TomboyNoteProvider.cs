using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Infinote;
using Tomboy;
using Tomboy.PrivateNotes;

namespace PrivateNotes.Infinote
{
	class TomboyNoteProvider : NoteProvider
	{
		public event NoteChanged OnNoteChanged;

		public const string NOTE_URI_PREFIX = "note://tomboy/";

		private Dictionary<String, Note> noteObjCache = new Dictionary<string, Note>();

		private List<String> registeredForNotes = new List<string>(); 

		private static readonly object locker = new object();

		private bool locked = false;

		private List<String> cachedShares = new List<string>(); 

		public string GetNoteContent(string noteId)
		{
			Note n = Tomboy.Tomboy.DefaultNoteManager.FindByUri("note://tomboy/" + noteId);
			if (n == null)
				return "";

			return GetNoteText(n);
		}

		public bool HasNote(string noteId)
		{
			Note n = Tomboy.Tomboy.DefaultNoteManager.FindByUri(NOTE_URI_PREFIX + noteId);
			return n != null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="noteId"></param>
		/// <param name="with">xmpp id</param>
		/// <returns></returns>
		public bool IsNoteSharedWith(string noteId, string with)
		{
			// this does not yet take into account that shared can be added/removed!
			bool shared = false;
			if (cachedShares.Contains(noteId+with)) {
				return true;
			}

			NoteShare share = SecureSharingFactory.Get().GetShareProvider().GetNoteShare(noteId);
			XmppEntry info = Communicator.Instance.AddressProvider.GetEntryForXmppId(with);
			if (share != null && info != null)
			{
				shared = share.IsSharedWith(info.Person.id);
				if (shared)
				{
					cachedShares.Add(noteId + with);
				}
			}
			return shared;
		}

		public void UpdateNoteContent(string noteId, string content)
		{
			Note note = GetNote(noteId);
			if (note != null)
			{
				// run this on ui thread!
				Gtk.Application.Invoke(delegate(object sender, EventArgs ea)
				                       	{
											lock (locker)
											{
												if (!note.IsOpened)
												{
													// must be opened to be edited
													note.Window.Present();
												}
												locked = true;
												UpdateContent(note, content);
												locked = false;
											}
				                       	});
			}
		}

		public void ObserveNote(string noteId)
		{
			Note n = GetNote(noteId);
			if (n != null)
			{
				RegisterListener(n);
			}
			else
			{
				Logger.Warn("Note with id {0} does not exist", noteId);
			}
		}

		public void SaveNote(String noteId)
		{
			Note n = GetNote(noteId);
			if (n != null)
			{
				n.Save();
			}
		}

		private void RegisterListener(Note note)
		{
			//only register once for every note
			if (!registeredForNotes.Contains(note.Id))
			{
				registeredForNotes.Add(note.Id);
			}
			else
			{
				return;
			}

			note.BufferChanged += delegate(Note n)
			{
				if (!locked)
				{
					NoteContentChanged(note);
				}
			};

			if (note.HasBuffer)
			{
				//change in formatting
				note.Buffer.TagApplied += delegate(object o, Gtk.TagAppliedArgs args)
                  	{
                  		if (!locked && args.Tag is NoteTag)
                  		{
							NoteContentChanged(note);
                  		}
                  	};
				note.Buffer.TagRemoved += delegate(object o, Gtk.TagRemovedArgs args)
					{
						if (!locked && args.Tag is NoteTag)
						{
							NoteContentChanged(note);
						}
					};
			}
		}

		private void NoteContentChanged(Note n)
		{
			if (OnNoteChanged != null)
			{
				String newContent = GetNoteText(n);
				if (!String.IsNullOrEmpty(newContent)) {
					// sometimes we get an empty string from tomboy, don't know why...
					OnNoteChanged(n.Id, newContent);
				}
			}
		}

		public Note GetNote(String noteId)
		{
			if (noteObjCache.ContainsKey(noteId))
				return noteObjCache[noteId];
			Note n = Tomboy.Tomboy.DefaultNoteManager.FindByUri(NOTE_URI_PREFIX + noteId);
			if (n != null)
			{
				noteObjCache.Add(noteId, n);
			}
			return n;
		}

#region methods_for_accessing_note_content

		internal virtual String GetNoteText(Note n)
		{
			return n.XmlContent;
		}

		internal virtual void UpdateContent(Note n, String content)
		{
			try
			{
				XmlDocument testDoc = new XmlDocument();
				StringReader sr = new StringReader(content);
				XmlTextReader tr = new XmlTextReader(sr);
				tr.Namespaces = false;
				testDoc.Load(tr);
			}
			catch (XmlException e)
			{
				// this is not valid xml currently!
				// so we simply skip this update
				//Logger.Warn("this is not valid xml, wait for next update! -=not nice but quick fix=-");
				return;
			}

			n.XmlContent = content;
		}

#endregion

	}

	/// <summary>
	/// class that only accesses the text-content
	/// this is safer (no xml-related errors can happen), however there is absolutely no support for formatting!
	/// so if one side formats, the other side doesn't get any feedback about that!
	/// possibly problems with syncing later!
	/// </summary>
	class TomboyPlainNoteProvider : TomboyNoteProvider
	{
		internal override String GetNoteText(Note n)
		{
			return n.TextContent;
		}

		internal override void UpdateContent(Note n, String content)
		{
			n.TextContent = content;
		}
	}
}
