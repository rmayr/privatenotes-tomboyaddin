// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
using Infinote;
using PrivateNotes;
using PrivateNotes.Infinote;
using Tomboy.Sync;
using System;
using Mono.Unix;
using System.Collections.Generic;
using Tomboy.PrivateNotes.Adress;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// a note addin can do things when a note is viewed in a window. for example
	/// displaying new gui elements there
	/// 
	/// this note addin provides functionality for managing shares over every note window
	/// </summary>
	public class ShareNoteAddin : NoteAddin
	{
		private Gtk.MenuItem shareItem;
		private Gtk.MenuItem unshareItem;
		private Gtk.MenuItem importSharedNoteItem;
		private Gtk.MenuItem copyShareLinkItem;
		private Gtk.MenuItem liveItem;

		private bool? isLiveEditing = false;

		public override void Initialize()
		{
			GpgConfigUtility.ConfigureIfNecessary(null);
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
			Communicator.Instance.OnLiveEditingStateChanged -= EditingStateChanged;
		}

		/// <summary>
		/// gets called when a note window is openend
		/// here we add the gui elements
		/// </summary>
		public override void OnNoteOpened()
		{
			// Add the menu item when the window is created
			shareItem = new Gtk.MenuItem(
				Catalog.GetString("Share Note"));
			shareItem.Activated += OnShareItemActivated;
			shareItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Gdk.Key.r, Gdk.ModifierType.ControlMask,
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

			liveItem = new Gtk.MenuItem(
				Catalog.GetString("Live Note Editing"));
			liveItem.Activated += OnLiveItemActivated;
			liveItem.AddAccelerator("live", Window.AccelGroup,
				(uint)Gdk.Key.l, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			liveItem.Show();
			AddPluginMenuItem(liveItem);
			CheckLiveEditingStatus();

			Gtk.MenuItem editAddressesItem = new Gtk.MenuItem(
				Catalog.GetString("Edit Cooperation Addresses"));
			editAddressesItem.Activated += OnEditAddressesActivated;
			editAddressesItem.Show();
			AddPluginMenuItem(editAddressesItem);

			Communicator.Instance.OnLiveEditingStateChanged += EditingStateChanged;
		}

		// ---------------
		// end of NoteAddin overrides
		// ---------------

#region delegate-impls

		/// <summary>
		/// callback that will be executed when any share is added to our share-manager
		/// </summary>
		/// <param name="noteid"></param>
		/// <param name="with"></param>
		void ShareAdded(String noteid, String with)
		{
			if (noteid.Equals(Note.Id))
			{
				unshareItem.Sensitive = true;
			}
		}

		/// <summary>
		/// callback that will be executed when any share is removed from our share-manager
		/// </summary>
		/// <param name="noteid"></param>
		/// <param name="with"></param>
		void ShareRemoved(String noteid, String with)
		{
			if (noteid.Equals(Note.Id))
			{
				CheckUnshareOption();
			}
		}

		void EditingStateChanged(String noteId, bool started)
		{
			if (noteId.Equals(Note.Id))
			{
				CheckLiveEditingStatus();
			}
		}

#endregion

		/// <summary>
		/// this method checks if this note is shared and if not it will deactivate the "unshare" element
		/// so that it does not confuse the user
		/// </summary>
		private void CheckUnshareOption()
		{
			// if no longer shared at all
			if (!SecureSharingFactory.Get().GetShareProvider().IsNoteShared(Note.Id))
			{
				unshareItem.Sensitive = false;
			}
		}

		/// <summary>
		/// the user has clicked on the "share" item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
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

		/// <summary>
		/// the user has clicked "unshare"
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
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

		void OnEditAddressesActivated(object sender, EventArgs args)
		{
			String filePath = Communicator.Instance.AddressProvider.AddressFile;
			try
			{
				System.Diagnostics.Process.Start(filePath);
			}
			catch (Exception e)
			{
				Logger.Warn("Cannot opern address file", e);
			}
		}

		void OnLiveItemActivated(object sender, EventArgs args)
		{
			CheckLiveEditingStatus();
			if (!isLiveEditing.HasValue)
			{
				// null, so no communicator there, show info
				Gtk.Widget wid = new Gtk.Label();
				GtkUtil.ShowHintWindow(wid, Catalog.GetString("Xmpp Configuration Error"), Catalog.GetString("Please go to sync preferences and configure Xmppp User+Password correctly to use this feature."));
			}
			else if (!isLiveEditing.Value)
			{
				//Communicator.Instance.testSend(Note.Title);
				List<XmppEntry> possible = Communicator.Instance.AddressProvider.GetAppropriateForNote(Note.Id);
				if (possible.Count > 0)
				{
					// show a list for the user to select with whom to co-edit
					List<object> tempList = new List<object>();
					tempList.AddRange(possible.ToArray());
					new MultiButtonPartnerSelector("Select cooperation-partner:", tempList, OnSelectEditPartner);
				}
				else
				{
					GtkUtil.ShowHintWindow("Error", "No one is available to live-edit with!");
				}
			}
			else
			{
				// commit
				bool worked = Communicator.Instance.CommitNoteLiveEditing(Note.Id);
				Logger.Info(String.Format("Commiting live note editing... {0}", worked ? "ok" : "error"));
			}
		}

		void OnSelectEditPartner(bool ok, Object resultObj)
		{
			if (ok)
			{
				XmppEntry partner = resultObj as XmppEntry;
				if (partner != null)
				{
					List<String> onlinePartners = Communicator.Instance.GetOnlinePartnerIds();
					bool online = onlinePartners.Contains(partner.XmppId);
					if (online)
					{
						Logger.Warn("Triggering live note editing! :) with user " + partner.XmppId);
						bool worked = Communicator.Instance.StartNoteLiveEditing(Note.Id, partner.XmppId);
						Logger.Info(String.Format("Live editing on {0} with {1} {2}", Note.Id, partner.XmppId,
						                          ((worked) ? "was started" : "could not be started")));
					}
					else
					{
						GtkUtil.ShowHintWindow(Catalog.GetString("Error"), Catalog.GetString("Cannot start live editing with offline user."));
					}
				}
			}
		}

		void CheckLiveEditingStatus()
		{
			bool communicatorAvailable = Communicator.Instance.IsConfigured();
			if (!communicatorAvailable)
			{
				((Gtk.Label)liveItem.Child).Text =
						Catalog.GetString("Live Editing");
				isLiveEditing = null;
			}
			else
			{
				var before = isLiveEditing.HasValue && isLiveEditing.Value;
				isLiveEditing = Communicator.Instance.IsInLiveEditMode(Note.Id);
				if (before != isLiveEditing)
				{
					((Gtk.Label) liveItem.Child).Text =
						Catalog.GetString(isLiveEditing.Value ? "Commit Live Note Editing" : "Live Note Editing");
				}
			}
		}

		/// <summary>
		/// the user has clicked "import share"
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void OnImportActivated(object sender, EventArgs args)
		{
			TextInput ti = new TextInput("enter share path:", "http://someone:secret@example.com/myShare/", "(" + AddinPreferences.NOTESHARE_URL_PREFIX + ")?http(s)?://.+", new inputDone(OnShareItemPathEntered));
		}

		/// <summary>
		/// the user has clicked "copy share link"
		/// here we will copy a link that will let other users add this note from the share
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
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

		/// <summary>
		/// callback for when the user wants to import a note and has already entered a url for this
		/// </summary>
		/// <param name="ok"></param>
		/// <param name="sharepath"></param>
		void OnShareItemPathEntered(bool ok, String sharepath)
		{
			if (ok && !String.IsNullOrEmpty(sharepath))
			{
				Logger.Info("sharepath add request: {0}", sharepath);
			    SharingAppAddin.ImportShareFromShareUrl(sharepath);
			}
			else
			{
				// nothing, user cancelled
			}
		}

		/// <summary>
		/// when the user wants to share a note and has selected somebody to share it with
		/// it might be that the note is not even shared with anybody at this point (not in
		/// the shares-list at all)
		/// </summary>
		/// <param name="ok"></param>
		/// <param name="selection"></param>
		void OnPeopleForShareChosen(bool ok, String selection)
		{
			if (ok && !String.IsNullOrEmpty(selection))
			{
				Logger.Info("person selected: {0}", selection);
				bool added = false;
				string error = null;
				try
				{
					added = SecureSharingFactory.Get().GetShareProvider().AddShare(Note.Id, selection);
				}
				catch (Exception e)
				{
					error = e.Message;
				}
				string message = Catalog.GetString("Error: Note not shared.");
				if (added)
				{
					message = Catalog.GetString("Note is now shared...");
				}
				else if (error != null)
				{
					message += "\n" + error;
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