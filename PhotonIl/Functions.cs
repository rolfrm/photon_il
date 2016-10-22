using System;
using System.Linq;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Reflection;

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
			PrintAny = gen.Sym ("print");
			If = gen.Sym ("if");
			gen.AddMacro (CreateArray, createArray);
			gen.AddMacro (ArrayCount, arrayCount);
			gen.AddMacro (ArrayAccess, arrayAccess);
			gen.AddMacro (Cast, cast);
			gen.AddMacro (If, ifmacro);
			gen.AddMacro (PrintAny, Printany);
			gen.AddMacro (gen.Sym ("defun"), defunmacro);
			gen.AddMacroSpec (gen.Sym ("defun"), defunMacroCompletions);
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
		public readonly Uid If;
		public readonly Uid PrintAny;
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
			Interact.Emit (OpCodes.Ldlen);	
			Interact.Emit (OpCodes.Conv_U4);
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
				Interact.Emit (OpCodes.Stelem, gen.GetCSType (elemType2));
				gen.SetExprs.Remove (expr);
				return gen.VoidType;
			} else {
				Interact.Emit (OpCodes.Ldelem, gen.GetCSType (elemType1));
				return elemType1;
			}
		}

		public Uid Const(object v){
			return gen.DefineConstant (gen.getPhotonType (v.GetType ()), v);
		}

		public static Uid[] getSubExpressions(Uid expr){
			
			return Interact.Current.SubExpressions.Get (expr).ToArray();
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
			Interact.Emit (Opcodes [idx]);
			return s [1];
		}

		Uid notImplemented(Uid expr){
			throw new NotImplementedException();
		}

		public static Uid ifmacro(Uid expr){
			var s = Interact.Current.SubExpressions.Get (expr);
			Uid rtype = Interact.CallOn (s [1]);
			var gen = Interact.Current;
			if (rtype == gen.VoidType)
				throw new CompilerError (expr, "if: The first argument must not evaluate to void.");
			var label = Interact.DefineLabel ();
			var endlabel = Interact.DefineLabel ();
			LocalBuilder loc = null;
			Interact.Emit (OpCodes.Brfalse, label);
			Uid r1 = Interact.CallOn (s [2]);
			if (r1 != gen.VoidType) {
				loc = Interact.DeclareLocal (gen.GetCSType (r1));
				Interact.Emit (OpCodes.Stloc, loc);
			}
			Interact.Emit (OpCodes.Br, endlabel);
			Interact.MarkLabel (label);
			Uid r2 = Interact.CallOn (s [3]);
			if (loc != null && r2 == r1) {
				Interact.Emit (OpCodes.Stloc, loc);
			}else if(r2 != gen.VoidType)
				Interact.Emit (OpCodes.Pop);
			Interact.MarkLabel (endlabel);
			if (r1 == r2 && r1 != gen.VoidType) {
				Interact.Emit (OpCodes.Ldloc, loc);
				return r1;
			}
			return gen.VoidType;
		}

		public static void PrintAny2(object obj){
			Console.Write (string.Format ("{0}", obj));
		}

		public static Uid Printany(Uid expr)
		{
			var sub = Interact.Current.SubExpressions.Get(expr);
			Uid t = Interact.CallOn (sub[1]);
			Interact.Emit (OpCodes.Dup);
			Interact.Emit (OpCodes.Box, Interact.Current.GetCSType(t));	
			Interact.Emit (OpCodes.Call, typeof(Functions).GetMethod ("PrintAny2", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
			return t;
		}

		// 4 arguments: normal function declaration.
		// 3 arguments: function forward declaration.
		public static Uid defunmacro(Uid expr){
			var sub = Interact.Current.SubExpressions.Get (expr);

			if (sub.Count != 4 && sub.Count != 3)
				throw new CompilerError (expr, "Invalid number of subexpressions for defun.");
			
			if (Interact.Current.ConstantType.Get(sub [1]) != Interact.Current.StringType)
				throw new CompilerError (expr, "Expected first argument of defun to be a string.");
			if (Interact.Current.SubExpressions.Contains (sub [2]) == false)
				throw new CompilerError (expr, "Expected the argument list of defun to be a sub expression.");
			var arglist = Interact.Current.SubExpressions.Get (sub [2]);
			int i = 0;
			foreach (var arg in arglist) {
				var argtype = Interact.Current.ArgumentType.Get (arg);
				if (argtype == Uid.Default)
					throw new CompilerError (sub [2], $"Argument '{i}' must be an argument.");
				i += 1;
			}

			Uid ret = Interact.Current.GenExpression (sub[3], arglist.ToArray());
			var fname = (string)Interact.Current.ConstantValue.Get(sub [1]);
			var fun = Interact.Current.DefineFunction (fname, ret, arglist.ToArray());
			Interact.Current.DefineFcnBody (fun, sub [3]);
			var method = Interact.Current.GenerateIL (fun);
			if (method == null)
				throw new CompilerError (expr, "Unable to generate method!");
			return Interact.Current.VoidType;
		}

		public readonly Uid ArgumentList = Uid.CreateNew();

		public static Uid defunMacroCompletions(Uid expr, int index, string suggestion){
			if (index == 1)
				return Interact.Current.StringType;
			if (index == 2)
				return Interact.Current.F.ArgumentList;
			if (index == 3)
				return Uid.Default;
			throw new CompilerError (expr, "Argument index={index} not supported.");
		}

	}
}

