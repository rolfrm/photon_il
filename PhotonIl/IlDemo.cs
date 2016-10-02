using System.Reflection.Emit;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;


public class IlTest{
	public static void GoTest(){

		var sw = Stopwatch.StartNew();
		DynamicMethod squareIt = new
			DynamicMethod(
				"SquareIt", 
				typeof(long), 
				new []{typeof(int)}, 
				typeof(IlTest).Module);
		var il = squareIt.GetILGenerator();
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Conv_I8);
		il.Emit(OpCodes.Dup);
		il.Emit(OpCodes.Mul);
		il.Emit(OpCodes.Ret);
		for(int i = 0; i < 1; i++){
			Func<int, long> f = (Func<int, long>)squareIt.CreateDelegate(typeof(Func<int,long>));
			Console.WriteLine(f(5).ToString());
		}
		Console.WriteLine(sw.Elapsed);
	}
}
