using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Mono.Unix;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// utility class that is able to display a item selection dialog
	/// </summary>
	public class ItemSelector : Gtk.Dialog
	{
		Gtk.ComboBoxEntry itemsComboBox;
		List<String> allItems;
		inputDone onOk;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="message"></param>
		/// <param name="defaultvalue"></param>
		/// <param name="regexmatch">if != null, input will only be accepted if it matches the regex</param>
		/// <param name="onFinished">callback function when input is done</param>
		public ItemSelector(String message, List<String> items, inputDone onFinished)
		{			
			onOk = onFinished;
			allItems = items;
			Title = Catalog.GetString("Selector");
			Gtk.VBox box = new Gtk.VBox(false, 6);

			box.PackStart(new Gtk.Label(Catalog.GetString(message)), true, true, 6);

			itemsComboBox = new Gtk.ComboBoxEntry(allItems.ToArray());

			box.PackStart(itemsComboBox);
			itemsComboBox.Changed += new EventHandler(itemsComboBox_Changed);

			Gtk.Button button = (Gtk.Button)AddButton(Gtk.Stock.Ok, Gtk.ResponseType.Ok);
			button.CanDefault = true;
			//button.Show();
			box.PackStart(button);
			//this.VBox.PackStart(button);

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup();
			AddAccelGroup(accel_group);

			button.AddAccelerator("activate",
														 accel_group,
														 (uint)Gdk.Key.Return,
														 0,
														 0);

			AddActionWidget(button, Gtk.ResponseType.Ok);
			DefaultResponse = Gtk.ResponseType.Ok;

			accel_group.AccelActivate += OnAction;
			Response += OnResponse;

			DeleteEvent += new Gtk.DeleteEventHandler(TextInput_DeleteEvent);

			box.ShowAll();
			this.VBox.PackStart(box);

			// show() must happen on ui thread
			Gtk.Application.Invoke(RunInUiThread);
		}

		bool isChanging = false;
		String lastText = "";
		void itemsComboBox_Changed(object sender, EventArgs e)
		{
			// prevents recursive call if we change the text-value in here
			if (isChanging)
				return;
			isChanging = true;
			String text = itemsComboBox.Entry.Text;
			if (text.Equals(lastText))
			{
				isChanging = false;
				return;
			}
			lastText = text.ToLower(); // to make a case insensitive search
			List<String> matches = allItems.FindAll(delegate(String s) { return s.ToLower().Contains(text); });

			// quick and VERY VERY DIRTY way of doing it
			int count = itemsComboBox.Model.IterNChildren();
			for (int i = 0; i < count; i++)
				itemsComboBox.RemoveText(0);
			foreach (String s in matches)
				itemsComboBox.AppendText(s);
			
			// TODO: this stops the entry process, so you can't continue typing...
			// but it would be nice to see the possibilities, haven't found any doc
			// which describes how this can be done in gtk
			//itemsComboBox.Popup();

			isChanging = false;
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

		public void TextChanged(object sender, EventArgs args)
		{
			if (false)
			{
	
			}				
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="o"></param>
		/// <param name="args"></param>
		public void OnAction(object o, Gtk.AccelActivateArgs args)
		{
			bool isOk = true;


			if (isOk)
			{
				// action
				onOk(true, itemsComboBox.Entry.Text);
				Hide();
				Destroy();
			}
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

		// forward other events
		// react to the [x]-button being pressed
		void TextInput_DeleteEvent(object o, Gtk.DeleteEventArgs args)
		{
			//OnAction(null, null);
			// do nothing
		}

	}
}
