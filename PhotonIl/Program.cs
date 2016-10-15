using System;


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
				/*IlGenTest.Test0();
				IlGenTest.Test1();
				IlGenTest.Test2();
				IlGenTest.Test3();
				IlGenTest.Test4();
				IlGenTest.Test5();
				IlGenTest.Test6();
				IlGenTest.Test6_2();
				IlGenTest.Test7();*/
				//IlGenTest.Test8();
				IlGenTest.Test9();

				Console.WriteLine("Passed");
			}
			catch
			{
				Console.WriteLine("Test failed");
			}
		}

	}
}
