using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Gdk;
using Gtk;
using Mono.Unix;
using PrivateNotes.Infinote;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// utility class that is able to display a list of items (all visible at the same time) as buttons vertically to select from
	/// </summary>
	public class MultiButtonPartnerSelector : MultiButtonSelector
	{

		static Gdk.Color colorRed = new Gdk.Color();
		static Gdk.Color colorGreen = new Gdk.Color();

		private List<String> onlinePartners;

		static MultiButtonPartnerSelector()
		{
			Gdk.Color.Parse("red", ref colorRed);
			Gdk.Color.Parse("green", ref colorGreen);
		}

		public MultiButtonPartnerSelector(String message, List<object> items, inputDoneObject onFinished) : base(message, items, onFinished)
		{
			
		}

		internal override void Init()
		{
			base.Init();
			onlinePartners = Communicator.Instance.GetOnlinePartnerIds();
		}

		/// <summary>
		/// creates the button for an item
		/// </summary>
		/// <param name="idx"></param>
		/// <param name="forObject"></param>
		/// <returns></returns>
		internal override Gtk.Button CreateButton(int idx, object forObject)
		{
			XmppEntry addresItem = forObject as XmppEntry;
			bool online = (addresItem == null) ? false : onlinePartners.Contains(addresItem.XmppId);

			Gtk.Button btn = new Button();
			HBox box = new HBox(false, 0);
			box.BorderWidth = 2;
			
			// check if contact is online:
			Gtk.Label indicator = new Label("\u25CF");
			indicator.ModifyFg(StateType.Normal, online ? colorGreen : colorRed);
			indicator.ModifyFg(StateType.Prelight, online ? colorGreen : colorRed);
			box.PackStart(indicator, false, false, 2);

			Gtk.Label lbl = new Gtk.Label(forObject.ToString());
			box.PackStart(lbl, true, true, 2);

			btn.Add(box);
			btn.SetSizeRequest(300, 25);
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
