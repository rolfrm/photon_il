using System;
using System.Reflection.Emit;
using System.Reflection;

namespace PhotonIl
{
	public static class Interact
	{
		public static IlGen Current;
		static ILGenerator IL;
		public static void Load(IlGen gen, ILGenerator il){
			Current = gen;
			IL = il;
		}

		public static Uid CallOn(Uid expr)
		{
			return Current.GenSubCall (expr);
		}

		public static void Emit(OpCode code){
			if(IL != null)
			IL.Emit (code);
		}

		public static void Emit(OpCode code, Type type){
			if(IL != null)
			IL.Emit (code, type);
		}

		public static void Emit(OpCode code, LocalBuilder local){
			if(IL != null)
			IL.Emit (code, local);
		}

		public static void Emit(OpCode code, Label label){
			if(IL != null)
			IL.Emit (code, label);
		}

		public static void Emit(OpCode code, FieldInfo label){
			if(IL != null)
			IL.Emit (code, label);
		}

		public static void Emit(OpCode code, MethodInfo label){
			if(IL != null)
			IL.Emit (code, label);
		}

		public static void Emit(OpCode code, float label){
			if(IL != null)
			IL.Emit (code, label);
		}
		public static void Emit(OpCode code, double label){
			if(IL != null)
			IL.Emit (code, label);
		}
		public static void Emit(OpCode code, int label){
			if(IL != null)
			IL.Emit (code, label);
		}
		public static void Emit(OpCode code, long label){
			if(IL != null)
			IL.Emit (code, label);
		}

		public static Label DefineLabel(){
			if (IL == null)
				return default(Label);
			return IL.DefineLabel ();
		}

		public static LocalBuilder DeclareLocal(Type type){
			if (IL == null)
				return null;
			return IL.DeclareLocal (type);
		}

		public static void MarkLabel(Label label){
			if (IL == null)
				return;
			IL.MarkLabel(label);
		}

	}
}

