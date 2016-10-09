using System;
using System.Linq;
using System.Reflection.Emit;

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
			gen.AddMacro (CreateArray, createArray);
			gen.AddMacro (ArrayCount, arrayCount);
			gen.AddMacro (ArrayAccess, arrayAccess);
			gen.TypeGetters.Add (getCSType);
		}

		Type getCSType(Uid arraytype){
			return arrayTypes.FirstOrDefault (x => x.Value == arraytype).Key;
		}

		public readonly Uid CreateArray;
		public readonly Uid ArrayCount;
		public readonly Uid ArrayAccess;

		public readonly Dict<Type, Uid> arrayTypes = new Dict<Type, Uid>();
		public readonly Dict<Uid, Uid> arrayElemTypes = new Dict<Uid, Uid>();

		public Uid getArrayType(Uid photonType){
			var type = gen.GetCSType(photonType);
			if (!arrayTypes.ContainsKey (type))
				arrayTypes.Add (type, Uid.CreateNew ());
			return arrayTypes.Get (type);
		}

		Uid createArray(Uid expr){
			Uid[] s = gen.SubExpressions.Get (expr);
			var elemType = gen.GetCSType (s [1]);

			Uid numtype = Interact.CallOn (s [2]);
			if (gen.types.Get (numtype) != Types.Primitive)
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



		Uid notImplemented(Uid expr){
			throw new NotImplementedException();
		}
	}
}

