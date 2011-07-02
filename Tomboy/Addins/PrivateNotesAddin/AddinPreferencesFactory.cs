using System;
using Tomboy;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// factory calss as needed by tomboy (AddinPreferenceFactory) to create a preferences gui
	/// </summary>
	public class AddinPreferencesFactory : AddinPreferenceFactory
	{
		public override Gtk.Widget CreatePreferenceWidget ()
		{
			return new AddinPreferences();
		}
	}
}
