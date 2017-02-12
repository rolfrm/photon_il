using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using PhotonIl;
using System.Collections;

namespace PhotonIl2
{

	public interface IVariable
	{
		object Value{get;set;}
		Type Type {get;}

	}

	public class Variable<T> : IVariable
	{
		T data;
		public T Value
		{
			get{ return data;  }
			set{ data = value; }
		}

		object IVariable.Value{
			get{ return data; }
			set{ data = (T)value; }
		}
		public Type Type{
			get{ return typeof(T); }
		}
	}

}
