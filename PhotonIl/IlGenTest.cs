using System.Reflection.Emit;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

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

		static public IlGen Run2(){
			var gen = new IlGen ();

			var sub = gen.Sub;

			Uid sarg1, sarg2;
			var typeId = gen.DefineStruct (gen.DefineVariable(gen.StringType, null , "vec2i"), 
				sarg1 = gen.DefineArgument ("x", gen.I32Type), 
				sarg2 = gen.DefineArgument ("y", gen.I32Type));

            var bareStruct = gen.DefineFunction("<>", gen.GetFunctionType(typeId));
			gen.DefineFcnBody(bareStruct,sub(gen.GetStructConstructor(typeId)));
            
            var fcnType = gen.GetFunctionType(gen.I32Type);
            var addvec2 = gen.DefineFunction("plus", fcnType);
			Uid sym = gen.Sym("x");

			gen.DefineFcnBody (addvec2, sub (gen.Progn,
				               sub (gen.Let, sym, sub (gen.GetStructConstructor (typeId))),
				               sub (gen.Set, gen.GetStructAccessor (sarg1, sym), gen.DefineVariable (gen.I32Type, null, 5))
				, gen.GetStructAccessor (sarg2, sym)
			               ));
            
            var m = gen.GenerateIL(addvec2);
            m.Invoke(null, null);
			return gen;
		}

        static public void Test1()
        {
            var gen = new IlGen();
            var sub = gen.Sub;
			Uid sarg1, sarg2;
            var typeId = gen.DefineStruct(gen.DefineVariable(gen.StringType, null, "vec2i"),
                sarg1 = gen.DefineArgument("x", gen.I32Type),
                sarg2 = gen.DefineArgument("y", gen.I32Type));
            
            var fcnType = gen.GetFunctionType(gen.I32Type);
            var addvec2 = gen.DefineFunction("plus", fcnType);
			Uid sym = gen.Sym("x");
			gen.DefineFcnBody(addvec2, sub(gen.Progn
				,sub(gen.Let, sym, sub(gen.GetStructConstructor(typeId)))
				,sub(gen.Set, gen.GetStructAccessor(sarg1, sym), gen.DefineVariable(gen.I32Type, null, 5))
				,gen.GetStructAccessor(sarg1, sym)
			));
            
            var m = gen.GenerateIL(addvec2);
            var blank2 = (int)m.Invoke(null, null);
            Assert.AreEqual(blank2, 5);
        }

        static public void Test2()
        {
            var gen = new IlGen();
            var sub = gen.Sub;
            Uid sarg1, sarg2;
            var typeId = gen.DefineStruct(gen.DefineVariable(gen.StringType, null, "vec2i"),
                sarg1 = gen.DefineArgument("x", gen.I32Type),
                sarg2 = gen.DefineArgument("y", gen.I32Type));
			
            var fcnType = gen.GetFunctionType(gen.I32Type);
            var addvec2 = gen.DefineFunction("plus", fcnType);
			Uid sym = gen.Sym("x");
			gen.DefineFcnBody(addvec2, sub(gen.Progn
				, sub(gen.Let, sym, sub(gen.GetStructConstructor(typeId)))
				, sub(gen.Set, gen.GetStructAccessor(sarg1, sym), gen.DefineConstant(gen.I32Type, 5))
				, sub(gen.Set, gen.GetStructAccessor(sarg2, sym), gen.DefineConstant(gen.I32Type, 13))
				, sub(gen.Add, gen.GetStructAccessor(sarg1, sym), gen.GetStructAccessor(sarg2, sym))
			));
            
            var m = gen.GenerateIL(addvec2);
            var blank2 = (int)m.Invoke(null, null);
            Assert.AreEqual(blank2, 5 + 13);
        }

        static public void Test3()
        {
            var gen = new IlGen();
            var sub = gen.Sub;
            Uid sarg1, sarg2;
            var typeId = gen.DefineStruct(gen.DefineVariable(gen.StringType, null, "vec2i"),
                sarg1 = gen.DefineArgument("x", gen.I32Type),
                sarg2 = gen.DefineArgument("y", gen.I32Type));

            Uid f1 = Uid.Default;
            {
                var fcnType = gen.GetFunctionType(gen.I32Type);
                var addvec2 = gen.DefineFunction("plus", fcnType);
				Uid sym = gen.Sym("x");

				gen.DefineFcnBody (addvec2, sub (gen.Progn
					, sub (gen.Let, sym, sub (gen.GetStructConstructor (typeId)))
					, sub (gen.Set, gen.GetStructAccessor (sarg1, sym), gen.DefineConstant (gen.I32Type, 5))
					, sub (gen.Set, gen.GetStructAccessor (sarg2, sym), gen.DefineConstant (gen.I32Type, 13))
					, sub (gen.Add, gen.GetStructAccessor (sarg1, sym), gen.GetStructAccessor (sarg2, sym))
				));
                
                var m = gen.GenerateIL(addvec2);
                var blank2 = (int)m.Invoke(null, null);
                Assert.AreEqual(blank2, 5 + 13);
                f1 = addvec2;
            }
            {
                var fcnType = gen.GetFunctionType(gen.I32Type);
                var addvec2 = gen.DefineFunction("f", fcnType);
				gen.DefineFcnBody(addvec2, sub(gen.Add, sub(f1), sub(f1), sub(f1), sub(f1)));
                var m = gen.GenerateIL(addvec2);
                var blank2 = (int)m.Invoke(null, null);
                Assert.AreEqual(blank2, 4 * (5 + 13));
            }
        }

		static public void Test4()
		{
			var gen = new IlGen();
			var sub = gen.Sub;

			var fcnType = gen.GetFunctionType(gen.I32Type);
			var addvec2 = gen.DefineFunction("pl µ s", fcnType);
			var v = gen.DefineVariable (gen.I32Type, "test", 100);
			gen.DefineFcnBody(addvec2, sub(gen.Add, v, v));
			var m = gen.GenerateIL(addvec2);
			var blank2 = (int)m.Invoke(null, null);
			Assert.AreEqual(blank2, 100 * 2);
		}

		static Uid swapMacro(IlGen gen, Uid expr){
			var sub = gen.SubExpressions.Get (expr);
			if (sub.Length < 2)
				throw new Exception ("Err");
			var s = sub.Skip(1).ToArray ();
			s [sub.Length - 1 - 1] = sub [sub.Length - 2];
			s [sub.Length - 2 - 1] = sub [sub.Length - 1];
			return gen.Sub (s);
		}

		static public void Test5(){
			var gen = new IlGen();
			var swapid = Uid.CreateNew ();
			gen.AddMacro (swapid, swapMacro);
			var sub = gen.Sub;
			var c12 = gen.DefineConstant (gen.I32Type, 12);
			var c32 = gen.DefineConstant (gen.I32Type, 32);
			var swap = sub (swapid, gen.Subtract, c12, c32); // 12 - 32 -> 32 - 12.

			var fcnType = gen.GetFunctionType(gen.I32Type);
			var addvec2 = gen.DefineFunction("pl µ s", fcnType);
			gen.DefineFcnBody(addvec2, swap);
			var m = gen.GenerateIL(addvec2);
			var blank2 = (int)m.Invoke(null, null);
			Assert.AreEqual(blank2, 32 - 12);
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

			var result = gen.GenerateIL (expr2);
            result.Invoke(null, null);
			return gen;
		}
	}

    public class Assert
    {
        public class AssertionFailedException : Exception
        {
            public AssertionFailedException() : base("Assertion failed")
            {

            }
        }
        public static void AreEqual<T>(T actual, T expected)
        {
            if(!actual.Equals(expected))
                throw new AssertionFailedException();
        }
    }
}

