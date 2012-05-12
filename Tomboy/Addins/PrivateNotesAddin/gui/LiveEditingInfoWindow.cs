// Part of PrivateNotes Project - FH Hagenberg
// http://privatenotes.dyndns-server.com/wiki/
// Authors: 
//      Paul Klingelhuber <s1010455009@students.fh-hagenberg.at>
// 
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

		/// <summary>
		/// present information about a note, if info for this note already exists, it will be overwritten
		/// </summary>
		/// <param name="noteId"></param>
		/// <param name="infoText"></param>
		/// <param name="editing"></param>
		public void SetInfo(String noteId, String infoText, bool editing)
		{
			if (infos.ContainsKey(noteId))
				infos.Remove(noteId);

			EditingInfo infoObj = new EditingInfo() { info = infoText, live = editing };
			infos.Add(noteId, infoObj);
			UpdateUi();
		}

#region private-stuff

		/// <summary>
		/// update the displayed information
		/// </summary>
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

		/// <summary>
		/// create an entry for displaying, constains the indicator if it's live, the note name and an info text
		/// </summary>
		/// <param name="live"></param>
		/// <param name="noteId">note-id, name will be retrieved for it automatically</param>
		/// <param name="info"></param>
		/// <returns></returns>
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

		/// <summary>
		/// get the name (displayed) for a noteid
		/// </summary>
		/// <param name="noteId"></param>
		/// <returns></returns>
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

#endregion

	}

}
