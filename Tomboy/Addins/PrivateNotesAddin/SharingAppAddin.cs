using System;
using System.Collections.Generic;
using System.Text;

namespace Tomboy.PrivateNotes
{
	/// <summary>
	/// subclass SharingAddin to be informed by Tomboy when a new note should
	/// be added from a share
	/// </summary>
	public class SharingAppAddin : SharingAddin
	{
		/// <summary>
		/// a share should be imported
		/// </summary>
		/// <param name="info">the share-info string (typically the path, might be prefixed with something (note://share/ or so)</param>
		/// <returns>true if imported</returns>
		public override bool ImportShare(String info)
		{
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
				}
			}
			else
			{
				Logger.Warn("we should import {0}, which isnt a valid share-url,"
				+"they look like this: {1}SOMETHING", info, AddinPreferences.NOTESHARE_URL_PREFIX);
			}
			return success;
		}

		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public override string Name
		{
			get
			{
				return "PrivateNotesSharing";
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get
			{
				return "PrivateNotesSharingAddin";
			}
		}

		/// <summary>
		/// Called when Tomboy has started up and is nearly 100% initialized.
		/// </summary>
		public override void Initialize()
		{
			Logger.Info("Initializing PrivateNotes Sharing Addin");
		}

		/// <summary>
		/// Called just before Tomboy shuts down for good.
		/// </summary>
		public override void Shutdown()
		{

		}

		/// <summary>
		/// Return true if the addin is initialized
		/// </summary>
		public override bool Initialized
		{
			get
			{
				return true;
			}
		}

	}
}
