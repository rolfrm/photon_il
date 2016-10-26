using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace PhotonIl
{
	public class ArrayModule : IPhotonModule{
		
		public Uid CreateArray { get; private set; }
		public Uid ArrayCount{ get; private set; }
		public Uid ArrayAccess{ get; private set; }
		public Uid GetSubExpressions{ get; private set; }

		public readonly Dict<Type, Uid> arrayTypes = new Dict<Type, Uid>();
		public readonly Dict<Uid, Uid> arrayElemTypes = new Dict<Uid, Uid>();
		IlGen gen;
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

		Type getArrayCsType(Uid arraytype){
			var item = arrayTypes.FirstOrDefault (x => x.Value == arraytype);
			return item.Key;
		}

		public void Load(IlGen gen){
			this.gen = gen;

			CreateArray = gen.Sym ("Create Array");
			ArrayCount = gen.Sym ("Array Count");
			ArrayAccess = gen.Sym ("Array Access");

			gen.TypeGetters.Add (getArrayCsType);

			if (gen.IsBare)
				return;

			GetSubExpressions = gen.DefineFunction("get subexpressions",ElemToArrayType(gen.UidType), gen.Arg("expr", gen.UidType));
			gen.FunctionInvocation.Add (GetSubExpressions, GetType ().GetMethod (nameof(getSubExpressions), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
			gen.AddMacro (CreateArray, createArray);
			gen.AddMacro (ArrayCount, arrayCount);
			gen.AddMacro (ArrayAccess, arrayAccess);
		}

		[ProtoBuf.ProtoContract]
		public class ArrayModuleData
		{
			[ProtoBuf.ProtoMember(1)]
			public Uid[] ArrayElemTypeKeys;
			[ProtoBuf.ProtoMember(2)]
			public Uid[] ArrayElemTypeValues;
		}

		public void LoadData(Assembly asm, object serialized)
		{
			
		}



		public object Save(){
			return null;
		}

		public static Uid[] getSubExpressions(Uid expr){
			return Interact.Current.SubExpressions.Get (expr).ToArray();
		}

		Uid createArray(Uid expr){
			List<Uid> s = gen.SubExpressions.Get (expr);
			var elemType = gen.GetCSType (s [1]);

			Uid numtype = Interact.CallOn (s [2]);
			if (gen.types.Get (numtype) != PhotonIl.Types.Primitive)
				throw new Exception ("count not a number type");
			Interact.Emit (System.Reflection.Emit.OpCodes.Newarr, elemType);

			if (!arrayTypes.ContainsKey (elemType)) {
				arrayTypes.Add (elemType, Uid.CreateNew ());
				this.arrayElemTypes.Add (arrayTypes.Get (elemType), s [1]);
			}
			gen.generatedStructs.Add (arrayTypes.Get (elemType), elemType.MakeArrayType ());
			return arrayTypes.Get (elemType);
		}

		Uid arrayCount(Uid expr){
			List<Uid> s = gen.SubExpressions.Get (expr);
			if (s.Count != 2)
				throw new Exception ();

			var arrayType = Interact.CallOn (s [1]);
			if (gen.GetCSType(arrayType).IsArray == false)
				throw new CompilerError (expr, "Argument is not an array type.");
			Interact.Emit (OpCodes.Ldlen);	
			Interact.Emit (OpCodes.Conv_U4);
			return gen.U32Type;
		}

		Uid arrayAccess(Uid expr){
			Uid valueExpr = gen.SetExprs.Get(expr);
			var s = gen.SubExpressions.Get (expr);
			var arrayType = Interact.CallOn (s [1]);
			var indexType = Interact.CallOn (s [2]);
			if (gen.types.Get (indexType) != PhotonIl.Types.Primitive)
				throw new CompilerError (expr, "Array indexer must be a primitive type.");
			var elemType1 = arrayElemTypes.Get(arrayType);

			if (valueExpr != Uid.Default) {
				var elemType2 = Interact.CallOn (valueExpr);
				if (elemType1 != elemType2)
					throw new CompilerError (expr, "Differing types in expression.");
				Interact.Emit (OpCodes.Stelem, gen.GetCSType (elemType2));
				gen.SetExprs.Remove (expr);
				return gen.VoidType;
			} else {
				Interact.Emit (OpCodes.Ldelem, gen.GetCSType (elemType1));
				return elemType1;
			}
		}
	}

}

