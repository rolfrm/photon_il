using System;

namespace PhotonIl
{
	[ProtoBuf.ProtoContract]
	public struct Uid : IEquatable<Uid>
	{
		static int _id = 1;


		public static readonly Uid Default = new Uid(0);

		Uid(int id ){
			Id = id;
			AssemblyId = 0;
		}

		public static Uid CreateNew ()
		{
			return new Uid(_id++);
		}

		[ProtoBuf.ProtoMember(1)]
		public readonly int Id;

		public int AssemblyId; 

		public static Dict<int,int> LoadAssemblyTranslation = new Dict<int, int> ();

		[ProtoBuf.ProtoAfterDeserializationAttribute]
		public void WasDeserialized(){
			
			AssemblyId = LoadAssemblyTranslation.Get(AssemblyId);
		}

		public bool Equals (Uid other)
		{
			return other.Id == Id;
		}

		public override bool Equals (object obj)
		{
			if (obj is Uid) {
				return this == ((Uid)obj);
			}
			return false;
		}

		public static bool operator== (Uid a, Uid b)
		{
			return a.Id == b.Id && a.AssemblyId == b.AssemblyId;
		}

		public static bool operator!= (Uid a, Uid b)
		{
			return a.Id != b.Id || a.AssemblyId != b.AssemblyId;
		}

		public override int GetHashCode ()
		{
			return Id.GetHashCode () ^ AssemblyId.GetHashCode ();
		}

		public override string ToString ()
		{
			return string.Format ("[Uid {0}]", Id);
		}
	}
}

