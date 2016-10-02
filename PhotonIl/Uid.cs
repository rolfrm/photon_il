using System;

namespace PhotonIl
{
	public struct Uid : IEquatable<Uid>
	{
		static int _id = 1;

		public static readonly Uid Default = new Uid(0);

		Uid(int id ){
			Id = id;
		}

		public static Uid CreateNew ()
		{
			return new Uid(_id++);
		}

		public readonly int Id;

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
			return a.Id == b.Id;
		}

		public static bool operator!= (Uid a, Uid b)
		{
			return a.Id != b.Id;
		}

		public override int GetHashCode ()
		{
			return Id.GetHashCode ();
		}

		public override string ToString ()
		{
			return string.Format ("[Uid {0}]", Id);
		}
	}
}

