using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Mono.Unix;

namespace Tomboy.PrivateNotes
{
	public delegate void inputDone(bool ok, String value);

	/// <summary>
	/// utility class that is able to display a text-input
	/// dialog
	/// </summary>
	public class TextInput : Gtk.Dialog
	{
		Gtk.Entry text;
		Gtk.Label match_label;
		inputDone onOk;
		System.Text.RegularExpressions.Regex regex = null;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="message"></param>
		/// <param name="defaultvalue"></param>
		/// <param name="regexmatch">if != null, input will only be accepted if it matches the regex</param>
		/// <param name="onFinished">callback function when input is done</param>
		public TextInput(String message, String defaultvalue, String regexmatch, inputDone onFinished)
		{
			if (regexmatch != null)
				regex = new System.Text.RegularExpressions.Regex(regexmatch);
			
			onOk = onFinished;
			Title = Catalog.GetString("Input");
			Gtk.VBox box = new Gtk.VBox(false, 6);

			box.PackStart(new Gtk.Label(Catalog.GetString(message)), true, true, 6);

			text = new Gtk.Entry();
			text.Text = defaultvalue;
			box.PackStart(text);

			text.Changed += TextChanged;

			if (regex != null)
			{
				match_label = new Gtk.Label();
				match_label.Markup = Catalog.GetString("input ok");
				box.PackStart(match_label);
			}


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
			if (regex != null)
			{
				bool valueOk = regex.IsMatch(text.Text);
				if (valueOk) {
					match_label.Markup = Catalog.GetString("input ok");
				} else {
					match_label.Markup = Catalog.GetString("wrong input");
				}
			}				
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="o"></param>
		/// <param name="args"></param>
		public void OnAction(object o, Gtk.AccelActivateArgs args)
		{
			bool isOk = false;
			if (regex == null)
				isOk = true;
			else
				isOk = regex.IsMatch(text.Text);


			if (isOk)
			{
				// action
				onOk(true, text.Text);
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
