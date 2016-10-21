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

		static public void Test0(){
			var gen = new IlGen();
			var sub = gen.Sub;

			Uid arg, arg2;
			var fid = gen.DefineFunction ("Identity", gen.I32Type, arg = gen.DefineArgument("X", gen.I32Type), arg2 = gen.DefineArgument("Y", gen.I32Type));
			gen.DefineFcnBody (fid, sub(gen.Subtract, arg, arg2));
			MethodInfo m = gen.GenerateIL (fid);
			var x = m.Invoke (null, new object[]{ 5, 10 });
			Assert.AreEqual ((int)x, -5);
		}

        static public void Test1()
        {
            var gen = new IlGen();
            var sub = gen.Sub;
			Uid sarg1, sarg2;
            var typeId = gen.DefineStruct(gen.DefineVariable(gen.StringType, null, "vec2i"),
                sarg1 = gen.Arg("x", gen.I32Type),
                sarg2 = gen.Arg("y", gen.I32Type));
            
			var addvec2 = gen.DefineFunction("plus", gen.I32Type);
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
			
			var addvec2 = gen.DefineFunction("plus", gen.I32Type);
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
				var addvec2 = gen.DefineFunction("plus", gen.I32Type);
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
				var addvec2 = gen.DefineFunction("f", gen.I32Type);
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

			var addvec2 = gen.DefineFunction("pl µ s", gen.I32Type);
			var v = gen.DefineVariable (gen.I32Type, "test", 100);
			gen.DefineFcnBody(addvec2, sub(gen.Add, v, v));
			var m = gen.GenerateIL(addvec2);
			var blank2 = (int)m.Invoke(null, null);
			Assert.AreEqual(blank2, 100 * 2);
		}

		static Uid swapMacro(Uid expr){
			var gen = Interact.Current;
			var sub = gen.SubExpressions.Get (expr);
			if (sub.Count < 2)
				throw new Exception ("Err");
			var s = sub.Skip(1).ToArray ();
			s [sub.Count - 1 - 1] = sub [sub.Count - 2];
			s [sub.Count - 2 - 1] = sub [sub.Count - 1];
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

			var addvec2 = gen.DefineFunction("pl µ s", gen.I32Type);
			gen.DefineFcnBody(addvec2, swap);
			var m = gen.GenerateIL(addvec2);
			var blank2 = (int)m.Invoke(null, null);
			Assert.AreEqual(blank2, 32 - 12);
		}

		static public void Test6(){
			var gen = new IlGen();
			var sub = gen.Sub;
			var f = gen.DefineFunction ("test6", gen.U32Type);
			Uid arraysym = gen.Sym ("array");
			gen.DefineFcnBody (f, 
				sub (gen.Progn
					, sub (gen.Let,
					     arraysym, sub (gen.F.CreateArray, gen.I32Type, gen.F.Const (15)))
					, sub (gen.F.ArrayCount, arraysym)));
					

			var m = gen.GenerateIL(f);
			var result = m.Invoke (null, null);
			Assert.AreEqual ((uint)result, (uint)(15));
		}

		static public void Test6_2(){
			var gen = new IlGen();
			var sub = gen.Sub;
			var f = gen.DefineFunction ("test6_2", gen.U32Type);
			Uid arraysym = gen.Sym("array"), lensym = gen.Sym("length");
			gen.DefineFcnBody (f, 
				sub (gen.Progn,
					sub (gen.Let,
						arraysym, sub (gen.F.CreateArray, gen.U32Type, gen.F.Const((uint)5))),
					sub (gen.Let, lensym, sub (gen.F.ArrayCount, arraysym)),
					sub (gen.Set, sub (gen.F.ArrayAccess, arraysym, gen.F.Const(3)),
						sub (gen.Add, lensym, sub (gen.F.ArrayAccess, arraysym, gen.F.Const(3)), gen.F.Const ((uint)100)))
					,sub(gen.F.ArrayAccess, arraysym, gen.F.Const(3))));
			
			var m = gen.GenerateIL(f);
			var result = (uint)m.Invoke (null, null);
			Assert.AreEqual (result, (uint)(5 + 100));
		}

		static public void Test7(){
			// swap macro written in photon.
			var gen = new IlGen();
			var sub = gen.Sub;
			Uid arg;
			var f = gen.DefineFunction ("test7", gen.F.ElemToArrayType(gen.UidType), arg = gen.Arg("X",gen.UidType));

			Uid arraysym =gen.Sym ("array"), lensym =gen.Sym ("length"), tmpsym =gen.Sym ("tmp");
			gen.DefineFcnBody (f, 
				sub (gen.Progn
					, sub (gen.Let, arraysym, sub (gen.F.GetSubExpressions, arg))
					, sub (gen.Let, lensym, sub (gen.F.Cast, gen.I32Type, sub (gen.F.ArrayCount, arraysym))) 
					, sub (gen.Let, tmpsym, sub (gen.F.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (1))))
					, sub (gen.Set
					   , sub (gen.F.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (1)))
					   , sub (gen.F.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (2))))
					, sub (gen.Set
					   , sub (gen.F.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (2)))
						, tmpsym)
					, arraysym));
			var m = gen.GenerateIL(f);
			var subexpr = (Uid[])m.Invoke (null, new object[]{sub (f, gen.U16Type, gen.U32Type)});
			Assert.AreEqual (subexpr [1], gen.U32Type);
			Assert.AreEqual (subexpr [2], gen.U16Type);
		}

		public static int PrintInt(int x){
			Console.WriteLine ("X: {0}", x);
			return x;
		}

		static public void Test8(){
			var gen = new IlGen();
			var method = typeof(IlGenTest).GetMethod ("PrintInt", BindingFlags.Static | BindingFlags.Public);
			var print = gen.DefineFunction ("printint", gen.I32Type, gen.Arg("X", gen.I32Type));
			gen.FunctionInvocation.Add (print, method);

			var sub = gen.Sub;
			Uid arg;
			var fibid = gen.DefineFunction ("CountDown", gen.I32Type, arg = gen.Arg("X", gen.I32Type));
			gen.DefineFcnBody (fibid, sub(gen.F.If, arg, sub(fibid, sub(print, sub(gen.Subtract, arg, gen.DefineConstant(gen.I32Type, 1)))), arg));
			MethodInfo m = gen.GenerateIL (fibid);
			var x = m.Invoke (null, new object[]{5});// recursive.
			Assert.AreEqual ((int)x, 0);
		}

		static public void Test9(){
			var gen = new IlGen();
			var f = gen.DefineFunction ("CodeBuilderTest", gen.F64Type);
			var cb = new CodeBuilder (gen, f);
			cb.PushArgument();
			cb.SetString ("+");
			cb.PushArgument ();
			{
				cb.CreateSub ();
				cb.Enter ();
				cb.PushArgument ();
				cb.SetString ("+");
				var opt3 = cb.GetOptions ().First ();
				cb.SelectOption (opt3);
				cb.PushArgument ();
				cb.SetString ("2.1");
				var opt = cb.GetOptions ().First (option => option == gen.F64Type);
				cb.SelectOption (opt);
				cb.PushArgument ();
				cb.SetString ("3.2");
				var opt2 = cb.GetOptions ().First (option => option == gen.F64Type);
				cb.SelectOption (opt2);
				cb.Exit ();
			}
			{
				cb.PushArgument ();
				cb.SetString ("9.5");
				var opt2 = cb.GetOptions ().First (option => option == gen.F64Type);
				cb.SelectOption (opt2);
				cb.SelectedIndex = 0;
				var opt3 = cb.GetOptions ().First ();
				cb.SelectOption (opt3);
			}
			var method = gen.GenerateIL (f);
			var result = (double)method.Invoke (null, null);
			Assert.AreEqual (result, 5.3 + 9.5);
		}

		static public void Test10(){
			var gen = new IlGen();
			var sub = gen.Sub;
			Uid type = gen.I32Type;
			Uid arg = gen.Arg ("X", type);;
			Uid rt = gen.GenExpression(sub(gen.F.If, gen.DefineConstant(type, 1),
				sub(gen.Subtract, arg, gen.DefineConstant(type, 1)), arg), arg);
			Assert.AreEqual (rt, type);
		}

		static public void Test11(){
			var gen = new IlGen();
			var sub = gen.Sub;
			var fibid = gen.DefineFunction ("test", gen.VoidType);
			gen.DefineFcnBody (fibid, sub (gen.Add, gen.DefineConstant(gen.F64Type, Math.PI)));
			MethodInfo m = gen.GenerateIL (fibid);
			m.Invoke (null, null);
		}

		static public void Test12(){
			var gen = new IlGen();
			var sub = gen.Sub;
			var cb = new CodeBuilder (gen);
			cb.PushArgument ("defun");
			cb.PushArgument ("testfcn", gen.StringType);
			cb.PushArgument ();
			cb.CreateSub ();
			cb.Enter ();
			cb.PushArgument ("X");
			cb.Exit ();
			cb.PushArgument ("X");
			cb.BuildAndRun ();
			var fcn = gen.FunctionName.First (x => x.Value == "testfcn").Key;
			MethodInfo m = gen.FunctionInvocation.Get (fcn);
			Assert.IsTrue (m != null);
			Assert.AreEqual ((int)m.Invoke (null, new Object []{5}), 5);
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
		public static void IsTrue(bool value){
			if(!value)
				throw new AssertionFailedException();
		}
    }
}

