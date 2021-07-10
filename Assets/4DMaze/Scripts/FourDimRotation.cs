using UnityEngine;

public class FourDimRotation {
	public Vector4 Right = new Vector4(1, 0, 0, 0);
	public Vector4 Up = new Vector4(0, 1, 0, 0);
	public Vector4 Front = new Vector4(0, 0, 1, 0);
	public Vector4 Ana = new Vector4(0, 0, 0, 1);

	public FourDimRotation(Vector4 right, Vector4 up, Vector4 front, Vector4 ana) {
		this.Right = right;
		this.Up = up;
		this.Front = front;
		this.Ana = ana;
	}
}
