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
	public readonly Dictionary<Vector4Int, string> DIRECTIONS_NAMES = new Dictionary<Vector4Int, string>() {
		{ Vector4Int.right, "+X" },
		{ Vector4Int.left, "-X" },
		{ Vector4Int.up, "+Y" },
		{ Vector4Int.down, "-Y" },
		{ Vector4Int.front, "+Z" },
		{ Vector4Int.back, "-Z" },
		{ Vector4Int.ana, "+W" },
		{ Vector4Int.kata, "-W" },
	};

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
	private float anim = 1f;
	private Vector4Int pos;
	private Vector4Int f;
	private Vector4Int r;
	private Vector4Int u;
	private Vector4Int a;
	private Vector4Int toPos;
	private Vector4Int toF;
	private Vector4Int toR;
	private Vector4Int toU;
	private Vector4Int toA;
	private Vector4Int target;
	private FourDimArray<Color?> walls;
	private Dictionary<Vector4Int, HyperCube> hypercubes = new Dictionary<Vector4Int, HyperCube>();
	private Queue<TurnDirection?> queue = new Queue<TurnDirection?>();

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
		r = AXIS[0];
		u = AXIS[1];
		a = AXIS[2];
		f = AXIS[3];
		toPos = pos;
		toR = r;
		toU = u;
		toA = a;
		toF = f;
		Debug.LogFormat("[4D Maze #{0}] Initial view direction: {1}", moduleId, "(" + new[]{ r, u, a, f }.Select(a => DIRECTIONS_NAMES[a]).Join(";") + ")");
		Selectable.Children = new[] {
			CreateButton(Vector3.zero, "L", () => queue.Enqueue(TurnDirection.LEFT)),
			CreateButton(Vector3.right, "R", () => queue.Enqueue(TurnDirection.RIGHT)),
			CreateButton(Vector3.back, "U", () => queue.Enqueue(TurnDirection.UP)),
			CreateButton(Vector3.back * 2, "D", () => queue.Enqueue(TurnDirection.DOWN)),
			CreateButton(Vector3.back + Vector3.right, "A", () => queue.Enqueue(TurnDirection.ANA)),
			CreateButton(Vector3.back * 2 + Vector3.right, "K", () => queue.Enqueue(TurnDirection.KATA)),
			CreateButton(Vector3.right / 2 + Vector3.back * 4, "F", () => queue.Enqueue(null), 2f),
		}.Select(b => b.Selectable).Concat(new[] { SubmitButton }).ToArray();
		Selectable.UpdateChildren();
		BombModule.OnActivate += Activate;
	}

	private void Update() {
		if (!activated) return;
		if (anim < 1f) anim = Mathf.Min(1f, anim + Time.deltaTime * (1 + queue.Count));
		else {
			if (pos != toPos) Debug.LogFormat("[4D Maze #{0}] Moved to: {1}", moduleId, (toPos + Vector4Int.one).ToString());
			pos = toPos;
			r = toR;
			u = toU;
			a = toA;
			f = toF;
			RemoveWalls();
			if (queue.Count > 0) {
				TurnDirection? nextAction = queue.Dequeue();
				if (nextAction == null) MoveForward();
				else Turn(nextAction.Value);
				AddWalls();
			}
		}
		RenderWalls();
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
		AddWalls();
		activated = true;
	}

	private void Submit() {
		if (pos == target) {
			solved = true;
			BombModule.HandlePass();
		} else BombModule.HandleStrike();
	}

	private void AddWalls() {
		foreach (Vector4Int id in GetPositionsToRender()) {
			if (hypercubes.ContainsKey(id)) {
				HyperCube oldHypercube = hypercubes[id];
				bool wasDestroying = oldHypercube.destroy;
				oldHypercube.destroy = false;
				if (wasDestroying) oldHypercube.renderedCubes = new HashSet<Vector4Int>();
				foreach (Vector4Int dir in DIRECTIONS) if (walls[id.AddMod(dir, SIZE)] == null) oldHypercube.renderedCubes.Add(dir);
				continue;
			}
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
	}

	private HashSet<Vector4Int> GetPositionsToRender() {
		return new HashSet<Vector4Int>(GetPositionsToRender(pos, r, u, a, f).Concat(GetPositionsToRender(toPos, toR, toU, toA, toF)));
	}

	private HashSet<Vector4Int> GetPositionsToRender(Vector4Int pos, Vector4Int r, Vector4Int u, Vector4Int a, Vector4Int f) {
		HashSet<Vector4Int> positionsToRender = new HashSet<Vector4Int>(new[] { r, u, a }.SelectMany(d => new[] { pos + d, pos - d }));
		if (walls[pos.AddMod(f, SIZE)] == null) positionsToRender = new HashSet<Vector4Int>(positionsToRender.SelectMany(p => new[] { p, p + f }));
		else positionsToRender.Add(pos + f);
		Dictionary<Vector4Int, Vector4Int[]> perpendiculars = new Dictionary<Vector4Int, Vector4Int[]>{
			{ r, new[] { u, a, f, -u, -a } },
			{ u, new[] { r, a, f, -r, -a } },
			{ a, new[] { r, u, f, -r, -u } },
			{ -r, new[] { u, a, f, -u, -a } },
			{ -u, new[] { r, a, f, -r, -a } },
			{ -a, new[] { r, u, f, -r, -u } },
		};
		foreach (KeyValuePair<Vector4Int, Vector4Int[]> pair in perpendiculars) {
			if (walls[pos.AddMod(pair.Key, SIZE)] == null) {
				positionsToRender = new HashSet<Vector4Int>(positionsToRender.Concat(pair.Value.Select(p => pos + pair.Key + p)));
			}
		}
		return positionsToRender;
	}

	private void RemoveWalls() {
		HashSet<Vector4Int> positionsToRender = GetPositionsToRender();
		foreach (Vector4Int idToRemove in hypercubes.Keys.Where((k) => !positionsToRender.Contains(k)).ToArray()) hypercubes[idToRemove].destroy = true;
	}

	private void RenderWalls() {
		foreach (Vector4Int id in hypercubes.Keys.ToArray()) {
			HyperCube hypercube = hypercubes[id];
			if (hypercube.destroy && hypercube.distructionAnim <= 0f) {
				Destroy(hypercube.gameObject);
				hypercubes.Remove(id);
				continue;
			}
			hypercube.Render(Lerp(pos, toPos, anim), new FourDimRotation(LerpNorm(r, toR, anim), LerpNorm(u, toU, anim), LerpNorm(f, toF, anim), LerpNorm(a, toA, anim)));
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
		Vector4Int newPos = pos + f;
		if (walls[newPos.AddMod(Vector4Int.zero, SIZE)] != null) return;
		toPos = newPos;
		anim = 0;
	}

	private void Turn(TurnDirection dir) {
		switch (dir) {
			case TurnDirection.LEFT: Turn(ref toR, f, ref toF, -r); break;
			case TurnDirection.RIGHT: Turn(ref toR, -f, ref toF, r); break;
			case TurnDirection.UP: Turn(ref toU, -f, ref toF, u); break;
			case TurnDirection.DOWN: Turn(ref toU, f, ref toF, -u); break;
			case TurnDirection.ANA: Turn(ref toA, -f, ref toF, a); break;
			case TurnDirection.KATA: Turn(ref toA, f, ref toF, -a); break;
			default: throw new NotImplementedException();
		}
		anim = 0;
	}

	private void Turn(ref Vector4Int a, Vector4Int newA, ref Vector4Int b, Vector4Int newB) {
		a = newA;
		b = newB;
	}

	private static Vector4 Lerp(Vector4Int from, Vector4Int to, float anim) {
		return (((Vector4)from) + (to - from) * anim);
	}

	private static Vector4 LerpNorm(Vector4Int from, Vector4Int to, float anim) {
		return Lerp(from, to, anim).normalized;
	}
}
