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
				IlGenTest.Test4();
				IlGenTest.Test5();
				IlGenTest.Test6();
				IlGenTest.Test6_2();
				IlGenTest.Test7();
				IlGenTest.Test8();
				IlGenTest.Test9();
				IlGenTest.Test10();
				IlGenTest.Test11();
				IlGenTest.Test12();
				IlGenTest.Test13();
				Console.WriteLine("Passed");
			}
			catch(Exception e)
			{
				Console.WriteLine("Test failed");
				Console.WriteLine (e.InnerException.Message);
			}
		}
	}
}
