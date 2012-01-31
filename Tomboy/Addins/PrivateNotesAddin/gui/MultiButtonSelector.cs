using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Gtk;
using Mono.Unix;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// utility class that is able to display a list of items (all visible at the same time) as buttons vertically to select from
	/// </summary>
	public class MultiButtonSelector : Gtk.Dialog
	{
		internal List<object> allItems;
		internal inputDoneObject onOk;

		/// <summary>
		/// creates the selector window and displays it automatically
		/// </summary>
		/// <param name="message"></param>
		/// <param name="onFinished">callback function when input is done</param>
		public MultiButtonSelector(String message, List<object> items, inputDoneObject onFinished)
		{
			// onOk must not be null
			onOk = onFinished ?? delegate(bool ok, object obj) {/*just ignore*/};
			allItems = items;
			Title = Catalog.GetString("Selector");
			Init();
			Gtk.VBox box = new Gtk.VBox(false, 6);

			box.PackStart(new Gtk.Label(Catalog.GetString(message)), true, true, 6);

			for (int i = 0; i < items.Count; i++)
			{
				int idx = i;
				Gtk.Button btn = CreateButton(i, items[i]);
				box.PackStart(btn);
			}

			Response += OnResponse;

			box.ShowAll();
			this.VBox.PackStart(box);

			// show() must happen on ui thread
			Gtk.Application.Invoke(RunInUiThread);
		}

		internal virtual void Init()
		{
		}

		/// <summary>
		/// creates a button to click on
		/// this can be overridden to place some custom buttons in there
		/// </summary>
		/// <param name="idx"></param>
		/// <param name="forObject"></param>
		/// <returns></returns>
		internal virtual Gtk.Button CreateButton(int idx, object forObject)
		{
			Gtk.Button btn = new Button();
			Gtk.Label lbl = new Gtk.Label(forObject.ToString());
			btn.Add(lbl);
			btn.SetSizeRequest(300, 25);
			btn.Clicked += delegate(object sender, EventArgs e)
			{
				ButtonClicked(idx);
			};
			return btn;
		}

		void ButtonClicked(int idx)
		{
			onOk(true, allItems[idx]);
			Hide();
			Destroy();
		}

		/// <summary>
		/// used to show the dialog via the UI-Thread
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="ea"></param>
		public void RunInUiThread(object sender, EventArgs ea)
		{
			Present();
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="o"></param>
		/// <param name="args"></param>
		public void OnAction(object o, Gtk.AccelActivateArgs args)
		{
			// action
			onOk(false, null);
			Hide();
			Destroy();
		}

		// forward other events
		public void OnResponse(object o, Gtk.ResponseArgs args) {
			if (args.ResponseId == Gtk.ResponseType.DeleteEvent)
			{
				// nothing
			}
			else
			{
				OnAction(o, null);
			}
		}

	}
}
