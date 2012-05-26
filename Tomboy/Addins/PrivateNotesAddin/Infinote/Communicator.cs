// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
using System;
using System.Collections.Generic;
using Infinote;
using Tomboy;
using Tomboy.PrivateNotes;
using Tomboy.Sync;
using XmppOtrLibrary;
using Logger = Tomboy.Logger;

namespace PrivateNotes.Infinote
{
	/// <summary>
	/// 
	/// </summary>
	/// <param name="started">when false, it means stopped</param>
	internal delegate void NoteLiveEditingChanged(String noteId, bool started);

	/// <summary>
	/// provides communication with other parties (e.g. for co-operative editing of a note)
	/// </summary>
	class Communicator
	{

#region members

		//public MultiNoteEditor NoteEditors { get; private set; }
		private MultiNoteEditor NoteEditors { get; set; }

		// connection retries
		public int RetryCount { get; private set; }
		private const int MAX_RETRIES = 10;

		/// <summary>
		/// get the notes from there
		/// </summary>
		public TomboyNoteProvider NoteProvider { get { return (NoteEditors == null) ? null : (TomboyNoteProvider)NoteEditors.Provider; } }

		/// <summary>
		/// contains noteId (key) -> withUser (value)
		/// </summary>
		private Dictionary<String, String> currentCooperations = new Dictionary<string, string>();

		/// <summary>
		/// the xmpp connection stuff
		/// </summary>
		private EasyXmpp xmpp;

		// credentials
		private string server, user, pw;

		// the live editing state of a note has changed
		public event NoteLiveEditingChanged OnLiveEditingStateChanged;

		public XmppAddressProvider AddressProvider { get; private set; }

		/// <summary>
		/// displays status information about notes that get edited currently
		/// </summary>
		private LiveEditingInfoWindow infoWindow = new LiveEditingInfoWindow();

		/// <summary>
		/// holds information about which peers have been authenticated successfully
		/// </summary>
		private Authenticator authenticator;

#endregion


#region Singleton
		private static Communicator _instance;

		public static Communicator Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new Communicator();
					//TestSetup();
				}
				return _instance;
			}
		}
#endregion

		private Communicator()
		{
			AddressProvider = new XmppAddressProvider();
			AddressProvider.Load();
			AddressProvider.UpdateAddressBookFile();
			authenticator = new Authenticator(this);
			// allow authenticator to emit a message
			authenticator.OnSendAuthMsg += SendMessageToUser;
		}

#region public-methods

		public bool IsConfigured()
		{
			var server = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPSERVER) as string;
			var user = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPUSER) as string;
			var pw = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPPW) as string;
			return !(String.IsNullOrEmpty(server) || String.IsNullOrEmpty(user) || pw == null);
		}

		/// <summary>
		/// returns true if there is some work going on (connecting, actively connected, ...)
		/// only returns false if it has only be instantiated yet, or if the max-retries have
		/// passed an nothing more is happening
		/// </summary>
		/// <returns></returns>
		public bool IsActive()
		{
			return (RetryCount > 0 && RetryCount <= MAX_RETRIES);
		}

		public void Connect()
		{
			if (!GetConfiguration())
				throw new InvalidOperationException("Xmpp service not configured yet");
			// reset retry count on external request
			RetryCount = 0;
			ConnectXmpp();
		}

		public bool StartNoteLiveEditing(String noteId, String withUser)
		{
			if (NoteEditors == null)
			{
				return false;
			}

			string occupyingUser;
			if (currentCooperations.TryGetValue(noteId, out occupyingUser))
			{
				if (occupyingUser != withUser)
				{
					return false;
				}
			}
			else
			{
				currentCooperations.Add(noteId, withUser);
			}
			// destroy it before, otherwise we might have problems with changes that have been made in the meantime
			NoteEditors.DestroyEditorStateMachine(noteId);
			var esm = NoteEditors.GetOrCreateEditorStateMachine(noteId, withUser);
			esm.OnStateChanged += delegate(EditorStateMachine.SmState state)
									{
										DlgOnEditorStateChanged(noteId, state);
									};
			return esm.InitCooperation();
		}

		public bool CommitNoteLiveEditing(String noteId)
		{
			if (NoteEditors == null)
			{
				return false;
			}

			var esm = NoteEditors.FindEditorStateMachine(noteId);
			var result = false;
			if (esm != null)
			{
				result = esm.CommitCooperation();
			}
			return result;
		}

		/// <summary>
		/// gets all online (and already authenticated) users
		/// </summary>
		/// <returns></returns>
		public List<String> GetOnlinePartnerIds()
		{
			List<String> results = new List<string>();
			if (xmpp == null)
			{
				return results;
			}

			foreach (XmppPartner partner in xmpp.PartnerObjects)
			{
				if (partner.IsOnline)
				{
					if (authenticator.IsAuthenticated(partner.Name))
					{
						results.Add(partner.Name);
					}
				}
			}
			return results;
		}

		public bool IsInLiveEditMode(String noteId)
		{
			if (NoteEditors == null)
			{
				return false;
			}

			var esm = NoteEditors.FindEditorStateMachine(noteId);
			var result = false;
			if (esm != null)
			{
				result = esm.CurrentState == EditorStateMachine.SmState.Editing;
			}
			return result;
		}

#endregion

#region privates

		/// <summary>
		/// sends a message via the encrypted channel to another user
		/// </summary>
		/// <param name="to"></param>
		/// <param name="msg"></param>
		private void SendMessageToUser(String to, String msg)
		{
			var com = xmpp.GetSecureCommunicator(to);
			if (com != null && com.ConnectionEstablished)
			{
				com.SendSecuredMessage(msg);
			}
			else
			{
				Tomboy.Logger.Warn("cannot send msg to {0} because connection currently not secured.", to);
			}
		}

		private void ConnectXmpp()
		{
			authenticator.Reset();
			RetryCount++;
			if (RetryCount > MAX_RETRIES)
			{
				Logger.Warn("Stopping xmpp connect retries after " + RetryCount + " tries.");
				return;
			}
			else if (RetryCount > 1)
			{
				Logger.Warn("Xmpp Connect Retry #" + RetryCount);
			}

			if (xmpp != null)
			{
				xmpp.OnSecureMessage -= DlgOnSecureMsg;
				xmpp.OnPartnerOffline -= DlgOnPartnerOffline;
				xmpp.OnPartnerOnline -= DlgOnPartnerOnline;
				xmpp.OnConnectionEstablished -= DlgOnConnectionEstablished;
				xmpp.Close();
			}
			
			xmpp = new EasyXmpp(server, user, pw);
			if (RetryCount > 1 && RetryCount%2 == 0) 
			{
				// try fallback mode every second time
				xmpp.FallBackMode = true;
			}

			NoteEditors = new MultiNoteEditor(user + "@" + server, new TomboyNoteProvider());
			NoteEditors.OnSendMessage += DlgOnNoteEditorsEmitMsg;
			NoteEditors.OnError += DlgOnError;
			NoteEditors.SetMsgChecker(NoteEditorsCheckAllowed);
			// add friends here
			var friends = xmpp.OtherUsers;
			var storedFriends = AddressProvider.GetAll();
			foreach (var addMe in storedFriends)
			{
				friends.Add(addMe.XmppId);
			}
			xmpp.OtherUsers = friends;
			xmpp.OnSecureMessage += DlgOnSecureMsg;
			xmpp.OnPartnerOffline += DlgOnPartnerOffline;
			xmpp.OnPartnerOnline += DlgOnPartnerOnline;
			xmpp.OnConnectionEstablished += DlgOnConnectionEstablished;
			xmpp.Start();
		}

		private bool NoteEditorsCheckAllowed(String from, String noteId)
		{
			string currentPartner = null;
			if (currentCooperations.TryGetValue(noteId, out currentPartner))
			{
				if (currentPartner == from)
				{
					// we are already working with him
					return true;
				}
				else
				{
					// another user, we can only cooperate with one at a time
					var esm = NoteEditors.FindEditorStateMachine(noteId);
					if (esm != null)
					{
						var state = esm.CurrentState;
						// only allow if we are not in an "active" state
						if (state == EditorStateMachine.SmState.Editing || state == EditorStateMachine.SmState.CommitCheck
								|| state == EditorStateMachine.SmState.PreCheck2)
						{
							// but first destroy that one
							NoteEditors.DestroyEditorStateMachine(noteId);
							currentCooperations.Remove(noteId);
							return true;
						}
						else
						{
							return false;
						}
					}
					else
					{
						// it was an error that it is stored in currentCooperations
						currentCooperations.Remove(noteId);
						return true;
					}
				}
			}
			else
			{
				return NoteEditors.Provider.IsNoteSharedWith(noteId, from);
			}
		}

		/// <summary>
		/// load the currently saved config (user, server, pw)
		/// </summary>
		/// <returns></returns>
		private bool GetConfiguration()
		{
			var server = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPSERVER) as string;
			var user = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPUSER) as string;
			var pw = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPPW) as string;
			if (String.IsNullOrEmpty(server) || String.IsNullOrEmpty(user) || pw == null)
			{
				return false;
			}
			this.server = server;
			this.user = user;
			this.pw = pw;
			return true;
		}

#endregion

#region delegate-implementations

		/// <summary>
		/// the state of an editorStateMachine of any note has changed, react to it
		/// if the new state is done, we remove the editorStateMachine, to be able
		/// to accept new requests from other users etc
		/// </summary>
		/// <param name="noteId"></param>
		/// <param name="state"></param>
		private void DlgOnEditorStateChanged(String noteId, EditorStateMachine.SmState state)
		{
			if (OnLiveEditingStateChanged != null)
			{
				if (state == EditorStateMachine.SmState.Editing) {
					OnLiveEditingStateChanged(noteId, true);
				}
				else if (state == EditorStateMachine.SmState.Done
						|| state == EditorStateMachine.SmState.Error)
				{
					OnLiveEditingStateChanged(noteId, false);
				}
			}
			if (state == EditorStateMachine.SmState.Editing)
			{
				infoWindow.SetInfo(noteId, "editing", true);
			}
			else if (state == EditorStateMachine.SmState.Done)
			{
				// destroy the editorStateMachine to allow future connections
				NoteEditors.DestroyEditorStateMachine(noteId);
				currentCooperations.Remove(noteId);
				// make sure the changes are saved to disc
				((TomboyNoteProvider)NoteEditors.Provider).SaveNote(noteId);
				infoWindow.SetInfo(noteId, "finished editing", false);
			}
			else if (state == EditorStateMachine.SmState.Error)
			{
				Logger.Warn("Editing note " + noteId + " failed!");
				// destroy the editorStateMachine to allow future connections
				NoteEditors.DestroyEditorStateMachine(noteId);
				currentCooperations.Remove(noteId);
				infoWindow.SetInfo(noteId, "an Error occured!", false);
			}
		}

		/// <summary>
		/// send a message via xmpp
		/// </summary>
		/// <param name="to"></param>
		/// <param name="msg"></param>
		private void DlgOnNoteEditorsEmitMsg(String to, String msg)
		{
			SendMessageToUser(to, msg);
		}

		private void DlgOnError(String error)
   		{
			GtkUtil.ShowHintWindow(new Gtk.Label(), "MultiNoteEditor Error", error);
   		}

		private void DlgOnSecureMsg(String from, string msg)
		{
			if (msg.StartsWith("AUTH"))
			{
				// verify the remote key stuff
				var secureCom = xmpp.GetSecureCommunicator(from);
				byte[] remoteKeyData = secureCom.EncodedRemoteKey;
				authenticator.OnAuthMsgReceived(from, remoteKeyData, msg);
				return;
			}
			if (!authenticator.IsAuthenticated(from))
			{
				Logger.Error("Message from unauthenticated user " + from);
			}

			//Logger.Info("IN  " + msg);
			string concernedNote;
			var newlyCreated = NoteEditors.OnMessage(from, msg, out concernedNote);
			if (newlyCreated != null && concernedNote != null)
			{
				// a new esm was created, save it:
				currentCooperations.Add(concernedNote, from);
				newlyCreated.OnStateChanged += delegate(EditorStateMachine.SmState state)
						{
							DlgOnEditorStateChanged(concernedNote, state);
						};
			}
		}

		private void DlgOnPartnerOffline(String partnerId)
		{
			//NoteEditors.GetEditorStateMachine()
			String infoMsg = partnerId + " went offline...";

			// destroy currently active editing-sessions:
			List<String> destroyedEditors = DestroyEditorsFor(partnerId);
			if (destroyedEditors.Count > 0)
			{
				foreach (String id in destroyedEditors)
				{
					infoWindow.SetInfo(id, "User went offline", false);
				}
				infoMsg += "\nActive editing sessions were destoryed";
			}

			authenticator.RemoveUser(partnerId);
			Logger.Info(infoMsg);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="partnerId"></param>
		/// <returns>all noteIds for which editors were destroyed, if list is empty, there were none</returns>
		private List<string> DestroyEditorsFor(String partnerId)
		{
			List<string> noteIds = new List<string>();
			foreach (var elem in currentCooperations)
			{
				if (elem.Value == partnerId)
				{
					noteIds.Add(elem.Key);
				}
			}
			foreach (var noteId in noteIds)
			{
				NoteEditors.DestroyEditorStateMachine(noteId);
				currentCooperations.Remove(noteId);
			}
			return noteIds;
		}

		private void DlgOnPartnerOnline(String partnerId)
		{
			var secureCom = xmpp.GetSecureCommunicator(partnerId);
			byte[] local = secureCom.EncodedLocalKey;
			byte[] remote = secureCom.EncodedRemoteKey;
			if (local == null || remote == null)
			{
				Logger.Error("a key is not set, cannot authenticate!");
				return;
			}

			authenticator.Authenticate(partnerId, local, remote);
			Logger.Info(partnerId + " came online... authenticating...");
		}

		private void DlgOnConnectionEstablished(bool success)
		{
			if (!success)
			{
				ConnectXmpp();
			}
		}

#endregion

	}
}
