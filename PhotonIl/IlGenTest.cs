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
			Uid sarg1;
            var typeId = gen.DefineStruct(gen.DefineVariable(gen.StringType, null, "vec2i"),
                sarg1 = gen.Arg("x", gen.I32Type), gen.Arg("y", gen.I32Type));
            
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
					     arraysym, sub (gen.A.CreateArray, gen.I32Type, gen.F.Const (15)))
					, sub (gen.A.ArrayCount, arraysym)));
					

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
						arraysym, sub (gen.A.CreateArray, gen.U32Type, gen.F.Const((uint)5))),
					sub (gen.Let, lensym, sub (gen.A.ArrayCount, arraysym)),
					sub (gen.Set, sub (gen.A.ArrayAccess, arraysym, gen.F.Const(3)),
						sub (gen.Add, lensym, sub (gen.A.ArrayAccess, arraysym, gen.F.Const(3)), gen.F.Const ((uint)100)))
					,sub(gen.A.ArrayAccess, arraysym, gen.F.Const(3))));
			
			var m = gen.GenerateIL(f);
			var result = (uint)m.Invoke (null, null);
			Assert.AreEqual (result, (uint)(5 + 100));
		}

		static public void Test7(){
			// swap macro written in photon.
			var gen = new IlGen();
			var sub = gen.Sub;
			Uid arg;
			var f = gen.DefineFunction ("test7", gen.A.ElemToArrayType(gen.UidType), arg = gen.Arg("X",gen.UidType));

			Uid arraysym =gen.Sym ("array"), lensym =gen.Sym ("length"), tmpsym =gen.Sym ("tmp");
			gen.DefineFcnBody (f, 
				sub (gen.Progn
					, sub (gen.Let, arraysym, sub (gen.A.GetSubExpressions, arg))
					, sub (gen.Let, lensym, sub (gen.F.Cast, gen.I32Type, sub (gen.A.ArrayCount, arraysym))) 
					, sub (gen.Let, tmpsym, sub (gen.A.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (1))))
					, sub (gen.Set
					   , sub (gen.A.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (1)))
					   , sub (gen.A.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (2))))
					, sub (gen.Set
					   , sub (gen.A.ArrayAccess, arraysym, sub (gen.Subtract, lensym, gen.F.Const (2)))
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
			var currentName = cb.StringOf (cb.CurrentExpression);
			Assert.IsTrue (string.IsNullOrWhiteSpace (currentName) == false);
			cb.PushArgument ("Y");
			cb.Exit ();
			Assert.AreEqual (gen.SubExpressions.Get (cb.CurrentExpression).Count, 2);
			cb.PushArgument ();
			cb.CreateSub ();
			cb.Enter ();
			cb.PushArgument ("+");
			cb.PushArgument ("X");
			cb.PushArgument ("Y");
			cb.Exit ();
			cb.BuildAndRun ();
			var fcn = gen.FunctionName.First (x => x.Value == "testfcn").Key;
			MethodInfo m = gen.FunctionInvocation.Get (fcn);
			Assert.IsTrue (m != null);
			object result = m.Invoke (null, new Object[]{ (byte)5, (byte)11 });
			Assert.AreEqual ((byte)result, (byte)16);
		}

		static public void Test13(){
			Type dec1, dec2, t1, t2;
			{ // Part 1. generate a simple function.
				var gen = new IlGen ();
				var sub = gen.Sub;
				{
					var pi_test_id = gen.DefineFunction ("pi_test", gen.F64Type);
					gen.DefineFcnBody (pi_test_id, gen.DefineConstant (gen.F64Type, Math.PI));
					gen.GenerateIL (pi_test_id);
					var m = gen.FunctionInvocation.Get (pi_test_id);
					var result = m.Invoke (null, null);
					dec1 = m.DeclaringType;
					Assert.AreEqual (result, Math.PI);
				}
				{
					var pi_test_id = gen.DefineFunction ("pi_test2", gen.F64Type);
					gen.DefineFcnBody (pi_test_id, gen.DefineConstant (gen.F64Type, Math.PI));
					gen.GenerateIL (pi_test_id);
					var m = gen.FunctionInvocation.Get (pi_test_id);
					var result = m.Invoke (null, null);
					Assert.AreEqual (result, Math.PI);
				}

				{
					var typeId = gen.DefineStruct(gen.DefineVariable(gen.StringType, null, "vec2i"),gen.DefineArgument("x", gen.I32Type), gen.DefineArgument("y", gen.I32Type));
					var cstype = gen.GetCSType (typeId);
					t1 = cstype;
				}
				gen.Save ("Workspace1.bin");
			}

			{
				var gen = new IlGen ();
				gen.Load ("Workspace1.bin");
				System.IO.File.Delete ("Workspace1.bin");
				var pi_test_id = gen.FunctionName.First (f => f.Value == "pi_test").Key;
				var m = gen.FunctionInvocation.Get (pi_test_id);
				var result = m.Invoke (null, null);
				Assert.AreEqual (result, Math.PI);
				dec2 = m.DeclaringType;
			}
			bool equals = dec1 == dec2;
			Console.WriteLine ($"equals? {equals}");

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

