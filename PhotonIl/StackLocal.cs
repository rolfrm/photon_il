using System;

namespace PhotonIl
{
	public class StackLocal<T>{

		T value;
		public T Value{ get { return value; } }

		public StackLocal(T defaultValue = default(T)){
			value = defaultValue;
		}

		public class Holder : IDisposable{
			StackLocal<T> parent;
			public T PrevValue;

			public Holder(T value, StackLocal<T> parent){
				PrevValue = parent.value;
				parent.value = value;
				this.parent = parent;
			}

			public void Dispose(){
				parent.value = PrevValue;
			}
		}

		public IDisposable WithValue(T value){
			return new Holder (value, this);
		}
	}
}

