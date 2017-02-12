using System;
using System.Runtime.Remoting.Messaging;
using System.Collections.Generic;

namespace PhotonIl2
{
	using System.Threading;

	internal class InheritableThreadLocal<T>
	{
		static InheritableThreadLocal(){
			CallContext.SetData ("SharedData", 0);
		}
		static int ids = 0;

		static Dictionary<int, T> shared = new Dictionary<int, T> ();
		bool hasValue = false;
		T value = default(T);
		int id = -1;
		public T Get ()
		{
			
			if (hasValue == false) {
				if (CallContext.HostContext == null) {
					CallContext.HostContext = new object ();
				}

				var t = (int)CallContext.GetData ("SharedData");
				lock(shared)
					hasValue = shared.TryGetValue (t, out value);
			}
			return value;
		}

		public void Set (T val)
		{
			value = val;
			hasValue = true;
			if (id == -1) {
				id = ids++;
				CallContext.SetData ("SharedData", id);
			}
			lock(shared)
			shared [id] = val;
		}
	}

	internal class InheritableThreadLocal2<T> where T : class
	{
		private static object nullMarker;
		private LocalDataStoreSlot slot;

		static InheritableThreadLocal2 ()
		{
			InheritableThreadLocal2<T>.nullMarker = new object ();
		}

		public InheritableThreadLocal2 ()
		{
			this.slot = System.Threading.Thread.AllocateDataSlot ();
		}

		public T Get ()
		{
			object data = System.Threading.Thread.GetData (this.slot);
			if (data == nullMarker) {
				return null;
			}
			if (data == null) {
				data = InitialValue ();
				Set ((T)data);
			}
			return (T)data;
		}

		protected virtual T InitialValue ()
		{
			return null;
		}

		public void Set (T val)
		{
			if (val == null) {
				System.Threading.Thread.SetData (slot, nullMarker);
			} else {
				System.Threading.Thread.SetData (slot, val);
			}
		}
	}

	class InheritableThreadLocalTest{
		class Box<T> {
			public T Value;
		}


		static InheritableThreadLocal2<Box<int>> value = new InheritableThreadLocal2<Box<int>>();

		static void doStuff2()
		{
			Console.WriteLine ("Starting workers..\n");
			value.Set (new Box<int> ());
			List<Thread> threads = new List<Thread> ();
			for(int i = 0; i < 2; i++)
				threads.Add(new Thread (doStuff));
			threads.ForEach (x => x.Start ());
			threads.ForEach (x => x.Join ());
		}

		static void doStuff(){
			for (int i = 0; i < 10; i++) {
				var val = value.Get ();
				if (val == null) {
					Console.WriteLine ("This happens :s");
					value.Set (new Box<int> ());
				}

				value.Get().Value += 1;
				Console.WriteLine ("{0}", value.Get().Value);
				Thread.Sleep (1000);
			}
		}

		static void testThreadLocal()
		{
			List<Thread> threads = new List<Thread> ();
			for(int i = 0; i < 2; i++)
				threads.Add(new Thread (doStuff2));
			threads.ForEach (x => x.Start ());
			threads.ForEach (x => x.Join ());
		}

	}

}

