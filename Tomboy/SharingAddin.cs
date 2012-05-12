using System;

namespace Tomboy
{
	/// <summary>
	/// A SharingAddin provides an addin (typically a sync addin) with the means
	/// to import additional notes which are shared with the user
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
