// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 

using System.Diagnostics;
using Gdk;
using Gtk;
using Infinote;
using PrivateNotes;
using PrivateNotes.Infinote;
using Tomboy.Sync;
using System;
using Mono.Unix;
using System.Collections.Generic;
using Tomboy.PrivateNotes.Adress;
using com.google.zxing;
using com.google.zxing.common;
using com.google.zxing.qrcode;
using Key = Gdk.Key;
using Object = System.Object;
using Window = Gtk.Window;

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
		private MenuItem shareItem;
		private MenuItem unshareItem;
		private MenuItem importSharedNoteItem;
		private MenuItem copyShareLinkItem;
		private MenuItem liveItem;

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
			shareItem = new MenuItem(
				Catalog.GetString("Share Note"));
			shareItem.Activated += OnShareItemActivated;
			shareItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Key.r, ModifierType.ControlMask,
				AccelFlags.Visible);
			shareItem.Show();
			AddPluginMenuItem(shareItem);
      
      unshareItem = new MenuItem(
				Catalog.GetString("Unshare Note"));
			unshareItem.Activated += OnUnshareItemActivated;
			unshareItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Key.u, ModifierType.ControlMask,
				AccelFlags.Visible);
			unshareItem.Show();
			AddPluginMenuItem(unshareItem);

			importSharedNoteItem = new MenuItem(
				Catalog.GetString("Import shared Note"));
			importSharedNoteItem.Activated += OnImportActivated;
			importSharedNoteItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Key.i, ModifierType.ControlMask,
				AccelFlags.Visible);
			importSharedNoteItem.Show();
			AddPluginMenuItem(importSharedNoteItem);

			copyShareLinkItem = new MenuItem(
				Catalog.GetString("Copy share-link"));
			copyShareLinkItem.Activated += OnCopyShareLink;
			copyShareLinkItem.AddAccelerator("activate", Window.AccelGroup,
				(uint)Key.s, ModifierType.ControlMask,
				AccelFlags.Visible);
			copyShareLinkItem.Show();
			AddPluginMenuItem(copyShareLinkItem);

			ShareProvider provider = SecureSharingFactory.Get().GetShareProvider();
			provider.OnShareAdded += ShareAdded;
			provider.OnShareRemoved += ShareRemoved;

			CheckUnshareOption();

			liveItem = new MenuItem(
				Catalog.GetString("Live Note Editing"));
			liveItem.Activated += OnLiveItemActivated;
			liveItem.AddAccelerator("live", Window.AccelGroup,
				(uint)Key.l, ModifierType.ControlMask,
				AccelFlags.Visible);
			liveItem.Show();
			AddPluginMenuItem(liveItem);
			CheckLiveEditingStatus();

			MenuItem editAddressesItem = new MenuItem(
				Catalog.GetString("Edit Cooperation Addresses"));
			editAddressesItem.Activated += OnEditAddressesActivated;
			editAddressesItem.Show();
			AddPluginMenuItem(editAddressesItem);

			ImageMenuItem syncWithAndroid = new ImageMenuItem(
				Catalog.GetString("Sync all with Android device"));
			syncWithAndroid.Image = Icons.PhoneIcon;
			syncWithAndroid.Activated += OnSyncAndroidActivated;
			syncWithAndroid.Show();
			AddPluginMenuItem(syncWithAndroid);

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
			List<string> people = new List<string>();

			AddressBook ab = AddressBookFactory.Instance().GetDefault();
			// maybe we shouldn't do this every time
			ab.Load();
			List<AddressBookEntry> entries = ab.GetEntries();

			// get a list of people with whom it is already shared
			ShareProvider sp = SecureSharingFactory.Get().GetShareProvider();
			NoteShare share = sp.GetNoteShare(Note.Id);
			List<string> alreadySharedWith = new List<string>();
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
			Widget wid = new Label();
			GtkUtil.ShowHintWindow(wid, Catalog.GetString("Sharing"), message);
		}

		/// <summary>
		/// show qr code that sets up the android sync settings to sync with the same location as this tomboy instance
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void OnSyncAndroidActivated(object sender, EventArgs args)
		{
			Uri serverUri = new Uri(Convert.ToString(Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH)));
			string serverUser = (string)Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER);
			string serverPass = (string)Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS);
			var builder = new UriBuilder(serverUri);
			builder.UserName = serverUser;
			builder.Password = serverPass;
			String fullUri = builder.Uri.ToString();

			String link = AddinPreferences.NOTESYNCCONFIG_URL_PREFIX + fullUri;
			GtkUtil.ShowQrCode("Scan on Android", "Install 'PrivateNotes' from Google Play store, then scan this code with a QR code reader.", link);
		}

		void OnEditAddressesActivated(object sender, EventArgs args)
		{
			String filePath = Communicator.Instance.AddressProvider.AddressFile;
			try
			{
				Process.Start(filePath);
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
				Widget wid = new Label();
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
					new MultiButtonPartnerSelector("Select cooperation-partner:", tempList, OnSelectEditPartner, (Window)Note.Window.Toplevel);
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
					List<string> onlinePartners = Communicator.Instance.GetOnlinePartnerIds();
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
				((Label)liveItem.Child).Text =
						Catalog.GetString("Live Editing");
				isLiveEditing = null;
			}
			else
			{
				var before = isLiveEditing.HasValue && isLiveEditing.Value;
				isLiveEditing = Communicator.Instance.IsInLiveEditMode(Note.Id);
				if (before != isLiveEditing)
				{
					((Label) liveItem.Child).Text =
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
				TaggedValue<string, int>[] values = new TaggedValue<string, int>[]
				                                    	{
				                                    		new TaggedValue<string, int>("Copy to clipboard", 0),
															new TaggedValue<string, int>("Show QR code for PrivateNotes on Android", 1)
				                                    	};
				MultiButtonCopySelector selector = new MultiButtonCopySelector("What do you want to do?",
					new List<object>(values), OnCopyActionSelected, (Window)Note.Window.Toplevel);
			}
			else
			{
				MessageDialog md = new MessageDialog(null, DialogFlags.Modal, MessageType.Info,
					ButtonsType.Ok, Catalog.GetString("Not shared yet. Click on 'Share Note' and select with whom you want to share first"));
				md.Run();
				md.Destroy();
			}
		}

		/// <summary>
		/// user clicked on of the copy share link options
		/// </summary>
		/// <param name="ok"></param>
		/// <param name="resultObj"></param>
		void OnCopyActionSelected(bool ok, Object resultObj)
		{
			var t = resultObj as TaggedValue<string, int>;
			if (ok && t != null)
			{
				ShareProvider sp = SecureSharingFactory.Get().GetShareProvider();
				NoteShare share = sp.GetNoteShare(Note.Id);
				String target = share.shareTarget;
				if (t.Tag == 0)
				{
					Clipboard clipboard = Clipboard.Get(Atom.Intern("CLIPBOARD", false));
					clipboard.Text = AddinPreferences.NOTESHARE_URL_PREFIX + target;
				}
				else
				{
					GtkUtil.ShowQrCode("QrCode for transfer to PrivateNotes for Android", null, AddinPreferences.NOTESHARE_URL_PREFIX + target);
				}
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
			    ImportShareFromShareUrl(sharepath);
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
				Widget wid = new Label();
				GtkUtil.ShowHintWindow(wid, Catalog.GetString("Sharing"), message);
			}
			else
			{
				// nothing
			}
		}

		/// <summary>
		/// same as ImportShare, just static for use by internal PrivateNotes gui for example
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public static bool ImportShareFromShareUrl(String info)
		{
			String errorMessage = "";
			bool success = false;
			if (info.StartsWith(AddinPreferences.NOTESHARE_URL_PREFIX))
			{
				String url = info.Substring(AddinPreferences.NOTESHARE_URL_PREFIX.Length);
				Logger.Info("we should import {0}", url);
				try
				{
					success = SecureSharingFactory.Get().GetShareProvider().ImportShare(url);
				}
				catch (Exception _e)
				{
					Logger.Warn("importing failed with exception {0} msg: {1}", _e.GetType().Name, _e.Message);
					errorMessage = _e.Message;
				}
			}
			else
			{
				Logger.Warn("we should import {0}, which isnt a valid share-url,"
				            + "they look like this: {1}SOMETHING", info, AddinPreferences.NOTESHARE_URL_PREFIX);
			}
			GtkUtil.ShowHintWindow("Sharing",
			                       success
			                       	? "Share successfully imported. Synchronize now to get access to the note."
			                       	: "Importing share failed with error:\n" + errorMessage + "\nFix this problem if you can or try again later.");
			return success;
		}
	}

}