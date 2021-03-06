﻿// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
using System;
using System.Collections.Generic;
using System.Text;
using PrivateNotes;
using PrivateNotes.Infinote;

namespace Tomboy.PrivateNotes
{
#if !NOSHARES
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
			return ShareNoteAddin.ImportShareFromShareUrl(info);
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
			initialized = true;
		}

		/// <summary>
		/// Called just before Tomboy shuts down for good.
		/// </summary>
		public override void Shutdown()
		{

		}

		private bool initialized = false;

		/// <summary>
		/// Return true if the addin is initialized
		/// </summary>
		public override bool Initialized { get { return initialized; }}
	}
#endif
}
