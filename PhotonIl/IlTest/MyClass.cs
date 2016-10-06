using System;

namespace IlTest
{
	public struct MyStruct {
		public int X, Y;
	}

	public static class MyClass
	{
		public static void Test2 (object o){
			Console.WriteLine(o.ToString());
		}

		public static int Op(int x, int y){
			return x + y * 2;
		}

		static int X = 5;
		public static void Test1 ()
		{
			MyStruct s = new MyStruct ();

			s.Y = Op(X, 4);
		    s.X = Op(X, 5);
			var Y3 = Op(X, 6);
			var Y4 = Op(Y3, 5);
			var Y5 = Op(Y4, 1);
			Test2 (Op(Y5, Op(Y3, Op(s.Y, s.X))));
		}


		public static void Test3 ()
		{
			int x = 5;
			int y = 10;
			Test2(Op(x,y));
		}

        public static void Test4()
        {
            MyStruct s = new MyStruct();
            s.X = 5;
            s.Y = 10;
            Console.WriteLine("{0} {1}", s.X, s.Y);
        }
	}
}

