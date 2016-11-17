using System;
using System.Reflection.Emit;


namespace PhotonIl
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			run_tests();
		}

		static void run_tests(){
			try
			{
				IlGenTest.Test0();
				IlGenTest.Test1();
				IlGenTest.Test2();
				IlGenTest.Test3();
				IlGenTest.FunctionWithGlobalVar();
				IlGenTest.SwapMacro();
				IlGenTest.ArrayCreation();
				IlGenTest.ArrayAccess();
				IlGenTest.MacroMethod();
				IlGenTest.RecursiveMethod();
				IlGenTest.BuildSimpleAddition();
				IlGenTest.IfWithArgReturnType();
				IlGenTest.VoidFunctionWithCalculation();
				IlGenTest.TestCodeBuildWithDefun();
				IlGenTest.TestSaveFunctionsInWorkspace();
				IlGenTest.TestLoadBaseWorkspace();
				IlGenTest.TestLoadingWithDependencies();
                IlGenTest.TestIncrementalSave();
				Console.WriteLine("Passed");
			}
			catch(Exception e)
			{
				Console.WriteLine("Test failed");
				Console.WriteLine(e.Message);
				if(e.InnerException != null)
					Console.WriteLine (e.InnerException.Message);
			}
		}
	}
}
