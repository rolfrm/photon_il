using System;
using System.Linq;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace PhotonIl
{
	public class Functions
	{
		IlGen gen;
		public Functions(IlGen gen){
			this.gen = gen;
			CreateArray = gen.Sym ("Create Array");
			ArrayCount = gen.Sym ("Array Count");
			ArrayAccess = gen.Sym ("Array Access");
			Cast = gen.Sym ("Cast");
			gen.AddMacro (CreateArray, createArray);
			gen.AddMacro (ArrayCount, arrayCount);
			gen.AddMacro (ArrayAccess, arrayAccess);
			gen.AddMacro (Cast, cast);
			gen.TypeGetters.Add (getCSType);

			GetSubExpressions = gen.DefineFunction("get subexpressions",ElemToArrayType(gen.UidType), gen.Arg("expr", gen.UidType));
			gen.FunctionInvocation.Add (GetSubExpressions, GetType ().GetMethod ("getSubExpressions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
		}

		Type getCSType(Uid arraytype){
			var item = arrayTypes.FirstOrDefault (x => x.Value == arraytype);
			return item.Key;
		}

		public readonly Uid CreateArray;
		public readonly Uid ArrayCount;
		public readonly Uid ArrayAccess;
		public readonly Uid GetSubExpressions;
		public readonly Uid Cast;

		public readonly Dict<Type, Uid> arrayTypes = new Dict<Type, Uid>();
		public readonly Dict<Uid, Uid> arrayElemTypes = new Dict<Uid, Uid>();

		public Uid ElemToArrayType(Uid photonType){
			var type = gen.GetCSType(photonType);
			if (!arrayTypes.ContainsKey (type)) {
				Uid id;
				arrayTypes.Add (type, id = Uid.CreateNew ());
				this.arrayElemTypes.Add (id, photonType); 
				gen.generatedStructs.Add (id, type.MakeArrayType ());
			}
			return arrayTypes.Get (type);
		}

		Uid createArray(Uid expr){
			Uid[] s = gen.SubExpressions.Get (expr);
			var elemType = gen.GetCSType (s [1]);

			Uid numtype = Interact.CallOn (s [2]);
			if (gen.types.Get (numtype) != PhotonIl.Types.Primitive)
				throw new Exception ("count not a number type");
			Interact.IL.Emit (System.Reflection.Emit.OpCodes.Newarr, elemType);

			if (!arrayTypes.ContainsKey (elemType)) {
				arrayTypes.Add (elemType, Uid.CreateNew ());
				this.arrayElemTypes.Add (arrayTypes.Get (elemType), s [1]);
			}
			gen.generatedStructs.Add (arrayTypes.Get (elemType), elemType.MakeArrayType ());
			return arrayTypes.Get (elemType);
		}

		Uid arrayCount(Uid expr){
			Uid[] s = gen.SubExpressions.Get (expr);
			if (s.Length != 2)
				throw new Exception ();
			
			var arrayType = Interact.CallOn (s [1]);
			Interact.IL.Emit (OpCodes.Ldlen);	
			Interact.IL.Emit (OpCodes.Conv_U4);
			return gen.U32Type;
		}

		Uid arrayAccess(Uid expr){
			Uid valueExpr = gen.SetExprs.Get(expr);
			var s = gen.SubExpressions.Get (expr);
			var arrayType = Interact.CallOn (s [1]);
			var indexType = Interact.CallOn (s [2]);
			var elemType1 = arrayElemTypes.Get(arrayType);

			if (valueExpr != Uid.Default) {
				var elemType2 = Interact.CallOn (valueExpr);
				if (elemType1 != elemType2)
					throw new CompilerError (expr, "Differing types in expression.");
				Interact.IL.Emit (OpCodes.Stelem, gen.GetCSType (elemType2));
				gen.SetExprs.Remove (expr);
				return gen.VoidType;
			} else {
				Interact.IL.Emit (OpCodes.Ldelem, gen.GetCSType (elemType1));
				return elemType1;
			}
		}

		public Uid Const(object v){
			return gen.DefineConstant (gen.getPhotonType (v.GetType ()), v);
		}

		public static Uid[] getSubExpressions(Uid expr){
			
			return Interact.Current.SubExpressions.Get (expr);
		}

		static Type[] Types = new Type[]
		{typeof(SByte), typeof(Int16), typeof(Int32), typeof(Int64),
			typeof(byte),typeof(UInt16), typeof(UInt32), typeof(UInt64), typeof(float), typeof(double)};
		static OpCode[] Opcodes = new []{OpCodes.Conv_I1, OpCodes.Conv_I2,OpCodes.Conv_I4, OpCodes.Conv_I8,
			OpCodes.Conv_U1, OpCodes.Conv_U2, OpCodes.Conv_U4, OpCodes.Conv_U8, OpCodes.Conv_R4, OpCodes.Conv_R8};

		public static Uid cast(Uid expr){
			var s = Interact.Current.SubExpressions.Get (expr);
			var type = Interact.Current.GetCSType (s [1]);
			Uid rtype = Interact.CallOn (s [2]);
			int idx = Array.IndexOf (Types, type);
			if (idx == -1)
				throw new CompilerError (expr, "Unable to cast from {0} to {1}", Interact.Current.GetCSType (rtype), Interact.Current.GetCSType (s [1])); 
			Interact.IL.Emit (Opcodes [idx]);
			return s [1];
		}

		Uid notImplemented(Uid expr){
			throw new NotImplementedException();
		}
	}
}

