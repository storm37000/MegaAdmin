using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MegaAdmin
{
	public class Server
	{
		private uint logID;
		public string SID { get; private set; }
		public ushort Port { get; set; }
		public List<string> buffer { get; private set; } = new List<string>();
		public string cmdbuffer = string.Empty;
		public string Name
		{
			get
			{
				if (Port != 0)
				{
					return "Port:      " + Port;
				}
				else if (SID != string.Empty)
				{
					return SID;
				}
				else
				{
					return " Starting Up... ";
				}
			}
		}
		public string AppFolder { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "SCP Secret Laboratory" + Path.DirectorySeparatorChar;
		public YamlConfig Config { get; private set; }

		// set via config files
		public bool nolog { get; private set; }
		// 

		public string ConfigKey { get; private set; }
		public string LogFolder
		{
			get
			{
				string loc = AppFolder + "ServerLogs" + Path.DirectorySeparatorChar + Port + Path.DirectorySeparatorChar;
				if (!Directory.Exists(loc))
				{
					Directory.CreateDirectory(loc);
				}
				return loc;
			}
		}
		public bool HasServerMod { get; set; }
		public string ServerModVersion { get; set; }
		public string ServerModBuild { get; set; }

		public bool InitialRoundStarted { get; set; }
		public bool fixBuggedPlayers { get; set; } = true;
		public bool runOptimized { get; private set; } = true;
		public int printSpeed { get; private set; } = 150;
		public bool stopping { get; private set; }

		private Thread printerThread;
		public Process GameProcess { get; private set; }

		public List<Feature> features = new List<Feature>();

		//BEGIN EVENT LIST
		public List<IEventRoundEnd> roundend = new List<IEventRoundEnd>();
		public List<IEventRoundStart> roundstart = new List<IEventRoundStart>();
		public List<IEventAdminAction> adminaction = new List<IEventAdminAction>();
		public List<IEventServerStart> serverstart = new List<IEventServerStart>();
		public List<IEventServerPreStart> preserverstart = new List<IEventServerPreStart>();
		public List<IEventServerFull> serverfull = new List<IEventServerFull>();
		public List<IEventPlayerConnect> playerconnect = new List<IEventPlayerConnect>();
		public List<IEventPlayerDisconnect> playerdisconnect = new List<IEventPlayerDisconnect>();
		public List<IEventConfigReload> configreload = new List<IEventConfigReload>();
		//END EVENT LIST

		public Server(string cfgkey)
		{
			this.ConfigKey = cfgkey;
			new Feature_Loader("MeA_features",this);
			Reload();
			printerThread = new Thread(new ThreadStart(() => new OutputThread(this)));
			Start();
		}

		public void OnTick()
		{
//			if (GameProcess != null && !GameProcess.HasExited)
//			{
//			}
//			else if (!stopping)
//			{
//				foreach (IEventCrash f in cras)
//				{
//					if (f is IEventCrash)
//					{
//						((IEventCrash)f).OnCrash();
//					}
//				}
//
//				Write("Game engine exited/crashed/closed/restarting", ConsoleColor.Red);
//				Write("Cleaning Session", ConsoleColor.Red);
//				CleanUp();
//				GenerateSessionID();
//				Write("Restarting game with new session id");
//				StartServer();
//				InitFeatures();
//			}
		}

		private void Start()
		{
			stopping = false;
			SID = Program.GenerateSessionID();
			this.write("starting server with session ID: " + SID, Color.Yellow);
			InitialRoundStarted = false;
			string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "SCPSL.*", SearchOption.TopDirectoryOnly);
			if (files.Length == 0)
			{
				write("Failed - couldnt find server executable file!", Color.Red);
				write("Please run the 'restart' command to try again...", Color.Gray);
				return;
			}
			string args;
			if (true) //nolog
			{
				if (Program.RunningPlatform() != Program.Platform.Windows)
				{
					args = "-batchmode -nographics -nolog -key" + SID + " -silent-crashes -id" + (object)Process.GetCurrentProcess().Id + " -logFile /dev/null";
				}
				else
				{
					args = "-batchmode -nographics -nolog -key" + SID + " -silent-crashes -id" + (object)Process.GetCurrentProcess().Id + " -logFile NUL";
				}
			}
			else
			{
				args = "-batchmode -nographics -key" + SID + " -silent-crashes -id" + (object)Process.GetCurrentProcess().Id + " -logFile \"" + LogFolder + "SCP_output_log.txt" + "\"";
			}
			write("Starting server with the following parameters", Color.Yellow);
			try
			{
				write(files[0] + " " + args, Color.Yellow);
				ProcessStartInfo startInfo = new ProcessStartInfo(files[0]);
				startInfo.Arguments = args;
				GameProcess = Process.Start(startInfo);
			}
			catch (Exception e)
			{
				write("Failed - Executable file or config issue!", Color.Red);
				write(e.Message, Color.Red);
				write("Please run the 'restart' command to try again...", Color.Gray);
				return;
			}
			foreach (IEventServerPreStart Event in preserverstart)
			{
				Event.OnServerPreStart();
			}
			printerThread.Start();
		}

		public void Stop()
		{
			stopping = true;
			if (printerThread.IsAlive)
			{
				printerThread.Abort();
				SendMessage("quit");
				DeleteSession();
			}
			Program.stopServer(this);
		}

		public void SoftRestart()
		{
			if (HasServerMod)
			{
				SendMessage("RECONNECTRS");
			}
			Stop();
			Start();
			Program.WriteMenu();
		}

		private void CleanSession()
		{
			string path = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			if (Directory.Exists(path))
			{
				foreach (string file in Directory.GetFiles(path))
				{
					File.Delete(file);
				}
			}

		}

		private void DeleteSession()
		{
			CleanSession();
			string path = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			if (Directory.Exists(path)) Directory.Delete(path);
		}

		public void write(string msg, string color)
		{
			lock (this)
			{
				string[] msgspl = Timestamp(msg).Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
				foreach (string mess in msgspl)
				{
					if (mess == Environment.NewLine) { continue; }
					else
					{
						buffer.Add(color + mess.Substring(0, Program.ClampI(Console.BufferWidth - 1, 0, mess.Length)) + Color.Reset + Environment.NewLine);
						Program.WriteBuffer(this);
					}
				}
				Log(msg);
			}
		}

		public void writePart(string msg, string color, bool date = false, bool lineEnd = false)
		{
			lock (this)
			{
				string msgcol = color + msg + Color.Reset;
				if (date)
				{
					buffer.Add(Timestamp(msgcol));
					Program.WriteBuffer(this);
				}
				else if (lineEnd && !msg.EndsWith(Environment.NewLine))
				{
					buffer.Add(msgcol + Environment.NewLine);
					Program.WriteBuffer(this);
				}
				else
				{
					buffer.Add(msgcol);
					Program.WriteBuffer(this);
				}
			}
		}

		public void cmd()
		{
			string command = cmdbuffer;
			cmdbuffer = string.Empty;
			this.write(">>> " + command, Color.DarkMagenta);
			if (command.Length >= 3)
			{
				if (command.ToLower() == "config reload")
				{
					this.write("Reloading Config...", Color.Blue);
					Reload();
					this.write("Config Reloaded!", Color.Green);
				}
				else if (command.ToLower() == "quit")
				{
					Stop();
					return;
				}
				else if (command.ToLower() == "restart")
				{
					SoftRestart();
					return;
				}
				else if (command.ToLower().Substring(0, 3) == "new")
				{
					Program.startServer();
					return;
				}
			}
			SendMessage(command);
		}

		private static string logbuff = ""; //buffer log output until we know the servers port if multimode isnt enabled.

		public void Log(string message, string filename = "MeA_output_log.txt")
		{
			if (!nolog || filename != "MA_output_log.txt")
			{
				message = Timestamp(message);
				if (Port == 0)
				{
					logbuff = logbuff + message + Environment.NewLine;
				}
				else
				{
					lock (this)
					{
						using (StreamWriter sw = File.AppendText(LogFolder + filename))
						{
							if (logbuff != "")
							{
								sw.Write(logbuff);
								logbuff = "";
							}
							sw.WriteLine(message);
						}
					}
				}
			}
		}

		public void SendMessage(string message)
		{
			string sessionDirectory = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			if (!Directory.Exists(sessionDirectory))
			{
				this.write("Send Message error: sending " + message + " failed. " + sessionDirectory + " does not exist!", Color.Yellow);
				this.write("skipping", Color.Yellow);
				return;
			}

			string file = sessionDirectory + Path.DirectorySeparatorChar + "cs" + logID + ".mapi";
			if (File.Exists(file))
			{
				this.write("Send Message error: sending " + message + " failed. " + file + " already exists!", Color.Yellow);
				this.write("skipping", Color.Yellow);
				logID++;
				return;
			}
			this.write("Sending request to SCP: Secret Laboratory...", Color.Yellow);
			StreamWriter streamWriter = new StreamWriter(file);
			logID++;
			streamWriter.WriteLine(message + "terminator");
			streamWriter.Close();
		}

		public static string Timestamp(string message)
		{
			if (string.IsNullOrEmpty(message))
				return string.Empty;
			DateTime now = DateTime.Now;
			message = "[" + now.Hour.ToString("00") + ":" + now.Minute.ToString("00") + ":" + now.Second.ToString("00") + "] " + message;
			return message;
		}

		public void Reload()
		{
			Config = new YamlConfig("MeA_config.yaml");
			nolog = Config.GetBool("MeA_nolog");
			printSpeed = Config.GetInt("MeA_print_speed");
		}

		public bool ServerModCheck(int major, int minor, int fix)
		{
			if (this.ServerModVersion == null)
			{
				return false;
			}

			string[] parts = ServerModVersion.Split('.');
			int verMajor = 0;
			int verMinor = 0;
			int verFix = 0;
			if (parts.Length == 3)
			{
				Int32.TryParse(parts[0], out verMajor);
				Int32.TryParse(parts[1], out verMinor);
				Int32.TryParse(parts[2], out verFix);
			}
			else if (parts.Length == 2)
			{
				Int32.TryParse(parts[0], out verMajor);
				Int32.TryParse(parts[1], out verMinor);
			}
			else
			{
				return false;
			}

			if (major == 0 && minor == 0 && verFix == 0)
			{
				return false;
			}

			return (verMajor > major) || (verMajor >= major && verMinor > minor) || (verMajor >= major && verMinor >= minor && verFix >= fix);

		}
	}
}
