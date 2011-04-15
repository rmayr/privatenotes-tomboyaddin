using System;

namespace Tomboy
{
	/// <summary>
	/// A SyncServiceAddin provides Tomboy Note Synchronization to a
	/// service such as WebDav, SSH, FTP, etc.
	/// <summary>
	public abstract class SharingAddin : ApplicationAddin
	{

		public abstract bool ImportShare(String info);

		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public abstract string Name
		{
			get;
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public abstract string Id
		{
			get;
		}

	}
}
