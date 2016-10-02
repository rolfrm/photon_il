using System.Reflection.Emit;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


namespace PhotonIl
{
	public class IlGenTest
	{
		public static void printValue (byte value)
		{
			System.Console.WriteLine ("Value: {0}", value);
		}

		public static byte add(byte x, byte y){
			return (byte)(x + y);
		}

		public static Uid MacroExample(IlGen gen, Uid expr)
		{ // swaps the last two args.
			var subex = gen.SubExpressions.Get (expr).ToArray();
			if (subex.Length < 3)
				return expr;

			var last = subex [subex.Length - 1];
			subex [subex.Length - 1] = subex [subex.Length - 2];
			subex [subex.Length - 2] = last;

			Uid expr2 = gen.CreateExpression (subex [0]);

			return expr2;
		}

		/*public static void IlMacroExample(IlGen gen, Uid expr, ILGenerator il){

			var subexprs = gen.SubExpressions.Get (expr);
			if (subexprs.Length <= 1)
				throw new Exception ("Invalid expression");
			var function = subexprs [1];

			var mt = gen.variableType.Get (function);
			var args = gen.FunctionArgTypes.Get (mt);
			var returnType = gen.FunctionReturnType.Get (mt);

			if (args.Length != subexprs.Length - 2)
				throw new Exception ("Unsupported number of arguments");

			LocalBuilder[] stlocs = new LocalBuilder[subexprs.Length - 2];
			for (int i = 2; i < subexprs.Length; i++) {
				using (var item = gen.ExpectedType.WithValue (args [i - 2])) {

					Uid type = gen.GenSubCall (subexprs [i], il);
					if (type != args [i - 2])
						throw new CompilerError (subexprs [i], "Invalid type of arg {0}. Expected {1}, got {2}.", i - 2, args [i - 2], type);
					stlocs [i - 2] = il.DeclareLocal (gen.getNetType (type));	
					il.Emit (OpCodes.Stloc, stlocs [i - 2]);
				}
			}

			foreach (var loc in stlocs)
				il.Emit (OpCodes.Ldloc, loc);
			il.Emit (OpCodes.Add);

			//return returnType;
		}

		public static Uid DefineStruct(IlGen gen, Uid[] exprs){
			//exprs[1] name
			//exprs[1 + i * 2] // menber
			//exprs[1 + i * 2 + 1] // member type
			Debug.Assert(exprs.Length > 1);
			gen.variableName.Get (exprs [1]);

		}*/

		static public IlGen Run2(){
			var gen = new IlGen ();

			var sub = gen.Sub;

			Uid sarg1, sarg2;
			var typeId = gen.DefineStruct (gen.DefineVariable(gen.StringType, null , "vec2i"), 
				sarg1 = gen.DefineArgument ("x", gen.I32Type), 
				sarg2 = gen.DefineArgument ("y", gen.I32Type));
			var v1 = gen.DefineVariable (typeId, "vec2Test");
			var fcnType = gen.GetFunctionType (typeId, typeId, typeId);
			Uid arg1, arg2;
			var addvec2 = gen.DefineFunction ("+", fcnType, arg1 = gen.DefineArgument("a"), arg2 = gen.DefineArgument("b"));
			var subexpr1 = gen.DefineFcnBody (addvec2);
			Uid xlocal, ylocal;
			gen.AddSubExpression (subexpr1, sub (gen.Progn, 
				xlocal = gen.Let ("x", sub (gen.Add, sub (gen.GetStructAccessor (sarg1), arg1), sub (gen.GetStructAccessor (sarg1), arg2))),
				ylocal = gen.Let ("y", sub (gen.Add, sub (gen.GetStructAccessor (sarg2), arg1), sub (gen.GetStructAccessor (sarg2), arg2))),
				sub (gen.GetStructConstructor (typeId), xlocal, ylocal)));
			return gen;
		}

		static public IlGen Run ()
		{
			var gen = new IlGen ();
			var v1 = gen.DefineVariable (gen.U8Type, "test", (byte)55);
			Uid ftype = gen.DefineFunctionType ();
			gen.AddFunctionArg (ftype, gen.U8Type);

			Uid ftype2 = gen.DefineFunctionType ();
			gen.AddFunctionArg (ftype2, gen.U8Type);
			gen.AddFunctionArg (ftype2, gen.U8Type);
			gen.FunctionReturnType.Add (ftype2, gen.U8Type);

			var f2 = gen.DefineFunction ("add", ftype2);
			gen.FunctionInvocation.Add (f2, typeof(IlGenTest).GetMethod ("add", BindingFlags.Static | BindingFlags.Public));

			var f1 = gen.DefineFunction ("printValue", ftype);

			var method = typeof(IlGenTest).GetMethod ("printValue", BindingFlags.Static | BindingFlags.Public);
			Debug.Assert (method != null);
			gen.FunctionInvocation.Add (f1, method);
			var expr = gen.CreateExpression (IlGen.CallExpression);
			gen.AddSubExpression (expr, f2);
			gen.AddSubExpression (expr, v1);
			gen.AddSubExpression (expr, v1);

			var expr2 = gen.CreateExpression (IlGen.CallExpression);
			gen.AddSubExpression (expr2, f1);
			gen.AddSubExpression (expr2, expr);

			gen.GenerateIL (expr2);
			return gen;
		}
	}
}

