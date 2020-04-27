using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MegaAdmin
{
	public class YamlConfig
	{
		public string[] RawData;
		private string path;

		public YamlConfig(string path)
		{
			this.path = path;
			if (!File.Exists(path))
			{
				File.Create(path).Close();
			}
			if (File.Exists(path))
			{
				RawData = File.ReadAllLines(path, System.Text.Encoding.UTF8);
			}
			else
			{
				RawData = new string[] { };
			}
		}

		public string GetString(string key, string def = null)
		{
			foreach (string line in RawData)
			{
				if (line.ToLower().StartsWith(key.ToLower() + ": "))
				{
					if(line.Substring(key.Length + 2) == "default")
					{
						return def;
					}
					else
					{
						return line.Substring(key.Length + 2);
					}
				}
			}
			Array.Resize(ref RawData, RawData.Length + 1);
			RawData[RawData.Length-1] = key + ": default";
			File.WriteAllLines(path, RawData, System.Text.Encoding.UTF8);
			return def;
		}

		public int GetInt(string key, int def = 0)
		{
			int.TryParse(GetString(key, def.ToString()), out def);
			return def;
		}

		public uint GetUInt(string key, uint def = 0)
		{
			uint.TryParse(GetString(key, def.ToString()), out def);
			return def;
		}

		public short GetShort(string key, short def = 0)
		{
			short.TryParse(GetString(key, def.ToString()), out def);
			return def;
		}

		public ushort GetUShort(string key, ushort def = 0)
		{
			ushort.TryParse(GetString(key, def.ToString()), out def);
			return def;
		}

		public float GetFloat(string key, float def = 0f)
		{
			float.TryParse(GetString(key, def.ToString()).Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out def);
			return def;
		}

		public bool GetBool(string key, bool def = false)
		{
			return GetString(key, def.ToString().ToLower()) == "true";
		}

		public List<string> GetStringList(string key)
		{
			var read = false;
			var list = new List<string>();
			foreach (var line in RawData)
			{
				if (line.ToLower().StartsWith(key.ToLower() + ":"))
				{
					read = true;
					continue;
				}
				if (!read) continue;
				if (line.StartsWith(" - ")) list.Add(line.Substring(3));
				else if (!line.StartsWith("#")) break;
			}
			return list;
		}

		public List<int> GetIntList(string key)
		{
			var list = GetStringList(key);
			return list.Select(x => Convert.ToInt32(x)).ToList();
		}

		public Dictionary<string, string> GetStringDictionary(string key)
		{
			//var list = GetStringList(key);
			Dictionary< string,string> dict = new Dictionary<string, string>();
			foreach (string item in RawData)
			{
				var i = item.IndexOf(": ", StringComparison.Ordinal);
				dict.Add(item.Substring(0, i), item.Substring(i + 2));
			}

			return dict;
		}

		public static string[] ParseCommaSeparatedString(string data)
		{
			if (!data.StartsWith("[") || !data.EndsWith("]")) return null;
			data = data.Substring(1, data.Length - 2);
			return data.Split(new string[] { ", " }, StringSplitOptions.None);
		}
	}
}
