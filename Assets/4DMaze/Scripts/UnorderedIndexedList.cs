using UnityEngine;

public class UnorderedIndexedList<T> {
	private int _size = 0;
	public int Size { get { return _size; } private set { _size = value; } }

	private T[] _data;

	public UnorderedIndexedList(int initialSize = 10) {
		_data = new T[initialSize];
	}

	public UnorderedIndexedList(T[] collection, int initialSize = 10) : this(initialSize) {
		foreach (T e in collection) Push(e);
	}

	public UnorderedIndexedList<T> Push(T value) {
		if (Size == _data.Length) {
			T[] temp = new T[_data.Length == 0 ? 1 : _data.Length * 2];
			for (int i = 0; i < Size; i++) temp[i] = _data[i];
			_data = temp;
		}
		_data[Size++] = value;
		return this;
	}

	public T Pop(MonoRandom rnd) {
		int index = rnd.Next(0, Size);
		T result = _data[index];
		_data[index] = _data[--Size];
		return result;
	}

	public T Pop() {
		int index = Random.Range(0, Size);
		T result = _data[index];
		_data[index] = _data[--Size];
		return result;
	}
}
