// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Gdk;
using Gtk;
using Mono.Unix;
using PrivateNotes.Infinote;
using Image = Gtk.Image;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// utility class that is able to display a list of items (all visible at the same time) as buttons vertically to select from
	/// </summary>
	public class MultiButtonCopySelector : MultiButtonSelector
	{

		static Gdk.Color colorRed = new Gdk.Color();
		static Gdk.Color colorGreen = new Gdk.Color();


		static MultiButtonCopySelector()
		{
			Gdk.Color.Parse("red", ref colorRed);
			Gdk.Color.Parse("green", ref colorGreen);
		}

		public MultiButtonCopySelector(String message, List<object> items, inputDoneObject onFinished, Gtk.Window parent)
			: base(message, items, onFinished, parent)
		{
			
		}

		/// <summary>
		/// creates the button for an item
		/// </summary>
		/// <param name="idx"></param>
		/// <param name="forObject"></param>
		/// <returns></returns>
		internal override Gtk.Button CreateButton(int idx, object forObject)
		{

			Gtk.Button btn = new Button();
			HBox box = new HBox(false, 0);
			box.BorderWidth = 2;
			
			// check if contact is online:
			Gtk.Image indicator = idx == 0 ? Icons.CopyIcon : Icons.PhoneIcon;
			box.PackStart(indicator, false, false, 2);

			Gtk.Label lbl = new Gtk.Label(forObject.ToString());
			box.PackStart(lbl, true, true, 2);

			btn.Add(box);
			btn.SetSizeRequest(300, 30);
			btn.Clicked += delegate(object sender, EventArgs e)
			{
				ButtonClicked(idx);
			};
			return btn;
		}

		/// <summary>
		/// user clicked a button
		/// </summary>
		/// <param name="idx"></param>
		void ButtonClicked(int idx)
		{
			onOk(true, allItems[idx]);
			Hide();
			Destroy();
		}

	}

}
