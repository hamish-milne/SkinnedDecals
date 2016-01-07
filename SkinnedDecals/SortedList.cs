using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SkinnedDecals
{
	[Serializable]
	public class SortedList<T> : ICollection<T>
	{
		private readonly Comparison<T> comparison; 

		[SerializeField]
		protected List<T> list = new List<T>();

		public SortedList(Comparison<T> comparison)
		{
			if(comparison == null)
				throw new ArgumentNullException(nameof(comparison));
			this.comparison = comparison;
		}

		public SortedList(IComparer<T> comparer)
		{
			if(comparer == null)
				throw new ArgumentNullException(nameof(comparer));
			comparison = comparer.Compare;
		}

		public SortedList() : this(Comparer<T>.Default)
		{
		} 

		public List<T>.Enumerator GetEnumerator()
		{
			return list.GetEnumerator();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Add(T item)
		{
			if (item == null)
				return;
			for(int i = 0; i < list.Count; i++)
				if (comparison(list[i], item) >= 0)
				{
					list.Insert(i, item);
					return;
				}
			list.Add(item);
		}

		public void Clear()
		{
			list.Clear();
		}

		public bool Contains(T item)
		{
			if (item == null)
				return false;
			return list.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			list.CopyTo(array, arrayIndex);
		}

		public bool Remove(T item)
		{
			return list.Remove(item);
		}

		public int Count => list.Count;
		public bool IsReadOnly => false;

		public T this[int index] => list[index];

		public void RemoveAt(int index)
		{
			list.RemoveAt(index);
		}

		public T[] ToArray()
		{
			return list.ToArray();
		}
	}
}
