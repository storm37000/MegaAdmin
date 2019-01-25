using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MegaAdmin
{
	class Feature_Loader
	{
		public Feature_Loader(string featuredir, Server server)
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
						Feature b = type.InvokeMember(null,BindingFlags.CreateInstance, null, null, null) as Feature;
						server.features.Add(b);
						b.Init(server);
					}
				}
			}
		}
	}
}
