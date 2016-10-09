using System;

namespace IlTest
{
	public struct MyStruct {
		public int X, Y;
	}

	public static class MyClass
	{

        public static int G = 5;

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
			int x = G;
			int y = G;
			Test2(Op(x,y));
		}

        public static void Test4()
        {
            MyStruct s = new MyStruct();
            s.X = 5;
            s.Y = 10;
            Console.WriteLine("{0} {1}", s.X, s.Y);
        }

		public static void ArrayTest(){
			var array = new int[10];
			unsafe{
				fixed(int* ptr = array) {
					for (int i = 0; i < array.Length; i++) {
						ptr [0] += ptr [i];
					}
				}
			}
			Console.WriteLine ("{0}", array [0]);
		}

		public unsafe static void ArrayTest2(){
			int * array = stackalloc int[10];

			Console.WriteLine ("{0}", array [0]);
		}

		public static void ArrayTest3(){
			var array = new int[10];
			Console.Write ("{0}", array [0]);
			Console.Write ("{0}", array.Length);
				
		}
	}
}

