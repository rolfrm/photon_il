using System;
using System.Reflection.Emit;

namespace PhotonIl
{
	public static class Interact
	{
		public static IlGen Current;
		public static ILGenerator IL;
		public static void Load(IlGen gen, ILGenerator il){
			Current = gen;
			IL = il;
		}

		public static Uid CallOn(Uid expr)
		{
			return Current.GenSubCall (expr, IL);
		}

	}
}

