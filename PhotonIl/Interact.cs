using System;
using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Generic;

namespace PhotonIl
{
	public static class Interact
	{
		public static List<Uid> CurrentArgs = new List<Uid> ();
		public static IlGen Current { get { return data.Value.Item1; } }
		static ILGenerator IL { get { return data.Value.Item2; } }

        public static bool IsDryRun { get { return IL == null; } }

        static StackLocal<Tuple<IlGen, ILGenerator>> data = new StackLocal<Tuple<IlGen, ILGenerator>>(Tuple.Create<IlGen, ILGenerator>(null, null));

		public static IDisposable Push(IlGen gen, ILGenerator il){
            return data.WithValue(Tuple.Create(gen, il));
		}
        
		public static Uid CallOn(Uid expr)
		{
			return Current.CompileSubExpression (expr);
		}

		public static void Emit(OpCode code){
			if(IL != null)
			IL.Emit (code);
		}

		public static void Emit(OpCode code, string str){
			if(IL != null)
				IL.Emit (code, str);
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

