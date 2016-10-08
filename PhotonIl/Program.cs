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
				IlGenTest.Test4();
				IlGenTest.Test3();
				IlGenTest.Test1();
				IlGenTest.Test2();
				Console.WriteLine("Passed");
			}
			catch
			{
				Console.WriteLine("Test failed");
			}
		}

	}
}
