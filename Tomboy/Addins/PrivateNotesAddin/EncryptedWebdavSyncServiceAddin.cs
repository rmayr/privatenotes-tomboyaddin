//#define USE_LOCAL_TEST
// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 

using System;
using System.IO;

using Gtk;
using Mono.Unix;
using PrivateNotes.Infinote;
using Tomboy;
using Tomboy.PrivateNotes;
using Tomboy.PrivateNotes.Crypto;

/*! \mainpage PrivateNotes TomboyAddin Start Page
 *
 * \section intro_sec Introduction
 *
 * Welcome to the PrivateNotes TomboyAddin documentation. <br/>
 * 
 * Here you find the extracted code-documentation. To get more information/stuff go
 * to <a href="http://privatenotes.dyndns-server.com/wiki/">our project website
 * privatenotes.dyndns-server.com/wiki/</a>.
 *
 * \section start_sec Getting started
 *
 * If you want to get started reading some documentation a good starting point might
 * be Tomboy.PrivateNotes.EncryptedWebdavSyncServer or more specifically the "Member List"
 * on that page.
 *  
 */
namespace Tomboy.Sync
{

	/// <summary>
	/// the actual addin class.
	/// responsible for creating the sync server object and handling the
	/// sync-preferences gui (sync tab in tomboy prefs)
	/// </summary>
	public class EncryptedWebdavSyncServiceAddin : SyncServiceAddin
	{
		/// <summary>
		/// we misuse this to start up our XMPP Communicator also
		/// because we have to do this as soon as possible, and this seems to
		/// get called always on startup, so it's a good fit
		/// </summary>
		public EncryptedWebdavSyncServiceAddin()
		{
			Logger.Info("Initializing PrivateNotes Sharing Addin");
			try
			{
				if ((!Communicator.Instance.IsActive()) && Communicator.Instance.IsConfigured())
				{
					Communicator.Instance.Connect();
				}
			}
			catch (Exception e)
			{
				Logger.Warn("cannot initialize Communicator ", e);
			}
		}

		#region GUI ELEMENTS
		//private FileChooserButton pathButton;
		private Gtk.RadioButton rbt_storePw;
		private Gtk.RadioButton rbt_alwaysAsk;
		private Gtk.Entry stored_pw;
		private Gtk.Entry stored_pw2; // confirm
		private Gtk.Label match_label;
		private Gtk.CheckButton check_ssl;

		private Gtk.Entry server_path;
		private Gtk.Entry server_user;
		private Gtk.Entry server_pass;

		private Gtk.Entry xmpp_user_at_server;
		private Gtk.Entry xmpp_pw;
		#endregion

		private bool initialized = false;

		/// <summary>
		/// Called as soon as Tomboy needs to do anything with the service
		/// </summary>
		public override void Initialize ()
		{
			initialized = true;
			GpgConfigUtility.ConfigureIfNecessary(null);
			Statistics.Init();
			Logger.Warn("INITING SYNC SERVICE ADDIN!");
			string syncServiceId =
					Preferences.Get(Preferences.SYNC_SELECTED_SERVICE_ADDIN) as String;
			if (syncServiceId == this.Id)
			{
				// we are the configured addin! *party*
				if (AddinPreferences.IsFirstRun(true))
				{
					GtkUtil.ShowFirstLaunchWindow();
				}
			}
		}

		public override void Shutdown ()
		{
			// Do nothing for now
		}

		public override bool Initialized {
			get {
				return initialized;
			}
		}


		/// <summary>
		/// Creates a SyncServer instance that the SyncManager can use to
		/// synchronize with this service.	This method is called during
		/// every synchronization process.	If the same SyncServer object
		/// is returned here, it should be reset as if it were new.
		/// </summary>
		public override SyncServer CreateSyncServer ()
		{
			Statistics.Instance.StartSyncRun();
			SyncServer server = null;

			String password;
			WebDAVInterface webdavserver;


			if (GetConfigSettings(out password, out webdavserver))
			{
				try
				{
					server = new EncryptedWebdavSyncServer(Services.NativeApplication.CacheDirectory, Util.GetBytes(password), webdavserver);
				}
				catch (PasswordException)
				{
					// Display window with hint that the pw is wrong
					GtkUtil.ShowHintWindow(Tomboy.SyncDialog, "Wrong Password", "The password you provided was wrong.");
					throw;
				}
				catch (FormatException)
				{
					// Display window with hint
					GtkUtil.ShowHintWindow(Tomboy.SyncDialog, "Encryption Error", "The encrypted files seem to be corrupted.");
					throw;
				}
				catch (WebDavException wde)
				{
					Exception inner = wde.InnerException;
					for (int i = 0; i < 10 && (inner != null && inner.InnerException != null); i++) // max 10
						inner = inner.InnerException;

					GtkUtil.ShowHintWindow(Tomboy.SyncDialog, "WebDav Error", "Error while communicating with server:\n" + (inner==null?wde.Message:inner.Message) + "\nPlease try again later.");
					throw;
				}
			} else {
				throw new InvalidOperationException ("FileSystemSyncServiceAddin.CreateSyncServer () called without being configured");
			}

			return server;
		}

		public override void PostSyncCleanup ()
		{
			Statistics.Instance.FinishSyncRun(null);
			Util.TryDeleteDirectory(Path.Combine(Services.NativeApplication.CacheDirectory, "sync_temp"));
			Util.TryDeleteDirectory(Path.Combine(Services.NativeApplication.CacheDirectory, "gp"));
			Util.TryDeleteDirectory(Path.Combine(Services.NativeApplication.CacheDirectory, "sharedSync"));
		}

		/// <summary>
		/// Creates a Gtk.Widget that's used to configure the service.	This
		/// will be used in the Synchronization Preferences.	Preferences should
		/// not automatically be saved by a GConf Property Editor.	Preferences
		/// should be saved when SaveConfiguration () is called.
		/// <param name="requiredPrefChanged">this has to be assigned to any handles that indicate change, only if this handler is invoked, the "save" button will be activated</param>
		/// </summary>
		public override Gtk.Widget CreatePreferencesControl (EventHandler requiredPrefChanged)
		{
			if (AddinPreferences.IsFirstRun(true))
			{
				GtkUtil.ShowFirstLaunchWindow();
			}
			Gtk.VBox container = new Gtk.VBox(false, 0);

			// small extra container for help btn
			Gtk.HBox c2 = new Gtk.HBox(false, 0);
			c2.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("<span weight='bold'>Server Settings:</span>")));

			LinkButton btn = new Gtk.LinkButton(AddinPreferences.PROJECT_HELP,
									Catalog.GetString("Need Help?"));
			// open link manually on click event on windows (because link buttons don't work there somehow
			btn.Clicked += delegate(object sender, EventArgs e)
				{
					if (Util.IsWindows())
					{
						System.Diagnostics.Process.Start(AddinPreferences.PROJECT_HELP);
					}
				};
			c2.PackStart(btn, false, false, 0);
			container.PackStart(c2);

			SetupGuiServerRelated(container, 4, requiredPrefChanged);

			container.PackStart(new Gtk.Label());
			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("<span weight='bold'>Encryption Settings:</span>")));
			SetupGuiEncryptionRelated(container, 4, requiredPrefChanged);

			container.PackStart(new Gtk.Label());
			container.PackStart(GtkUtil.newMarkupLabel(Catalog.GetString("<span weight='bold'>Xmpp Settings:</span>  <span size='small'>(optional)</span>")));
			SetupGuiXmppRelated(container, 4, requiredPrefChanged);

			container.ShowAll();
			return container;
		}

		/// <summary>
		/// The Addin should verify and check the connection to the service
		/// when this is called.	If verification and connection is successful,
		/// the addin should save the configuration and return true.
		/// </summary>
		public override bool SaveConfiguration ()
		{
			string serverPath = server_path.Text.Trim();

			if (serverPath.Trim().Equals(String.Empty)) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("The serverpath is empty");
				throw new TomboySyncException (Catalog.GetString ("Folder path field is empty."));
			}

			if (!stored_pw.Text.Equals(stored_pw2.Text)) {
				Logger.Debug ("Passwords must match!");
				throw new TomboySyncException (Catalog.GetString ("Passwords must match!"));
			}

			// actually save if everything was ok
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME, (bool)!rbt_storePw.Active);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH, (string)serverPath);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER, (string)server_user.Text);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS, (string)server_pass.Text);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT, (bool)check_ssl.Active);

			if (rbt_storePw.Active)
				storePassword(stored_pw.Text);
			else
			{
				// don't store, delete it from prefs and gui (to make it clear to the user
				stored_pw.Text = string.Empty;
				stored_pw2.Text = string.Empty;
				storePassword(string.Empty);
			}

			String xmppUserServer = xmpp_user_at_server.Text.Trim();
			String xmppPw = xmpp_pw.Text.Trim();
            
			// separate user/server
			String xUser, xServer;
			if (Util.SeparateMail(xmppUserServer, out xUser, out xServer))
			{
				string oldServer = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPSERVER) as string;
				string oldUser = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPUSER) as string;
				string oldPassword = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPPW) as string;
				Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPSERVER, xServer);
				Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPUSER, xUser);
				Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPPW, xmppPw);

				if (oldPassword != xmppPw || oldUser != xUser || oldServer != xServer)
				{
					Logger.Info("Xmpp inforamtion changed, reconnecting service...");
					try
					{
						Communicator.Instance.Connect();
					}
					catch (Exception e)
					{
						Logger.Warn("cannot initialize Communicator ", e);
					}
				}
			}
			else
			{
				Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPSERVER, string.Empty);
				Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPPW, xmppPw);
			}

			return true;
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		public override void ResetConfiguration ()
		{
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME, "true");
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT, "true");
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPSERVER, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPUSER, string.Empty);
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_XMPPPW, string.Empty);
			storePassword(" ");
		}

		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string syncPath = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH) as String;

				if (syncPath != null && syncPath.Trim() != string.Empty) {
					return true;
				}

				return false;
			}
		}

		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public override string Name
		{
			get {
				return Mono.Unix.Catalog.GetString ("PrivateNotes Synchronization");
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.	This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get {
				return "securewebdav";
			}
		}

		/// <summary>
		/// Returns true if the addin has all the supporting libraries installed
		/// on the machine or false if the proper environment is not available.
		/// If false, the preferences dialog will still call
		/// CreatePreferencesControl () when the service is selected.	It's up
		/// to the addin to present the user with what they should install/do so
		/// IsSupported will be true.
		/// </summary>
		public override bool IsSupported
		{
			get {
				return true;
			}
		}

		#region Private Methods

		/// <summary>
		/// store the password with the according method
		/// </summary>
		/// <param name="_pw"></param>
		private void storePassword(String _pw)
		{
#if WIN32 && DPAPI
				DPAPIUtil.storePassword(_pw);
#else
			Preferences.Set(AddinPreferences.SYNC_PRIVATENOTES_PASSWORD, _pw);
#endif
		}

		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string _password, out WebDAVInterface _webdav)
		{
			_password = null;
			_webdav = null;

			object ask = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME);

			if (ask == null)
				return false;

			if (((bool)ask == false))
			{
#if WIN32 && DPAPI
				object pw = DPAPIUtil.getPassword();
#else
				object pw = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_PASSWORD);
#endif
				if (pw != null)
				{
					_password = Convert.ToString(pw); // quick fix -> a num-only pw is returned as an int o.O
				}
			}

			if (_password == null)
			{
				// ask for password
				var entryWindow = new PrivateNotes.PasswordEntry();

				_password = entryWindow.getPassword();
			}

#if USE_LOCAL_TEST
			 _webdav = new WebDAVInterface("http://localhost", "/webdav/notes", "wampp", "xampp", false);
#else
			Uri serverUri = new Uri(Convert.ToString(Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH)));

			String serverHost = serverUri.GetLeftPart(UriPartial.Authority);
			String serverBasePath = serverUri.AbsolutePath;

			bool checkSslCertificates = true;
			object checksslobj = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT);
			if (checksslobj != null && (checksslobj.Equals(false) || checksslobj.Equals("false")))
				checkSslCertificates = false;

			string serverUser = (string)Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER);
			string serverPass = (string)Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS);

			Logger.Debug("will user server: " + serverHost + " path: " + serverBasePath);
			//Logger.Debug("creating server with user " + serverUser + " pass: " + serverPass);

			_webdav = new WebDAVInterface(serverHost, serverBasePath,
				serverUser,
				serverPass,
				checkSslCertificates);
#endif

			if (_webdav != null && _password != null)
				return true;

			return false;
		}
		#endregion // Private Methods

		#region Gui Setup Methods

		/// <summary>
		/// setup fields like: store password:yes/no and the actual password entry,
		/// if it should be stored
		/// </summary>
		/// <param name="insertTo"></param>
		/// <param name="defaultSpacing"></param>
		void SetupGuiEncryptionRelated(Gtk.Box insertTo, int defaultSpacing, EventHandler requiredPrefChanged)
		{
			Gtk.HBox customBox = new Gtk.HBox(false, defaultSpacing);
			insertTo.PackStart(customBox);
			rbt_storePw = new Gtk.RadioButton(Catalog.GetString("_Store password"));
			customBox.PackStart(rbt_storePw);

			customBox = new Gtk.HBox(false, defaultSpacing);
			insertTo.PackStart(customBox);

			//	--- Password Boxes --- 
#if WIN32 && DPAPI
			String pw = DPAPIUtil.getPassword();
#else
			String pw = Convert.ToString(Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_PASSWORD));
#endif
			pw = (pw == null) ? "" : pw;
			Gtk.VBox pwbox = new Gtk.VBox(false, defaultSpacing);
			Gtk.HBox superbox = new Gtk.HBox(false, defaultSpacing);
			superbox.PackStart(new Gtk.Alignment(0, 0, 200, 0)); // spacer
			superbox.PackStart(pwbox);
			customBox.PackStart(superbox);

			stored_pw = new Gtk.Entry();
			// set password style:
			stored_pw.InvisibleChar = '*';
			stored_pw.Visibility = false;
			stored_pw.Text = pw;
			pwbox.PackStart(stored_pw);

			stored_pw2 = new Gtk.Entry();
			// set password style:
			stored_pw2.InvisibleChar = '*';
			stored_pw2.Visibility = false;
			stored_pw2.Text = pw;
			pwbox.PackStart(stored_pw2);

			match_label = new Gtk.Label();
			match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_TEXT);
			pwbox.PackStart(match_label);

			customBox = new Gtk.HBox(false, defaultSpacing);
			insertTo.PackStart(customBox);

			// give the first rbt here to link the 2
			rbt_alwaysAsk = new Gtk.RadioButton(rbt_storePw, Catalog.GetString("_Always ask for password"));
			customBox.PackStart(rbt_alwaysAsk);

			
			// assign event-listener
			rbt_storePw.Toggled += PasswordMethodChanged;
			rbt_storePw.Toggled += requiredPrefChanged;

			// init with values from preferences
			object value = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_ASKEVERYTIME);
			if (value == null || value.Equals(false))
			{
				rbt_storePw.Active = true;
			}
			else
			{
				rbt_alwaysAsk.Active = true;
			}

			// assign event-listeners
			stored_pw.Changed += PasswordChanged;
			stored_pw2.Changed += PasswordChanged;
			stored_pw.Changed += requiredPrefChanged;
			stored_pw2.Changed += requiredPrefChanged;
		}

		/// <summary>
		/// server gui stuff:
		/// server path
		/// server username + password
		/// check server ssl certificate yes/no
		/// </summary>
		/// <param name="insertTo"></param>
		/// <param name="defaultSpacing"></param>
		void SetupGuiServerRelated(Gtk.Box insertTo, int defaultSpacing, EventHandler requiredPrefChanged)
		{
			Gtk.Table customBox = new Gtk.Table(3, 2, false);

			// somehow you can't change the default spacing or set it for all rows
			for (int i = 0; i < 3; i++)
				customBox.SetRowSpacing((uint)i, (uint)defaultSpacing);

			// insert the labels
			customBox.Attach(new Gtk.Label(Catalog.GetString("Server path:")), 0, 1, 0, 1);
			customBox.Attach(new Gtk.Label(Catalog.GetString("Username:")), 0, 1, 1, 2);
			customBox.Attach(new Gtk.Label(Catalog.GetString("Password:")), 0, 1, 2, 3);

			insertTo.PackStart(customBox);
			server_path = new Gtk.Entry();
			customBox.Attach(server_path, 1, 2, 0, 1);
			string serverPath = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH) as String;
			if (String.IsNullOrEmpty(serverPath))
			{
				serverPath = "https://193.170.124.44/webdav2/";
			}
			server_path.Text = serverPath;
			server_path.Changed += requiredPrefChanged;
			// NO EDITOR! because we only save when "SaveConfiguration" is called
			//IPropertyEditor serverEditor = Services.Factory.CreatePropertyEditorEntry(
			//	AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH, server_path);
			//serverEditor.Setup();

			server_user = new Gtk.Entry();
			customBox.Attach(server_user, 1, 2, 1, 2);
			string serverUser = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER) as String;
			server_user.Text = serverUser;
			server_user.Changed += requiredPrefChanged;
			// NO EDITOR! because we only save when "SaveConfiguration" is called
			//IPropertyEditor userEditor = Services.Factory.CreatePropertyEditorEntry(
			// AddinPreferences.SYNC_PRIVATENOTES_SERVERUSER, server_user);
			//userEditor.Setup();

			server_pass = new Gtk.Entry();
			server_pass.InvisibleChar = '*';
			server_pass.Visibility = false;
			customBox.Attach(server_pass, 1, 2, 2, 3);
			string serverpass = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS) as String;
			server_pass.Text = serverpass;
			server_pass.Changed += requiredPrefChanged;
			// NO EDITOR! because we only save when "SaveConfiguration" is called
			//IPropertyEditor passEditor = Services.Factory.CreatePropertyEditorEntry(
			// AddinPreferences.SYNC_PRIVATENOTES_SERVERPASS, server_pass);
			//passEditor.Setup();

			check_ssl = new Gtk.CheckButton(Catalog.GetString("Check servers SSL certificate"));
			insertTo.PackStart(check_ssl);

			// set up check-ssl certificate stuff
			object value = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERCHECKSSLCERT);
			if (value != null && value.Equals(true))
				check_ssl.Active = true;

			check_ssl.Activated += requiredPrefChanged;

		}

		/// <summary>
		/// setup fields: xmpp-user, xmpp-password
		/// </summary>
		/// <param name="insertTo"></param>
		/// <param name="defaultSpacing"></param>
		void SetupGuiXmppRelated(Gtk.Box insertTo, int defaultSpacing, EventHandler requiredPrefChanged)
		{
			Gtk.Table customBox = new Gtk.Table(2, 2, false);

			// somehow you can't change the default spacing or set it for all rows
			for (int i = 0; i < 2; i++)
				customBox.SetRowSpacing((uint)i, (uint)defaultSpacing);

			// insert the labels
			//customBox.Attach(new Gtk.Label(Catalog.GetString("Server path:")), 0, 1, 0, 1);
			customBox.Attach(new Gtk.Label(Catalog.GetString("User:")), 0, 1, 0, 1);
			customBox.Attach(new Gtk.Label(Catalog.GetString("Password:")), 0, 1, 1, 2);

			insertTo.PackStart(customBox);

			xmpp_user_at_server = new Gtk.Entry();
			customBox.Attach(xmpp_user_at_server, 1, 2, 0, 1);
			string server = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPSERVER) as String;
			string user = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPUSER) as String;
			xmpp_user_at_server.Text = user + "@" + server;
            xmpp_user_at_server.Changed += requiredPrefChanged;

			xmpp_pw = new Gtk.Entry();
			customBox.Attach(xmpp_pw, 1, 2, 1, 2);
			string pw = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_XMPPPW) as String;
			xmpp_pw.InvisibleChar = '*';
			xmpp_pw.Visibility = false;
			xmpp_pw.Text = pw;
            xmpp_pw.Changed += requiredPrefChanged;
		}

		#endregion

		#region Gui Callbacks

		 /// <summary>
		/// radiobutton changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void PasswordMethodChanged(object sender, EventArgs args)
		{
			bool storedPwEnabled = rbt_storePw.Active;

			stored_pw.Sensitive = storedPwEnabled;
			stored_pw2.Sensitive = storedPwEnabled;
			match_label.Sensitive = storedPwEnabled;
		}

		/// <summary>
		/// entered a new password
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void PasswordChanged(object sender, EventArgs args)
		{
			if (stored_pw.Text.Equals(stored_pw2.Text))
			{
				match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_TEXT);
			}
			else
			{
				match_label.Markup = Catalog.GetString(AddinPreferences.MATCH_NOT_TEXT);
			}
		}


		#endregion
	}
}
