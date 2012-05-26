using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Tomboy.PrivateNotes
{
	class Statistics
	{
		private static String statsPath = Path.Combine(Services.NativeApplication.ConfigurationDirectory, "stats.txt");
		private static bool AUTORESTART = false;
		//private static int WAITTORESET = 400;
		//private static int MAXREPETITIONS = 100;
		private static int WAITTORESET = 500;
		private static int MAXREPETITIONS = 20;
		private static bool VIOLATIONS_THROW = false;
		private static bool WRITE_LOGS = false;
		

		private int repetitions = 0;
		private static String randName = "rand";

		private static Statistics instance = new Statistics();

		public static Statistics Instance
		{
			get { return instance; }
		}

		public static void Init()
		{
			bool newFile = false;
			randName += new Random().Next(10, 100);
			String configured = Preferences.Get(AddinPreferences.SYNC_PRIVATENOTES_SERVERPATH) as string;
			String path = "";
			if (configured != null)
			{
				path = Util.GetCleanText(configured);
			}
			String newPath = Path.Combine(Services.NativeApplication.ConfigurationDirectory,
				                            "_" + DateTime.Now.ToString("MMdd_HHmm") + "stats" + path + ".txt");
			newFile = statsPath != newPath;
			statsPath = newPath;


			if (WRITE_LOGS && newFile)
			{
				using (StreamWriter w = File.AppendText(statsPath))
				{
					w.WriteLine("# successful (+yes,-no) | notesbefore | notesafter | totalms | comMs (webdav) | cryptms (gpg)");
				}
			}
		}

		private int notesCountStart = 0;
		private int notesCountEnd = 0;

		private TimeSpan totalTime = TimeSpan.Zero;
		private TimeSpan comTime = TimeSpan.Zero;
		private TimeSpan cryptTime = TimeSpan.Zero;

		private DateTime? syncStart = null;

		private Stopwatch comSw = Stopwatch.StartNew();
		private Stopwatch cryptSw = Stopwatch.StartNew();


		public void StartSyncRun()
		{
			Reset(false);

			notesCountStart = Tomboy.DefaultNoteManager.Notes.Count;
			if (syncStart.HasValue)
			{
				throw new Exception("StartSyncRun while previous was not finished");
			}
			syncStart = DateTime.Now;
		}

		private bool success = false;
		public void SetSyncStatus(bool success)
		{
			this.success = success;
		}

		public void FinishSyncRun(bool? successful)
		{
			if (VIOLATIONS_THROW && (comSw.IsRunning || cryptSw.IsRunning))
			{
				throw new Exception("FinishSyncRun while none is in progress");
			}
			if (!syncStart.HasValue)
			{
				Logger.Error("multiple FinishSyncRun calls!");
			}
			else
			{
				totalTime = DateTime.Now - syncStart.Value;
				notesCountEnd = Tomboy.DefaultNoteManager.Notes.Count;
				if (successful.HasValue)
				{
					success = successful.Value;
				}
				Save(success);
				Reset(true);
			}
		}

		public void StartCommunication()
		{
			if (VIOLATIONS_THROW && (!syncStart.HasValue || comSw.IsRunning))
			{
				throw new Exception("StartCommunication while previous was not finished");
			}
			comSw.Reset();
			comSw.Start();
		}

		public void EndCommunication()
		{
			if (VIOLATIONS_THROW && (!syncStart.HasValue || !comSw.IsRunning || cryptSw.IsRunning))
			{
				throw new Exception("EndCommunication while none is in progress");
			}
			comSw.Stop();
			comTime = comTime.Add(TimeSpan.FromMilliseconds(comSw.ElapsedMilliseconds));
		}

		public void StartCrypto()
		{
			if (VIOLATIONS_THROW && (!syncStart.HasValue || cryptSw.IsRunning || comSw.IsRunning))
			{
				throw new Exception("StartCrypto while previous was not finished");
			}
			cryptSw.Reset();
			cryptSw.Start();
		}

		public void EndCrypto()
		{
			if (VIOLATIONS_THROW && (!syncStart.HasValue || !cryptSw.IsRunning || comSw.IsRunning))
			{
				throw new Exception("EndCrypto while none is in progress");
			}
			cryptSw.Stop();
			cryptTime = cryptTime.Add(TimeSpan.FromMilliseconds(cryptSw.ElapsedMilliseconds));
		}

		private int blockSaves = 0;
		private void Save(bool successful)
		{
			if (!WRITE_LOGS)
			{
				return;
			}
			if (blockSaves > 0)
			{
				blockSaves--;
				return;
			}
			using (StreamWriter w = File.AppendText(statsPath))
			{
				w.Write(successful?"+":"-");
				w.Write(" ");
				w.Write(notesCountStart);
				w.Write(" ");
				w.Write(notesCountEnd);
				w.Write(" ");
				w.Write((long) (totalTime.TotalMilliseconds));
				w.Write(" ");
				w.Write((long) (comTime.TotalMilliseconds));
				w.Write(" ");
				w.Write((long) (cryptTime.TotalMilliseconds));
				w.Write(" logic=");
				w.Write((long)((totalTime - (comTime + cryptTime)).TotalMilliseconds));
				w.WriteLine();
			}
		}

		private void Reset(bool plusTriggerRestart)
		{
			success = false;
			syncStart = null;
			totalTime = TimeSpan.Zero;
			cryptTime = TimeSpan.Zero;
			comTime = TimeSpan.Zero;

			comSw.Stop();
			comSw.Reset();
			cryptSw.Stop();
			cryptSw.Reset();

			if (plusTriggerRestart && AUTORESTART)
			{
				if (repetitions < MAXREPETITIONS)
				{
					repetitions++;
					Timer t = new Timer(WAITTORESET);
					t.AutoReset = false;
					t.Elapsed += new ElapsedEventHandler(t_Elapsed);
					t.Enabled = true;
				}
			}
		}

		void t_Elapsed(object sender, ElapsedEventArgs e)
		{
			GuiUtils.GtkInvokeAndWait(() =>
			{
				Tomboy.SyncDialog.Respond(Gtk.ResponseType.Close);
			});
			Thread.Sleep(200);
			//CreateRandomNote();
			//CreateRandomNote1();
			//Thread.Sleep(200);
			GuiUtils.GtkInvokeAndWait(() =>
			{
			    Tomboy.ActionManager["NoteSynchronizationAction"].Activate();
			});
		}

		
		private int counter = 0;
		private Note lastCreatedNote = null;

		// creates 1 random note and deltes a maybe existent previous one
		private void CreateRandomNote1()
		{
			if (lastCreatedNote != null)
			{
				GuiUtils.GtkInvokeAndWait(() =>
				{
					Tomboy.DefaultNoteManager.Delete(lastCreatedNote);
				});
			}
			GuiUtils.GtkInvokeAndWait(() =>
				{
					lastCreatedNote = Tomboy.DefaultNoteManager.Create(randName + counter);
					lastCreatedNote.QueueSave(ChangeType.ContentChanged);
					lastCreatedNote.Save();
				});
		}


		private void CreateRandomNote()
		{
			// every 20 notes, remove all created ones
			if (counter % 21 == 0)
			{
				if (lastCreatedNote != null)
				{
					GuiUtils.GtkInvokeAndWait(() =>
					{
						List<Note> delMe = Tomboy.DefaultNoteManager.Notes.FindAll((Note n) => n.Title.StartsWith("rand"));
						foreach (Note n in delMe)
						{
							Tomboy.DefaultNoteManager.Delete(n);
						}
					});
					blockSaves = 1;
				}
			}
			else
			{
				GuiUtils.GtkInvokeAndWait(() =>
				    {
				        lastCreatedNote = Tomboy.DefaultNoteManager.Create(randName + counter);
				        lastCreatedNote.QueueSave(ChangeType.ContentChanged);
				        lastCreatedNote.Save();
				    });
			}
			counter++;
		}

	}
}
