using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

namespace KH2EventVoiceRandomizer
{
	static class Program
	{
		static Random rand = new Random();
		static Settings settings;

		static void Main(string[] args)
		{
			if (File.Exists("settings.yml"))
				settings = new Deserializer().Deserialize<Settings>(File.ReadAllText("settings.yml"));
			else
				settings = new Settings();
			string[] voices = File.ReadAllLines("eventvoice.txt");
			List<VoiceLine> voiceLines = voices.Select(a => new VoiceLine(a)).ToList();
			if (settings.IgnoreWorlds != null)
				voiceLines.RemoveAll(a => settings.IgnoreWorlds.Contains(a.World));
			if (settings.IgnoreCharacters != null)
				voiceLines.RemoveAll(a => settings.IgnoreCharacters.Contains(a.Character));
			switch (settings.WorldMode)
			{
				case Mode.Mix:
					RandomizeWorld(voiceLines, voiceLines);
					break;
				case Mode.Separate:
					foreach (var world in voiceLines.GroupBy(a => a.World).Select(a => a.ToList()))
						RandomizeWorld(world, world);
					break;
				case Mode.Swap:
					{
						List<List<VoiceLine>> worlds = voiceLines.GroupBy(a => a.World).Select(a => a.ToList()).ToList();
						int[] ids = Enumerable.Range(0, worlds.Count).ToArray();
						rand.RandomizeArray(ids);
						for (int i = 0; i < worlds.Count; i++)
							RandomizeWorld(worlds[i], worlds[ids[i]]);
					}
					break;
			}
			Mod mod = new Mod
			{
				Title = "Event Voice Randomizer",
				Description = "Randomizes voices in cutscenes.",
				Assets = new List<Asset>()
			};
			string ext = "win32.scd";
			switch (settings.Platform)
			{
				case Platform.PS2:
					ext = "vag";
					break;
			}
			string filepathfmt = $"voice/{settings.Language}/event/{{0}}.{ext}";
			foreach (var item in voiceLines)
			{
				Asset src = new Asset() { Name = string.Format(filepathfmt, item.Replacement), Type = "internal" };
				Asset asset = new Asset() { Name = string.Format(filepathfmt, item.Name), Method = "copy", Sources = new List<Asset>() { src } };
				mod.Assets.Add(asset);
			}
			File.WriteAllText("mod.yml", new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build().Serialize(mod));
		}

		private static void RandomizeWorld(List<VoiceLine> dstPool, List<VoiceLine> srcPool)
		{
			switch (settings.CharacterMode)
			{
				case Mode.Mix:
					RandomizeVoiceList(dstPool, srcPool);
					break;
				case Mode.Separate:
					{
						var dstChars = dstPool.GroupBy(a => a.Character).ToDictionary(a => a.Key, b => b.ToList());
						var srcChars = srcPool.GroupBy(a => a.Character).ToDictionary(a => a.Key, b => b.ToList());
						foreach (string name in dstChars.Keys.Intersect(srcChars.Keys))
							RandomizeVoiceList(dstChars[name], srcChars[name]);
						var dstUnique = dstChars.Where(a => !srcChars.ContainsKey(a.Key)).Select(a => a.Value).ToList();
						var srcUnique = srcChars.Where(a => !dstChars.ContainsKey(a.Key)).Select(a => a.Value).ToList();
						while (srcUnique.Count < dstUnique.Count)
							srcUnique.Add(srcPool);
						rand.RandomizeList(srcUnique);
						for (int i = 0; i < dstUnique.Count; i++)
							RandomizeVoiceList(dstUnique[i], srcUnique[i]);

					}
					break;
				case Mode.Swap:
					{
						var dstChars = dstPool.GroupBy(a => a.Character).Select(a => a.ToList()).ToList();
						var srcChars = srcPool.GroupBy(a => a.Character).Select(a => a.ToList()).ToList();
						while (srcChars.Count < dstChars.Count)
							srcChars.Add(srcPool);
						rand.RandomizeList(srcChars);
						for (int i = 0; i < dstChars.Count; i++)
							RandomizeVoiceList(dstChars[i], srcChars[i]);
					}
					break;
			}
		}

		private static void RandomizeVoiceList(List<VoiceLine> dstPool, List<VoiceLine> srcPool)
		{
			srcPool = new List<VoiceLine>(srcPool);
			int srccnt = srcPool.Count;
			while (srcPool.Count < dstPool.Count)
				srcPool.Add(srcPool[rand.Next(srccnt)]);
			rand.RandomizeList(srcPool);
			for (int i = 0; i < dstPool.Count; i++)
				dstPool[i].Replacement = srcPool[i].Name;
		}

		static void RandomizeArray<T>(this Random rand, T[] arr)
		{
			int[] keys = new int[arr.Length];
			for (int i = 0; i < arr.Length; i++)
				keys[i] = rand.Next();
			Array.Sort(keys, arr);
		}

		static void RandomizeList<T>(this Random rand, List<T> list)
		{
			T[] tmp = list.ToArray();
			rand.RandomizeArray(tmp);
			list.Clear();
			list.AddRange(tmp);
		}
	}

	class Mod
	{
		[YamlMember(Alias = "title")]
		public string Title { get; set; }
		[YamlMember(Alias = "description")]
		public string Description { get; set; }
		[YamlMember(Alias = "assets")]
		public List<Asset> Assets { get; set; } = new List<Asset>();
	}

	class Asset
	{
		[YamlMember(Alias = "name")]
		public string Name { get; set; }
		[YamlMember(Alias = "type")]
		public string Type { get; set; }
		[YamlMember(Alias = "method")]
		public string Method { get; set; }
		[YamlMember(Alias = "source")]
		public List<Asset> Sources { get; set; }
	}

	class Settings
	{
		public Platform Platform { get; set; }
		public string Language { get; set; }
		public Mode WorldMode { get; set; }
		public Mode CharacterMode { get; set; }
		public List<string> IgnoreWorlds { get; set; }
		public List<string> IgnoreCharacters { get; set; }
	}

	enum Platform { PC, PS2 }

	enum Mode { Mix, Separate, Swap }

	class VoiceLine
	{
		public string Name { get; }
		public string World { get; }
		public string Character { get; }
		public string Replacement { get; set; }

		public VoiceLine(string name)
		{
			Name = name;
			World = name.Remove(2);
			int end = name.IndexOf('_');
			if (end == -1)
				end = name.Length;
			Character = name.Substring(end - 2, 2);
		}
	}
}
