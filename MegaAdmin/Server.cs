using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Drawing;
using Pastel;

namespace MegaAdmin
{
	public class Server
	{
		public readonly EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
		private bool signaled = false;
		public readonly EventWaitHandle CMDevent = new EventWaitHandle(false, EventResetMode.AutoReset);
		private uint logID;
		public string SID { get; private set; } = string.Empty;
		public ushort Port { get; private set; } = 0;
		public List<string> buffer { get; private set; } = new List<string>();
		public string cmdbuffer = string.Empty;
		public bool cmdlock { get; private set; } = false;
		public string Name
		{
			get
			{
				if (Port != 0)
				{
					return "Port:" + Port;
				}
				else if (SID != string.Empty)
				{
					return SID;
				}
				else
				{
					return " Starting... ";
				}
			}
		}
		public readonly string AppFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "SCP Secret Laboratory" + Path.DirectorySeparatorChar;
		public YamlConfig Config { get; private set; }
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
		public bool HasServerMod { get; private set; }
		public string ServerModVersion { get; private set; }
		public string ServerModBuild { get; private set; }
		public bool InitialRoundStarted { get; private set; }
		public bool stopping { get; private set; } = false;
		public bool restarting { get; private set; } = false;

		public Process GameProcess { get; private set; }
		public bool IsGameProcessRunning
		{
			get
			{
				if (GameProcess == null)
					return false;

				GameProcess.Refresh();

				return !GameProcess.HasExited;
			}
		}

		// set via config files
		public bool nolog { get; private set; }
		//public bool runOptimized { get; private set; } = true;
		public int printSpeed { get; private set; }
		public bool DisableConfigValidation { get; private set; }
		public bool ShareNonConfigs { get; private set; }
		public Color defaultColor { get; private set; } = Color.Cyan;

		//BEGIN EVENT LIST
		public readonly List<IEventRoundEnd> roundend = new List<IEventRoundEnd>();
		public readonly List<IEventRoundStart> roundstart = new List<IEventRoundStart>();
		public readonly List<IEventAdminAction> adminaction = new List<IEventAdminAction>();
		public readonly List<IEventServerStart> serverstart = new List<IEventServerStart>();
		public readonly List<IEventServerPreStart> preserverstart = new List<IEventServerPreStart>();
		public readonly List<IEventServerFull> serverfull = new List<IEventServerFull>();
		public readonly List<IEventPlayerConnect> playerconnect = new List<IEventPlayerConnect>();
		public readonly List<IEventPlayerDisconnect> playerdisconnect = new List<IEventPlayerDisconnect>();
		public readonly List<IEventConfigReload> configreload = new List<IEventConfigReload>();
		//END EVENT LIST

		private bool fixBuggedPlayers;
		private FileSystemWatcher watcher;
		public bool IsWatcherRunning
		{
			get
			{
				if (watcher == null)
				{
					return false;
				}
				else
				{
					return true;
				}
			}
		}

		//static stuff
		private static readonly Regex SmodRegex = new Regex(@"\[(.*?)\] (\[.*?\]) (.*)", RegexOptions.Compiled | RegexOptions.Singleline);
		private static readonly Regex VanillaRegex = new Regex(@"(\[.*?\]) (.*)", RegexOptions.Compiled | RegexOptions.Singleline);

		public Server()
		{
			cmdlock = true;
			Program.servers.Add(this);
			Program.WriteMenu(this);
			//Feature_Loader("MeA_features", this);
			Reload();
			Start();
			cmdlock = false;
			Program.WriteMenu(this);
			// Wait if someone tells us to die or do something else.
			do
			{
				signaled = waitHandle.WaitOne(10);
				if (CMDevent.WaitOne(10))
				{
					cmd();
				}
			} while (!signaled);
		}

		private static string GenerateSessionID()
		{
			Random getrandom = new Random();
			byte[] buf = new byte[16];
			for (byte i = 0; i < buf.Length; i++)
			{
				byte rnd = (byte)getrandom.Next(0, 3);
				if (rnd == 0)
				{
					buf[i] = (byte)getrandom.Next(48, 58);
				}
				else if (rnd == 1)
				{
					buf[i] = (byte)getrandom.Next(65, 91);
				}
				else if (rnd == 2)

				{
					buf[i] = (byte)getrandom.Next(97, 123);
				}
			}
			return System.Text.Encoding.ASCII.GetString(buf);
		}

		private void Feature_Loader(string featuredir, Server server)
		{
			DirectoryInfo dir = new DirectoryInfo(featuredir);

			if (!dir.Exists)
			{
				dir.Create();
			}

			foreach (FileInfo file in dir.GetFiles("*.dll"))
			{
				Assembly assembly = Assembly.LoadFrom(file.FullName);
				foreach (Type type in assembly.GetTypes())
				{
					if (type.IsSubclassOf(typeof(Feature)) && type.IsAbstract == false)
					{
						Feature b = type.InvokeMember(null, BindingFlags.CreateInstance, null, null, null) as Feature;
						b.Server = server;
						try
						{
							b.Init();
						}catch(Exception e)
						{
							write("[MeA Feature] [" + b.ID + "] Error in Init()  -  " + e.Message,Color.DarkRed);
						}
					}
				}
			}
		}

		private void Start()
		{
			stopping = false;
			restarting = false;
			SID = GenerateSessionID();
			if (!Directory.Exists("SCPSL_Data"))
			{
				write("Failed - couldnt find server install!", Color.DarkRed);
				write("Please run the 'restart' command to try again...", Color.Gray);
				return;
			}
			if (!Directory.Exists("SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated"))
			{
				Directory.CreateDirectory("SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated");
			}
			string path = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			Directory.CreateDirectory(path);
			watcher = new FileSystemWatcher(path, "sl*.mapi");
			watcher.IncludeSubdirectories = false;
			watcher.Created += OnMapiCreated;
			watcher.EnableRaisingEvents = true;
			this.write("starting server with session ID: " + SID, Color.Yellow);
			InitialRoundStarted = false;
			string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "SCPSL.*", SearchOption.TopDirectoryOnly);
			if (files.Length == 0)
			{
				write("Failed - couldnt find server executable file!", Color.DarkRed);
				write("Please run the 'restart' command to try again...", Color.Gray);
				return;
			}
			List<string> scpslArgs = new List<string>
					{
						"-batchmode",
						"-nographics",
						"-silent-crashes",
						"-nodedicateddelete",
						$"-key{SID}",
						$"-id{Process.GetCurrentProcess().Id}",
						$"-port{Port}"
					};
			if (nolog)
			{
				scpslArgs.Add("-nolog");

				if (Program.RunningPlatform != Platform.Windows)
					scpslArgs.Add("-logFile \"/dev/null\"");
				else
					scpslArgs.Add("-logFile \"NUL\"");
			}
			else
			{
				scpslArgs.Add($"-logFile \"{LogFolder + "SCP_output_log.txt"}\"");
			}
			if (DisableConfigValidation)
			{
				scpslArgs.Add("-disableconfigvalidation");
			}
			if (ShareNonConfigs)
			{
				scpslArgs.Add("-sharenonconfigs");
			}
//			if (!string.IsNullOrEmpty(configLocation))
//			{
//				scpslArgs.Add($"-configpath \"{configLocation}\"");
//			}
			scpslArgs.RemoveAll(string.IsNullOrEmpty);
			string args = string.Join(" ", scpslArgs);
			write("Starting server with the following parameters", Color.Yellow);
			write(files[0] + " " + args, Color.Yellow);
			GameProcess = Process.Start(new ProcessStartInfo(files[0], args){
				CreateNoWindow = true,
				UseShellExecute = false
			});
			GameProcess.Exited += GameProcess_Exited;
			GameProcess.EnableRaisingEvents = true;
			foreach (IEventServerPreStart Event in preserverstart)
			{
				Event.OnServerPreStart();
			}
		}

		private void GameProcess_Exited(object sender, EventArgs e)
		{
			if (restarting || stopping) { return; }
			write("Server has stopped unexpectedly! Restarting...",Color.DarkRed);
			HardRestart();
		}

		public void Stop()
		{
			if (IsGameProcessRunning)
			{
				write("Stopping Server...", Color.Yellow);
				SendMessage("quit");
				while (!GameProcess.HasExited){ Thread.Sleep(1); }
				GameProcess.Dispose();
				GameProcess = null;
				write("Stopped Server", Color.Green);
			}
			DeleteSession();
			if (!restarting)
			{
				Program.stopServer(this);
			}
		}

		private void SoftRestart()
		{
			write("Restarting server with a new Session ID...", Color.Yellow);
			restarting = true;
			SendMessage("RECONNECTRS");
			Stop();
			Start();
		}

		private void HardRestart()
		{
			restarting = true;
			Stop();
			Start();
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
			if (SID == string.Empty) { return; }
			if (IsWatcherRunning)
			{
				watcher.Dispose();
				watcher = null;
			}
			CleanSession();
			string path = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
			if (Directory.Exists(path)) Directory.Delete(path);
		}

		public void write(string msg, Color? color = null)
		{
			lock (this)
			{
				string[] msgspl = Timestamp(msg,true).Split(new string[] { "/n" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string mess in msgspl)
				{
					if (mess == Environment.NewLine) { continue; }
					else
					{
						if(color == null)
						{
							buffer.Add(mess.Substring(0, Program.ClampI(Console.BufferWidth - 1, 0, mess.Length)));
						}
						else
						{
							buffer.Add(mess.Substring(0, Program.ClampI(Console.BufferWidth - 1, 0, mess.Length)).Pastel(color.GetValueOrDefault()));
						}
						Program.WriteBuffer(this);
					}
				}
				Log(msg);
			}
		}

		private void cmd()
		{
			cmdlock = true;
			Program.WriteMenu(this);
			bool send = true;
			string command = cmdbuffer;
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
					stopping = true;
					Stop();
					send = false;
				}
				else if (command.ToLower() == "restart")
				{
					SoftRestart();
					send = false;
				}
				else if (command.ToLower().Substring(0, 3) == "new")
				{
					Program.startServer();
					send = false;
				}
			}
			if (send)
			{
				SendMessage(command);
			}
			cmdlock = false;
			CMDevent.Reset();
			cmdbuffer = string.Empty;
			Program.WriteInput(this);
		}

		private static string logbuff = ""; //buffer log output until we know the servers port.

		private void Log(string message, bool newline = true)
		{
			if (!nolog)
			{
				message = Timestamp(message);
				if (Port == 0)
				{
					if (newline)
					{
						logbuff = logbuff + message + Environment.NewLine;
					}
					else
					{
						logbuff = logbuff + message;
					}
				}
				else
				{
					using (StreamWriter sw = File.AppendText(LogFolder + "MeA_output_log.txt"))
					{
						if (logbuff != "")
						{
							sw.Write(logbuff);
							logbuff = "";
						}
						if (newline)
						{
							sw.WriteLine(message);
						}
						else
						{
							sw.Write(message);
						}
					}
				}
			}
		}

		public void SendMessage(string message)
		{
			lock (this)
			{
				string sessionDirectory = "SCPSL_Data" + Path.DirectorySeparatorChar + "Dedicated" + Path.DirectorySeparatorChar + SID;
				if (!Directory.Exists(sessionDirectory))
				{
					write("Send Message error: sending " + message + " failed. " + sessionDirectory + " does not exist!", Color.Yellow);
					write("skipping", Color.Yellow);
					return;
				}
				string file = sessionDirectory + Path.DirectorySeparatorChar + "cs" + logID + ".mapi";
				if (File.Exists(file))
				{
					write("Send Message error: sending " + message + " failed. " + file + " already exists!", Color.Yellow);
					write("skipping", Color.Yellow);
					logID++;
					return;
				}
				write("Sending request to SCP: Secret Laboratory...", Color.Yellow);
				File.AppendAllText(file, message + "terminator");
				logID++;
			}
		}

		private static string Timestamp(string message, bool color = false)
		{
			if (string.IsNullOrEmpty(message))
				return string.Empty;
			DateTime now = DateTime.Now;
			if (color)
			{
				message = ("[" + now.Hour.ToString("00") + ":" + now.Minute.ToString("00") + ":" + now.Second.ToString("00") + "]").Pastel(Color.Cyan) + " " + message;
			}
			else
			{
				message = "[" + now.Hour.ToString("00") + ":" + now.Minute.ToString("00") + ":" + now.Second.ToString("00") + "] " + message;
			}
			return message;
		}

		private void Reload()
		{
			Config = new YamlConfig("MeA_config.yaml");
			nolog = Config.GetBool("nolog",true);
			printSpeed = Config.GetInt("print_speed",150);
			DisableConfigValidation = Config.GetBool("disable_config_validation");
			ShareNonConfigs = Config.GetBool("share_non_configs", true);
			if(Port == 0)
			{
				Port = (ushort)(Config.GetInt("starting_port", 7777) + (Program.servers.Count-1));
			}
		}

		private void OnMapiCreated(object sender, FileSystemEventArgs e)
		{
			try
			{
				ProcessFile(e.FullPath);
			}
			catch (Exception ex)
			{
				write(ex.Message, Color.DarkRed);
			}
		}

		private void ProcessFile(string file)
		{
			string stream = string.Empty;
			string command = "open";
			bool isRead = false;

			// Lock this object to wait for this event to finish before trying to read another file
			lock (this)
			{
				for (int attempts = 0; attempts < 100; attempts++)
				{
					try
					{
						if (!File.Exists(file)) return;

						// Lock the file to prevent it from being modified further, or read by another instance
						using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)))
						{
							command = "read";
							stream = sr.ReadToEnd();

							isRead = true;
						}

						command = "delete";
						File.Delete(file);

						break;
					}
					catch (UnauthorizedAccessException e)
					{
						write(e.Message, Color.DarkRed);
						Thread.Sleep(8);
					}
					catch (Exception e)
					{
						write(e.Message, Color.DarkRed);
						Thread.Sleep(5);
					}
				}
			}

			if (!isRead)
			{
				write($"Message printer warning: Could not {command} \"{file}\". Make sure that {nameof(MegaAdmin)} has all necessary read-write permissions\nSkipping...", Color.DarkRed);

				return;
			}

			Color color = defaultColor;
			bool display = true;

			if (!string.IsNullOrEmpty(stream.Trim()))
			{
				if (stream.Contains("LOGTYPE"))
				{
					string type = stream.Substring(stream.IndexOf("LOGTYPE")).Trim();
					stream = stream.Substring(0, stream.IndexOf("LOGTYPE")).Trim();

					switch (type)
					{
						case "LOGTYPE02":
							color = Color.Green;
							break;
						case "LOGTYPE-8":
							color = Color.DarkRed;
							break;
						case "LOGTYPE14":
							color = Color.Magenta;
							break;
						default:
							color = defaultColor;
							break;
					}
				}
			}

			if (stream.EndsWith(Environment.NewLine))
				stream = stream.Substring(0, stream.Length - Environment.NewLine.Length);

			Match vmatch = VanillaRegex.Match(stream);
			if (vmatch.Success)
			{
				if (vmatch.Groups.Count >= 2 && !((vmatch.Groups[1].Value + vmatch.Groups[2].Value).Contains("][")))
				{
					Color tagColor = Color.Yellow;
					Color msgColor = Color.Gray;
					switch (vmatch.Groups[1].Value.Trim())
					{
						case "[DEBUG_MAPGEN]":
							tagColor = Color.Gray;
							break;
						case "[DEBUG_VC]":
							tagColor = Color.Gray;
							break;
						default:
							break;
					}
					write(vmatch.Groups[1].Value.Pastel(tagColor) + " " + vmatch.Groups[2].Value.Pastel(msgColor));
					return;
				}
			}
			// Smod2 loggers pretty printing
			Match match = SmodRegex.Match(stream);
			if (match.Success)
			{
				if (match.Groups.Count >= 3)
				{
					Color levelColor = Color.Cyan;
					Color tagColor = Color.Yellow;
					Color msgColor = Color.White;
					switch (match.Groups[1].Value.Trim())
					{
						case "DEBUG":
							levelColor = Color.Gray;
							break;
						case "INFO":
							levelColor = Color.Green;
							break;
						case "WARN":
							levelColor = Color.Yellow;
							break;
						case "ERROR":
							levelColor = Color.Red;
							msgColor = Color.Red;
							break;
						default:
							break;
					}
					write(("[" + match.Groups[1].Value + "]").Pastel(levelColor) + " " + match.Groups[2].Value.Pastel(tagColor) + " " + match.Groups[3].Value.Pastel(msgColor));

					// P.S. the format is [Info] [courtney.exampleplugin] Something interesting happened
					// That was just an example

					// This return should be here
					return;
				}
			}
			if (stream.Contains("Mod Log:"))
			{
				foreach (IEventAdminAction Event in adminaction)
				{
					Event.OnAdminAction(stream.Replace("Mod log:", string.Empty));
				}
			}
			else if (stream.Contains("ServerMod - Version"))
			{
				HasServerMod = true;
				// This should work fine with older ServerMod versions too
				string[] streamSplit = stream.Replace("ServerMod - Version", string.Empty).Split('-');

				if (streamSplit.Length != 0)
				{
					ServerModVersion = streamSplit[0].Trim();
					ServerModBuild = (streamSplit.Length > 1 ? streamSplit[1] : "A").Trim();
				}
			}
			else if (stream.Contains("Round finished!"))
			{
				foreach (IEventRoundEnd Event in roundend)
				{
					Event.OnRoundEnd();
				}
			}
			else if (stream.Contains("Waiting for players"))
			{
				Program.WriteInput(this);
				if (!InitialRoundStarted)
				{
					InitialRoundStarted = true;
					foreach (IEventRoundStart Event in roundstart)
					{
						Event.OnRoundStart();
					}
				}
				if (fixBuggedPlayers)
				{
					SendMessage("ROUNDRESTART");
					fixBuggedPlayers = false;
				}
			}
			else if (stream.Contains("New round has been started"))
			{
				foreach (IEventRoundStart Event in roundstart)
				{
					Event.OnRoundStart();
				}
			}
			else if (stream.Contains("Level loaded. Creating match..."))
			{
				foreach (IEventServerStart Event in serverstart)
				{
					Event.OnServerStart();
				}
			}
			else if (stream.Contains("Server full"))
			{
				foreach (IEventServerFull Event in serverfull)
				{
					Event.OnServerFull();
				}
			}
			else if (stream.Contains("Player connect"))
			{
				display = false;
				foreach (IEventPlayerConnect Event in playerconnect)
				{
					string name = stream.Substring(stream.IndexOf(":"));
					Event.OnPlayerConnect(name);
				}
			}
			else if (stream.Contains("Player disconnect"))
			{
				display = false;
				foreach (IEventPlayerDisconnect Event in playerdisconnect)
				{
					string name = stream.Substring(stream.IndexOf(":"));
					Event.OnPlayerDisconnect(name);
				}
			}
			else if (stream.Contains("Player has connected before load is complete"))
			{
				fixBuggedPlayers = true;
			}
			if (display)
			{
				write(stream, color);
				if(printSpeed != 0)
				{
					Thread.Sleep(printSpeed);
				}
			}
		}
	}
}
