// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
using System;
using Tomboy;
using Mono.Unix;

namespace Tomboy.PrivateNotes
{
	/// <summary>
	/// although this class doesn't actually set the preferences, the constants for the names under which the prefs are
	/// stored are defined here (because the name AddinPreferences is a good hint to search for this stuff here).
	/// </summary>
	public class AddinPreferences : Gtk.VBox
	{
		// PREFERENCES CONSTANTS FOR THIS PLUGIN
		public const string SYNC_PRIVATENOTES_PASSWORD = "/apps/tomboy/sync/private_notes/password";
		public const string SYNC_PRIVATENOTES_ASKEVERYTIME = "/apps/tomboy/sync/private_notes/ask_for_password";

		public const string SYNC_PRIVATENOTES_SERVERPATH = "/apps/tomboy/sync/private_notes/server";
		public const string SYNC_PRIVATENOTES_SERVERUSER = "/apps/tomboy/sync/private_notes/server_user";
		public const string SYNC_PRIVATENOTES_SERVERPASS = "/apps/tomboy/sync/private_notes/server_password";
		public const string SYNC_PRIVATENOTES_SERVERCHECKSSLCERT = "/apps/tomboy/sync/private_notes/server_checkcert";

		public const string SYNC_PRIVATENOTES_SHARE_GPG = "/apps/tomboy/sync/private_notes/share_gpg";

		public const string SYNC_PRIVATENOTES_XMPPSERVER = "/apps/tomboy/sync/private_notes/xmpp_server";
		public const string SYNC_PRIVATENOTES_XMPPUSER = "/apps/tomboy/sync/private_notes/xmpp_user";
		public const string SYNC_PRIVATENOTES_XMPPPW = "/apps/tomboy/sync/private_notes/xmpp_password";

		public const string SYNC_PRIVATENOTES_RANBEFORE = "/apps/tomboy/sync/private_notes/has_been_run";

		// match-label texts
		public const string MATCH_TEXT = "<markup><span foreground=\"green\">match</span></markup>";
		public const string MATCH_NOT_TEXT = "<span foreground=\"red\">don't match</span>";

		public const string NOTESHARE_URL_PREFIX = "note://tomboyshare/";
		// url used by PrivateNotes on Android to get sync configuration (webdav address appended to it)
		public const string NOTESYNCCONFIG_URL_PREFIX = "note://synccfg/";

		public const string PROJECT_URL = "http://privatenotes.dyndns-server.com/";
		public const string PROJECT_HELP = "http://tiny.cc/privatenotes#page2";

		// gui elements

		/// <summary>
		/// creates the preferences-view (mostly it shows information, you can't actually set any preferences in this view)
		/// </summary>
		public AddinPreferences()
			: base(false, 12)
		{
			Gtk.VBox container = new Gtk.VBox(false, 6);

			PackStart(container);
			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("<span size=\"x-large\">Info</span>")));
			container.PackStart(new Gtk.Label(Catalog.GetString("You can configure the sync-settings in the \"Synchronization\" tab.")));
			container.PackStart(new Gtk.Label());
			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("For more information please visit:")));
			Gtk.LinkButton btn = new Gtk.LinkButton(PROJECT_URL,
													PROJECT_URL);
			// manually catch this event, because linkbutton doesn't work somehow...
			btn.Clicked += delegate(object sender, EventArgs e)
				{ System.Diagnostics.Process.Start(PROJECT_URL); };
			container.PackStart(btn);

			container.PackStart(new Gtk.Label(Catalog.GetString("If you need any help setting up PrivateNotes go to:")));
			btn = new Gtk.LinkButton(PROJECT_HELP,
									PROJECT_HELP);
			btn.Clicked += delegate(object sender, EventArgs e)
				{ System.Diagnostics.Process.Start(PROJECT_HELP); };
			container.PackStart(btn);


			Gtk.Button btnGpg = new Gtk.Button(container);
			btnGpg.Label = Catalog.GetString("Configure GPG utility");
			btnGpg.Pressed += OnConfGpgActivated;
			
			Gtk.Button btnRegister = new Gtk.Button(container);
			btnRegister.Label = Catalog.GetString("Register for note:// protocol");
			btnRegister.Pressed += OnRegisterProtocolActivated;

			container.PackStart(new Gtk.Label());
			container.PackStart(btnGpg);
			container.PackStart(btnRegister);

			ShowAll();
		}

		/// <summary>
		/// callback for when the user wants to manually configure the path of the pgp utility
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void OnConfGpgActivated(object sender, EventArgs args)
		{
			// TODO how to get parent window here? (instead of null)
			GpgConfigUtility.ConfigureGpg(false, null);
		}
			
		/// <summary>
		/// callback for when the user wants to register for the note:// protocol
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void OnRegisterProtocolActivated(object sender, EventArgs args)
		{
			// TODO how to get parent window here? (instead of null)
			if (!NoteProtocolRegisterUtility.Register())
			{
				GtkUtil.ShowHintWindow(Catalog.GetString("Protocol registration"),
					Catalog.GetString("The protocol registration failed, sorry."));
			}
		}

		/// <summary>
		/// check if addin run the first time
		/// </summary>
		/// <param name="registerAsFirstRun">if true, this will be marked as the first run, so following calls will return false</param>
		/// <returns></returns>
		public static bool IsFirstRun(bool registerAsFirstRun)
		{
			object isFirstRun =
					Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_RANBEFORE);
			bool isFirst = true;
			if (isFirstRun != null)
			{
				if (!(isFirstRun is bool))
				{
					// no valid value
					isFirst = true;
				}
				else
				{
					// negate caues we store "ran before" but return "is first run"
					isFirst = !(bool)isFirstRun;
				}
			}
			if (isFirst && registerAsFirstRun)
			{
				Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_RANBEFORE, true);
			}
			return isFirst;
		}

	}
}
