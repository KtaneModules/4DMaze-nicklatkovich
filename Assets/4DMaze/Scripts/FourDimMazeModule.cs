using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class FourDimMazeModule : MonoBehaviour {
	public const float CUBE_OFFSET = 0.05f;
	public const float BUTTONS_OFFSET = 0.02f;
	public static readonly Vector4Int SIZE = new Vector4Int(5, 5, 5, 5);
	public static Color[] COLORS { get { return new[] { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.cyan }; } }

	public static Vector4Int[] AXIS {
		get { return new[] { new Vector4Int(1, 0, 0, 0), new Vector4Int(0, 1, 0, 0), new Vector4Int(0, 0, 1, 0), new Vector4Int(0, 0, 0, 1) }; }
	}

	public static Vector4Int[] DIRECTIONS { get { return AXIS.SelectMany(axis => new[] { axis, axis * -1 }).ToArray(); } }
	public readonly string[] DIRECTIONS_NAMES = new[] { "+X", "-X", "+Y", "-Y", "+Z", "-Z", "+W", "-W" };

	private static int moduleIdCounter = 1;

	public KMRuleSeedable RuleSeedable;
	public GameObject ViewContainer;
	public GameObject ButtonsContainer;
	public KMSelectable Selectable;
	public ButtonComponent ButtonPrefab;
	public WallComponent WallPrefab;

	private int moduleId;
	private Vector4Int pos;
	private Vector4Int f;
	private Vector4Int r;
	private Vector4Int u;
	private Vector4Int a;
	private FourDimArray<Color?> walls;

	private void Start() {
		moduleId = moduleIdCounter++;
		MonoRandom rnd = RuleSeedable.GetRNG();
		FourDimArray<int> temp = new FourDimArray<int>(SIZE, 0);
		Vector4Int generationStartPos = new Vector4Int(rnd.Next(0, SIZE.x), rnd.Next(0, SIZE.y), rnd.Next(0, SIZE.z), rnd.Next(0, SIZE.w));
		temp[generationStartPos] = 1;
		UnorderedIndexedList<Vector4Int> q = new UnorderedIndexedList<Vector4Int>();
		q.Push(generationStartPos);
		while (q.Size > 0) {
			Vector4Int pos = q.Pop(rnd);
			if (temp[pos] != 1) continue;
			temp[pos] = 2;
			foreach (Vector4Int direction in DIRECTIONS) {
				Vector4Int adjPos = pos.AddMod(direction, SIZE);
				if (temp[adjPos] == 0) {
					temp[adjPos] = 1;
					q.Push(adjPos);
				} else if (temp[adjPos] == 1) temp[adjPos] = 3;
			}
		}
		temp.ForEach((value, pos) => {
			if (value == 3 && rnd.Next(0, 8) == 0) temp[pos] = 2;
		});
		walls = temp.Select((cell, pos) => cell == 2 ? null as Color? : COLORS[rnd.Next(0, COLORS.Length)]);
		int passedCells = 0;
		walls.ForEach((cell, pos) => {
			if (cell != null) return;
			if (Random.Range(0, passedCells++) == 0) this.pos = pos;
		});
		Debug.LogFormat("[4D Maze #{0}] Initial position: {1}", moduleId, (pos + Vector4Int.one).ToString());
		int[] axis = Enumerable.Range(0, 4).Select(a => a * 2 + (Random.Range(0, 2) == 0 ? 0 : 1)).ToArray();
		axis = axis.Shuffle();
		r = AXIS[0];
		u = AXIS[1];
		a = AXIS[2];
		f = AXIS[3];
		Debug.LogFormat("[4D Maze #{0}] Initial view direction: {1}", moduleId, "(" + axis.Select(a => DIRECTIONS_NAMES[a]).Join(";") + ")");
		Debug.Log(walls[new Vector4Int(0, 0, 0, 0)]);
		Debug.Log(walls[new Vector4Int(1, 0, 0, 0)]);
		Debug.Log(walls[new Vector4Int(2, 0, 0, 0)]);
		Debug.Log(walls[new Vector4Int(3, 0, 0, 0)]);
		Debug.Log(walls[new Vector4Int(4, 0, 0, 0)]);
		RenderWalls();
		Selectable.Children = new[] {
			CreateButton(Vector3.zero, "L", () => Turn(ref r, f, ref f, -r)),
			CreateButton(Vector3.right, "R", () => Turn(ref r, -f, ref f, r)),
			CreateButton(Vector3.back, "U", () => Turn(ref u, -f, ref f, u)),
			CreateButton(Vector3.back * 2, "D", () => Turn(ref u, f, ref f, -u)),
			CreateButton(Vector3.back + Vector3.right, "A", () => Turn(ref a, -f, ref f, a)),
			CreateButton(Vector3.back * 2 + Vector3.right, "K", () => Turn(ref a, f, ref f, -a)),
			CreateButton(Vector3.right / 2 + Vector3.back * 4, "F", () => MoveForward(), 2f),
		}.Select(b => b.Selectable).ToArray();
		Selectable.UpdateChildren();
	}

	private void RenderWalls() {
		int childs = ViewContainer.transform.childCount;
		for (int i = childs - 1; i >= 0; i--) Destroy(ViewContainer.transform.GetChild(i).gameObject);
		Color? r = walls[pos.AddMod(this.r, SIZE)];
		if (r != null) CreateWall(Vector3.right, r.Value);
		Color? l = walls[pos.AddMod(-this.r, SIZE)];
		if (l != null) CreateWall(Vector3.left, l.Value);
		Color? u = walls[pos.AddMod(this.u, SIZE)];
		if (u != null) CreateWall(Vector3.up, u.Value);
		Color? d = walls[pos.AddMod(-this.u, SIZE)];
		if (d != null) CreateWall(Vector3.down, d.Value);
		Color? a = walls[pos.AddMod(this.a, SIZE)];
		if (a != null) CreateWall(Vector3.forward, a.Value);
		Color? k = walls[pos.AddMod(-this.a, SIZE)];
		if (k != null) CreateWall(Vector3.back, k.Value);
		Color? f = walls[pos.AddMod(this.f, SIZE)];
		if (f != null) CreateWall(Vector3.zero, f.Value);
	}

	private ButtonComponent CreateButton(Vector3 pos, string label, Action action = null, float scale = 1f) {
		ButtonComponent button = Instantiate(ButtonPrefab);
		button.transform.parent = ButtonsContainer.transform;
		button.transform.localPosition = pos * BUTTONS_OFFSET;
		button.transform.localScale = Vector3.one * scale;
		button.transform.localRotation = Quaternion.identity;
		button.Selectable.Parent = Selectable;
		button.text = label;
		if (action != null) button.Selectable.OnInteract += () => { action(); return false; };
		return button;
	}

	private void CreateWall(Vector3 pos, Color color) {
		WallComponent wall = Instantiate(WallPrefab);
		wall.transform.parent = ViewContainer.transform;
		wall.transform.localPosition = pos * CUBE_OFFSET;
		wall.transform.localScale = Vector3.one;
		wall.transform.localRotation = Quaternion.identity;
		wall.color = color;
	}

	private void MoveForward() {
		Vector4Int newPos = pos.AddMod(f, SIZE);
		if (walls[newPos] != null) return;
		pos = newPos;
		RenderWalls();
	}

	private void Turn(ref Vector4Int a, Vector4Int newA, ref Vector4Int b, Vector4Int newB) {
		a = newA;
		b = newB;
		RenderWalls();
	}
}
