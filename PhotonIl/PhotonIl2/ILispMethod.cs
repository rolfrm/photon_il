using System;

namespace PhotonIl2
{
	public interface ILispMethod
	{
		Type[] ArgumentTypes{ get; }
		Symbol[] ArgumentNames{ get; }
		Type ReturnType {get;}
		object Invoke (params object[] args);
	}
}

