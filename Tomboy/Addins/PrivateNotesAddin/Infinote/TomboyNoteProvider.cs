// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
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

	/// <summary>
	/// provides access to the tomboy notes
	/// </summary>
	class TomboyNoteProvider : NoteProvider
	{
		/// <summary>
		/// fired when a note gets changed
		/// </summary>
		public event NoteChanged OnNoteChanged;

		public const string NOTE_URI_PREFIX = "note://tomboy/";

		/// <summary>
		/// cache note-objects, so that we don't always have to query them from the tomboy instance
		/// </summary>
		private Dictionary<String, Note> noteObjCache = new Dictionary<string, Note>();

		/// <summary>
		/// list of notes for which we have registered for changes
		/// </summary>
		private List<String> registeredForNotes = new List<string>(); 

		/// <summary>
		/// lock obj, to protect from possibly harmful actions in the gui
		/// </summary>
		private static readonly object locker = new object();

		/// <summary>
		/// set to true while 'we' (the program) is making an update to the note, so we can
		/// distinguish between user changes and our own
		/// </summary>
		private bool locked = false;

		/// <summary>
		/// checking if a note is shared with somebody is rather expensive, so we cache this information
		/// </summary>
		private List<String> cachedShares = new List<string>(); 

		/// <summary>
		/// retrieve content from a note
		/// </summary>
		/// <param name="noteId"></param>
		/// <returns></returns>
		public string GetNoteContent(string noteId)
		{
			Note n = GetNote(noteId);
			if (n == null)
				return "";

			return GetNoteText(n);
		}

		/// <summary>
		/// is a note available
		/// </summary>
		/// <param name="noteId"></param>
		/// <returns></returns>
		public bool HasNote(string noteId)
		{
			Note n = GetNote(noteId);
			return n != null;
		}

		/// <summary>
		/// check if a note is shared with somebody (by xmpp-id, not key-ids/fingerprints!)
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

		/// <summary>
		/// update the content of a note
		/// </summary>
		/// <param name="noteId"></param>
		/// <param name="content"></param>
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

		/// <summary>
		/// observe a note for changes
		/// </summary>
		/// <param name="noteId"></param>
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

		/// <summary>
		/// save a note (persist to disc)
		/// </summary>
		/// <param name="noteId"></param>
		public void SaveNote(String noteId)
		{
			Note n = GetNote(noteId);
			if (n != null)
			{
				n.Save();
			}
		}

		/// <summary>
		/// register as listener for note changes
		/// this is protected against double-registering
		/// </summary>
		/// <param name="note"></param>
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

		/// <summary>
		/// the content of a note has changed
		/// this emits the OnNoteChanged event after some security checks
		/// </summary>
		/// <param name="n"></param>
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

		/// <summary>
		/// retrieve a note, more efficient because uses caching, so it
		/// doesn't always have to let tomboy search for it
		/// </summary>
		/// <param name="noteId"></param>
		/// <returns></returns>
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
