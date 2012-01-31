// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
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
