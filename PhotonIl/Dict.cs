using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotonIl
{
	public class Dict<K, V> : Dictionary<K, V>{

		public V Get(K key){
			if (ContainsKey (key))
				return this [key];
			return default(V);
		}
	}

	public class MultiDict<K, V>{
		Dictionary<K, List<V>> dict = new Dictionary<K, List<V>>();
		public void Add(K key, V value){
			if(dict.ContainsKey(key) == false)
				dict[key] = new List<V>{};
			dict [key].Add (value);
		}
		public void Add(K key, IEnumerable<V> value){
			if(dict.ContainsKey(key) == false)
				dict[key] = new List<V>{};
			dict [key].AddRange (value);
		}
		V[] empty = new V[0];
		public V[] Get(K key){
			if (dict.ContainsKey (key))
				return dict [key].ToArray();
			return empty;
		}

		public IEnumerable<KeyValuePair<K, List<V>>> Entries{
			get{ return dict; }
		}
	}
}

