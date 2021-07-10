using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class FourDimMazeModule : MonoBehaviour {
	public const int MAX_TARGET_DISTANCE = 8;
	public const float CUBE_OFFSET = 0.04f;
	public const float BUTTONS_OFFSET = 0.02f;
	public static readonly Vector4Int SIZE = new Vector4Int(5, 5, 5, 5);
	public static Color[] COLORS { get { return new[] { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.cyan }; } }

	public static Vector4Int[] AXIS {
		get { return new[] { new Vector4Int(1, 0, 0, 0), new Vector4Int(0, 1, 0, 0), new Vector4Int(0, 0, 1, 0), new Vector4Int(0, 0, 0, 1) }; }
	}

	public static Vector4Int[] DIRECTIONS { get { return AXIS.SelectMany(axis => new[] { axis, axis * -1 }).ToArray(); } }
	public readonly string[] DIRECTIONS_NAMES = new[] { "+X", "-X", "+Y", "-Y", "+Z", "-Z", "+W", "-W" };

	public enum TurnDirection { RIGHT, LEFT, UP, DOWN, ANA, KATA };

	private static int moduleIdCounter = 1;

	public KMRuleSeedable RuleSeedable;
	public GameObject ViewContainer;
	public GameObject ButtonsContainer;
	public TextMesh TargetText;
	public KMSelectable Selectable;
	public KMSelectable SubmitButton;
	public KMBombModule BombModule;
	public ButtonComponent ButtonPrefab;
	public HyperCube HyperCubePrefab;

	private bool activated = false;
	private bool solved = false;
	private int moduleId;
	private Vector4Int pos;
	private Vector4Int f;
	private Vector4Int r;
	private Vector4Int u;
	private Vector4Int a;
	private Vector4Int target;
	private FourDimArray<Color?> walls;
	private Dictionary<Vector4Int, HyperCube> hypercubes = new Dictionary<Vector4Int, HyperCube>();

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
		Selectable.Children = new[] {
			CreateButton(Vector3.zero, "L", () => Turn(TurnDirection.LEFT)),
			CreateButton(Vector3.right, "R", () => Turn(TurnDirection.RIGHT)),
			CreateButton(Vector3.back, "U", () => Turn(TurnDirection.UP)),
			CreateButton(Vector3.back * 2, "D", () => Turn(TurnDirection.DOWN)),
			CreateButton(Vector3.back + Vector3.right, "A", () => Turn(TurnDirection.ANA)),
			CreateButton(Vector3.back * 2 + Vector3.right, "K", () => Turn(TurnDirection.KATA)),
			CreateButton(Vector3.right / 2 + Vector3.back * 4, "F", () => MoveForward(), 2f),
		}.Select(b => b.Selectable).Concat(new[] { SubmitButton }).ToArray();
		Selectable.UpdateChildren();
		BombModule.OnActivate += Activate;
	}

	private void Update() {
		if (activated) RenderWalls();
	}

	private void Activate() {
		FourDimArray<int> steps = new FourDimArray<int>(SIZE, int.MaxValue);
		steps[pos] = 0;
		Queue<Vector4Int> q = new Queue<Vector4Int>();
		q.Enqueue(pos);
		target = pos;
		int exactStepsCount = 1;
		int distance = 0;
		while (q.Count > 0) {
			Vector4Int pos = q.Dequeue();
			int newSteps = steps[pos] + 1;
			if (newSteps > MAX_TARGET_DISTANCE) break;
			foreach (Vector4Int dd in DIRECTIONS) {
				Vector4Int adjPos = pos.AddMod(dd, SIZE);
				if (walls[adjPos] != null) continue;
				if (steps[adjPos] <= newSteps) continue;
				steps[adjPos] = newSteps;
				q.Enqueue(adjPos);
				if (distance == newSteps) {
					if (Random.Range(0, exactStepsCount) == 0) target = adjPos;
					exactStepsCount++;
				} else if (distance < newSteps) {
					distance = newSteps;
					exactStepsCount = 1;
					target = adjPos;
				}
			}
		}
		TargetText.text = (target + Vector4Int.one).ToString();
		SubmitButton.OnInteract += () => { Submit(); return false; };
		RenderWalls();
		activated = true;
	}

	private void Submit() {
		if (pos == target) {
			solved = true;
			BombModule.HandlePass();
		} else BombModule.HandleStrike();
	}

	private void RenderWalls() {
		HashSet<Vector4Int> positionsToRender = new HashSet<Vector4Int>(new[] { r, u, a }.SelectMany(d => new[] { pos + d, pos - d }));
		if (walls[pos.AddMod(f, SIZE)] == null) positionsToRender = new HashSet<Vector4Int>(positionsToRender.SelectMany(p => new[] { p, p + f }));
		positionsToRender.Add(pos + f);
		foreach (Vector4Int idToRemove in hypercubes.Keys.Where((k) => !positionsToRender.Contains(k)).ToArray()) {
			Destroy(hypercubes[idToRemove].gameObject);
			hypercubes.Remove(idToRemove);
		}
		foreach (Vector4Int id in positionsToRender) {
			if (hypercubes.ContainsKey(id)) continue;
			Color? color = walls[id.AddMod(Vector4Int.zero, SIZE)];
			if (color == null) continue;
			HyperCube hypercube = Instantiate(HyperCubePrefab);
			hypercube.transform.parent = ViewContainer.transform;
			hypercube.transform.localPosition = Vector3.zero;
			hypercube.transform.localScale = Vector3.one;
			hypercube.transform.localRotation = Quaternion.identity;
			hypercube.pos = new Vector4(id.x, id.y, id.z, id.w);
			hypercube.color = color.Value;
			foreach (Vector4Int dir in DIRECTIONS) if (walls[id.AddMod(dir, SIZE)] == null) hypercube.renderedCubes.Add(dir);
			hypercubes[id] = hypercube;
		}
		foreach (HyperCube hypercube in hypercubes.Values) {
			hypercube.Render(ToVector4(pos), new FourDimRotation(ToVector4(r), ToVector4(u), ToVector4(f), ToVector4(a)));
		}
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

	private void MoveForward() {
		if (!activated || solved) return;
		Vector4Int newPos = pos.AddMod(f, SIZE);
		if (walls[newPos] != null) return;
		pos = newPos;
		RenderWalls();
	}

	private void Turn(TurnDirection dir) {
		switch (dir) {
			case TurnDirection.LEFT: Turn(ref r, f, ref f, -r); break;
			case TurnDirection.RIGHT: Turn(ref r, -f, ref f, r); break;
			case TurnDirection.UP: Turn(ref u, -f, ref f, u); break;
			case TurnDirection.DOWN: Turn(ref u, f, ref f, -u); break;
			case TurnDirection.ANA: Turn(ref a, -f, ref f, a); break;
			case TurnDirection.KATA: Turn(ref a, f, ref f, -a); break;
			default: throw new NotImplementedException();
		}
	}

	private void Turn(ref Vector4Int a, Vector4Int newA, ref Vector4Int b, Vector4Int newB) {
		if (!activated || solved) return;
		a = newA;
		b = newB;
		RenderWalls();
	}

	private static Vector4 ToVector4(Vector4Int v) {
		return new Vector4(v.x, v.y, v.z, v.w);
	}
}
