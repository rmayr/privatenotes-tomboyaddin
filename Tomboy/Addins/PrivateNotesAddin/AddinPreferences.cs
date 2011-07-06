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

		// match-label texts
		public const string MATCH_TEXT = "<markup><span foreground=\"green\">match</span></markup>";
		public const string MATCH_NOT_TEXT = "<span foreground=\"red\">don't match</span>";

		public const string NOTESHARE_URL_PREFIX = "note://tomboyshare/";

		// gui elements

		/// <summary>
		/// creates the preferences-view (mostly it shows information, you can't actually set any preferences in this view)
		/// </summary>
		public AddinPreferences()
			: base(false, 12)
		{
			Gtk.VBox container = new Gtk.VBox(false, 12);

			PackStart(container);
			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("<span size=\"x-large\">Info</span>")));
			container.PackStart(new Gtk.Label(Catalog.GetString("You can configure the sync-settings in the \"Synchronization\" tab.")));
			container.PackStart(new Gtk.Label());
			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("For more information, please visit:")));
			container.PackStart(new Gtk.LinkButton("http://privatenotes.dyndns-server.com/", "http://privatenotes.dyndns-server.com/"));

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
				// dummy parent
				Gtk.Widget wid = new Gtk.Label();
				GtkUtil.ShowHintWindow(wid, Catalog.GetString("Protocol registration"),
					Catalog.GetString("The protocol registration failed, sorry."));
			}
		}

	}
}
