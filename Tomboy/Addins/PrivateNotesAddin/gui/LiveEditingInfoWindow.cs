using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Gdk;
using Gtk;
using Infinote;
using Mono.Unix;
using PrivateNotes.Infinote;

namespace Tomboy.PrivateNotes
{

	/// <summary>
	/// displays information about editing sessions
	/// </summary>
	public class LiveEditingInfoWindow : Gtk.Window
	{

		static Gdk.Color colorRed = new Gdk.Color();
		static Gdk.Color colorGreen = new Gdk.Color();

		private TomboyNoteProvider provider;

		struct EditingInfo
		{
			public bool live;
			public string info;
		}

		/// <summary>
		/// key=noteId
		/// </summary>
		private Dictionary<String, EditingInfo> infos = new Dictionary<string, EditingInfo>(); 

		static LiveEditingInfoWindow()
		{
			Gdk.Color.Parse("red", ref colorRed);
			Gdk.Color.Parse("green", ref colorGreen);
		}

		public LiveEditingInfoWindow() : base(Catalog.GetString("Live Editing Information"))
		{
			
		}

		public void SetInfo(String noteId, String infoText, bool editing)
		{
			if (infos.ContainsKey(noteId))
				infos.Remove(noteId);

			EditingInfo infoObj = new EditingInfo() { info = infoText, live = editing };
			infos.Add(noteId, infoObj);
			UpdateUi();
		}

		private void UpdateUi()
		{
			VBox newContent = new VBox(true, 2);
			foreach (var item in infos)
			{
				Gtk.Widget lbl = CreateEntry(item.Value.live, item.Key, item.Value.info);
				newContent.PackStart(lbl, true, true, 2);
			}

			EventHandler showDelegate = delegate(object s, EventArgs ea)
			                            	{
												if (Child != null)
												{
													Remove(Child);
												}
			                            		Add(newContent);
												newContent.ShowAll();
												Present();
			                            	};
			Gtk.Application.Invoke(showDelegate);
		}

		private Gtk.Widget CreateEntry(bool live, String noteId, String info)
		{
			HBox box = new HBox(false, 0);
			box.BorderWidth = 2;
			
			// check if contact is online:
			Gtk.Label indicator = new Label("\u25CF");
			indicator.ModifyFg(StateType.Normal, live ? colorGreen : colorRed);
			indicator.ModifyFg(StateType.Prelight, live ? colorGreen : colorRed);
			box.PackStart(indicator, false, false, 2);


			Gtk.Label lbl = new Gtk.Label(GetNoteName(noteId));
			box.PackStart(lbl, true, true, 2);

			Gtk.Label lbl2 = new Gtk.Label(info);
			box.PackStart(lbl2, true, true, 2);

			box.SetSizeRequest(300, 25);
			
			return box;
		}

		private String GetNoteName(String noteId)
		{
			string result = noteId;
			if (provider == null)
			{
				provider = Communicator.Instance.NoteProvider;
			}
			if (provider != null)
			{
				Note n = provider.GetNote(noteId);
				if (n != null)
				{
					result = n.Title;
				}
			}
			return result;
		}

	}

}
