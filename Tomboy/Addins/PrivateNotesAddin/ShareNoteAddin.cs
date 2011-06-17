using Tomboy.Sync;
using System;
using Mono.Unix;
using System.Collections.Generic;
using Tomboy.PrivateNotes.Adress;

namespace Tomboy.PrivateNotes
{

	public class ShareNoteAddin : NoteAddin
	{
		Gtk.MenuItem shareItem;
		Gtk.MenuItem unshareItem;
		Gtk.MenuItem importSharedNoteItem;
		Gtk.MenuItem copyShareLinkItem;

		public override void Initialize()
		{
		}

		public override void Shutdown()
		{
			if (shareItem != null)
				shareItem.Activated -= OnShareItemActivated;
			if (unshareItem != null)
				unshareItem.Activated -= OnUnshareItemActivated;

			ShareProvider provider = SecureSharingFactory.Get().GetShareProvider();
			provider.OnShareAdded -= ShareAdded;
			provider.OnShareRemoved -= ShareRemoved;
		}

		public override void OnNoteOpened()
		{
			// Add the menu item when the window is created
			shareItem = new Gtk.MenuItem(
				Catalog.GetString("Share Note"));
			shareItem.Activated += OnShareItemActivated;
			shareItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Gdk.Key.a, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			shareItem.Show();
			AddPluginMenuItem(shareItem);
      
      unshareItem = new Gtk.MenuItem(
				Catalog.GetString("Unshare Note"));
			unshareItem.Activated += OnUnshareItemActivated;
			unshareItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Gdk.Key.u, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			unshareItem.Show();
			AddPluginMenuItem(unshareItem);

			importSharedNoteItem = new Gtk.MenuItem(
				Catalog.GetString("Import shared Note"));
			importSharedNoteItem.Activated += OnImportActivated;
			importSharedNoteItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Gdk.Key.i, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			importSharedNoteItem.Show();
			AddPluginMenuItem(importSharedNoteItem);

			copyShareLinkItem = new Gtk.MenuItem(
				Catalog.GetString("Copy share-link"));
			copyShareLinkItem.Activated += OnCopyShareLink;
			copyShareLinkItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Gdk.Key.s, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			copyShareLinkItem.Show();
			AddPluginMenuItem(copyShareLinkItem);

			ShareProvider provider = SecureSharingFactory.Get().GetShareProvider();
			provider.OnShareAdded += ShareAdded;
			provider.OnShareRemoved += ShareRemoved;

			CheckUnshareOption();
		}

		void ShareAdded(String noteid, String with)
		{
			if (noteid.Equals(Note.Id))
			{
				unshareItem.Sensitive = true;
			}
		}

		void ShareRemoved(String noteid, String with)
		{
			if (noteid.Equals(Note.Id))
			{
				CheckUnshareOption();
			}
		}

		private void CheckUnshareOption()
		{
			// if no longer shared at all
			if (!SecureSharingFactory.Get().GetShareProvider().IsNoteShared(Note.Id))
			{
				unshareItem.Sensitive = false;
			}
		}

		void OnShareItemActivated(object sender, EventArgs args)
		{
			Logger.Info("menu item clicked!");
			List<String> people = new List<String>();

			AddressBook ab = AddressBookFactory.Instance().GetDefault();
			// maybe we shouldn't do this every time
			ab.Load();
			List<AddressBookEntry> entries = ab.GetEntries();

			// get a list of people with whom it is already shared
			ShareProvider sp = SecureSharingFactory.Get().GetShareProvider();
			NoteShare share = sp.GetNoteShare(Note.Id);
			List<String> alreadySharedWith = new List<string>();
			if (share != null)
			{
				foreach (String id in share.sharedWith)
				{
					String cleanId = NoteShare.GetIdOnlyFromVariousFormats(id);
					alreadySharedWith.Add(cleanId);
				}
			}

			foreach (AddressBookEntry abe in entries)
			{
				if (alreadySharedWith.Contains(abe.id))
					people.Add("(active) " + abe.name + " " + abe.mail + " - " + abe.id);
				else
					people.Add(abe.name + " " + abe.mail + " - " + abe.id);
			}

			ItemSelector selector = new ItemSelector("Choose contact to share note with (type to search):", people, new inputDone(OnPeopleForShareChosen));
		}

		void OnUnshareItemActivated(object sender, EventArgs args)
		{
			Logger.Info("unshare menu item clicked!");
			bool removed = SecureSharingFactory.Get().GetShareProvider().RemoveShare(Note.Id);
			string message = Catalog.GetString("Error: Could not be unshared.");
			if (removed)
			{
				message = Catalog.GetString("Note is no longer shared.");
			}
			// DUMMY PARENT
			Gtk.Widget wid = new Gtk.Label();
			GtkUtil.ShowHintWindow(wid, Catalog.GetString("Sharing"), message);
		}

		void OnImportActivated(object sender, EventArgs args)
		{
			TextInput ti = new TextInput("enter share path:", "http://someone:secret@example.com/myShare/", "(" + AddinPreferences.NOTESHARE_URL_PREFIX + ")?http(s)?://.+", new inputDone(OnShareItemPathEntered));
		}

		void OnCopyShareLink(object sender, EventArgs args)
		{
			ShareProvider sp = SecureSharingFactory.Get().GetShareProvider();
			NoteShare share = sp.GetNoteShare(Note.Id);
			if (share != null)
			{
				String target = share.shareTarget;
				Gtk.Clipboard clipboard = Gtk.Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", false));
				clipboard.Text = AddinPreferences.NOTESHARE_URL_PREFIX + target;
			}
			else
			{
				Gtk.MessageDialog md = new Gtk.MessageDialog(null, Gtk.DialogFlags.Modal, Gtk.MessageType.Info,
					Gtk.ButtonsType.Ok, Catalog.GetString("Not shared yet. Click on 'Share Note' and select with whom you want to share first"));
				md.Run();
				md.Destroy();
			}
		}

		void OnShareItemPathEntered(bool ok, String sharepath)
		{
			if (ok && !String.IsNullOrEmpty(sharepath))
			{
				Logger.Info("sharepath add request: {0}", sharepath);
				try
				{
					bool success = SecureSharingFactory.Get().GetShareProvider().ImportShare(sharepath);

					// DUMMY PARENT
					Gtk.Widget wid = new Gtk.Label();
					String message = Catalog.GetString("Notes successfully imported. Please execute note synchronization to get the new notes.");
					if (!success)
						message = Catalog.GetString("Import failed. Could not get the note(s) from the specified location.");
					GtkUtil.ShowHintWindow(wid, Catalog.GetString("Import"), message);
				}
				catch (Exception _e)
				{
					Logger.Warn("could not import share", _e);
					// DUMMY PARENT
					Gtk.Widget wid = new Gtk.Label();
					String message = Catalog.GetString("Could not import note because of the following error:\n");
					message += _e.Message;
					GtkUtil.ShowHintWindow(wid, Catalog.GetString("Import"), message);
				}
			}
			else
			{
				// nothing, user cancelled
			}
		}

		void OnPeopleForShareChosen(bool ok, String selection)
		{
            if (ok && !String.IsNullOrEmpty(selection))
			{
				Logger.Info("person selected: {0}", selection);
				bool added = SecureSharingFactory.Get().GetShareProvider().AddShare(Note.Id, selection);
				string message = Catalog.GetString("Error: Note not shared.");
				if (added)
				{
					message = Catalog.GetString("Note is now shared...");
				}
				// DUMMY PARENT
				Gtk.Widget wid = new Gtk.Label();
				GtkUtil.ShowHintWindow(wid, Catalog.GetString("Sharing"), message);
			}
			else
			{
				// nothing
			}
		}

	}

}