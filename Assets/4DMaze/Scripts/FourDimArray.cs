using System;

public class FourDimArray<T> {

	private Vector4Int _size;
	public Vector4Int size { get { return _size; } private set { _size = value; } }

	public T this[Vector4Int pos] { get { return _data[pos.x][pos.y][pos.z][pos.w]; } set { _data[pos.x][pos.y][pos.z][pos.w] = value; } }
	private T[][][][] _data;

	public FourDimArray(Vector4Int size) {
		this.size = size;
		_data = new T[size.x][][][];
		for (int x = 0; x < size.x; x++) {
			_data[x] = new T[size.y][][];
			for (int y = 0; y < size.y; y++) {
				_data[x][y] = new T[size.z][];
				for (int z = 0; z < size.z; z++) {
					_data[x][y][z] = new T[size.w];
				}
			}
		}
	}

	public FourDimArray(Vector4Int size, T _default) : this(size) {
		ForEach((_, pos) => this[pos] = _default);
	}

	public void ForEach(Action<T, Vector4Int> f) {
		for (int x = 0; x < size.x; x++) {
			for (int y = 0; y < size.y; y++) {
				for (int z = 0; z < size.z; z++) {
					for (int w = 0; w < size.w; w++) {
						Vector4Int pos = new Vector4Int(x, y, z, w);
						f(this[pos], pos);
					}
				}
			}
		}
	}

	public FourDimArray<F> Select<F>(Func<T, Vector4Int, F> f) {
		FourDimArray<F> result = new FourDimArray<F>(size);
		ForEach((value, pos) => result[pos] = f(value, pos));
		return result;
	}
}
