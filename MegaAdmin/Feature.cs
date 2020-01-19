namespace MegaAdmin
{
	public abstract class Feature
	{
		public abstract Server Server { get; set; }
		public abstract string ID { get; }
		public abstract string Name { get; }
		public abstract string Version { get; }
		public abstract string Author { get; }
		public abstract string Description { get; }
		public abstract void Init();
	}
}
