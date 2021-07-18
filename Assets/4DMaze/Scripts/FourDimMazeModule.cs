using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class FourDimMazeModule : MonoBehaviour {
	class FourDimMazeSettings {
		public bool advancedRenderingMode = true;
		public bool wiggleMode = true;
		public bool postSolveAutoMovement = true;
		public bool postSolveAutoAdvancedRenderingMode = true;
	}

	public const float ANIMATION_ACCELERATION = 1f;
	public const float CONTAINER_ANGLE = 8f;
	public const float CONTAINER_ANGLE_SPEED = 1f;
	public const float CONTAINER_ROTATION_SPEED = 1f;
	public const int MAX_TARGET_DISTANCE = 8;
	public const float CUBE_OFFSET = 0.04f;
	public const float BUTTONS_OFFSET = 0.02f;
	public static readonly Vector4Int SIZE = new Vector4Int(5, 5, 5, 5);
	public static Color[] COLORS { get { return new[] { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.cyan }; } }

	public readonly string TwitchHelpMessage = "\"{0} rludakf\" - rotate/move | \"{0} mode\" - toggle render mode | \"{0} submit\" - press submit";

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
	private readonly TurnDirection[] AllTurnDirections = new[] {
		TurnDirection.RIGHT, TurnDirection.LEFT, TurnDirection.UP, TurnDirection.DOWN, TurnDirection.ANA, TurnDirection.KATA
	};

	private static int moduleIdCounter = 1;

	public KMRuleSeedable RuleSeedable;
	public GameObject ViewContainer;
	public GameObject ButtonsContainer;
	public TextMesh TargetText;
	public KMSelectable Selectable;
	public KMSelectable SubmitButton;
	public KMBombModule BombModule;
	public KMAudio Audio;
	public ModeTogglerComponent ModeToggleButton;
	public ModeTogglerComponent WiggleToggleButton;
	public ButtonComponent ButtonPrefab;
	public HyperCube HyperCubePrefab;

	public bool TwitchShouldCancelCommand;

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
	private Queue<TurnDirection?> queue = new Queue<TurnDirection?>();
	private float animationSpeed;
	private bool solvingAnimationActive;
	private bool postSolveAutoAdvancedRenderingMode;
	private float containerRotation;
	private float containerAngle;
	private bool solvingAnimationForcedForward = false;
	private FourDimArray<Color?> walls;
	private Dictionary<Vector4Int, HyperCube> hypercubes = new Dictionary<Vector4Int, HyperCube>();

	private void Start() {
		ModConfig<FourDimMazeSettings> modConfig = new ModConfig<FourDimMazeSettings>("4DMazeSettings");
		FourDimMazeSettings settings = modConfig.Read();
		modConfig.Write(settings);
		ModeToggleButton.advanced = settings.advancedRenderingMode;
		WiggleToggleButton.advanced = settings.wiggleMode;
		postSolveAutoAdvancedRenderingMode = settings.postSolveAutoAdvancedRenderingMode;
		solvingAnimationActive = settings.postSolveAutoMovement;
		containerAngle = settings.wiggleMode ? CONTAINER_ANGLE : 0f;
		moduleId = moduleIdCounter++;
		MonoRandom rnd = RuleSeedable.GetRNG();
		Debug.LogFormat("[4D Maze #{0}] Map seed: {1}", moduleId, rnd.Seed);
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
		toPos = pos;
		toR = r = AXIS[0];
		toU = u = AXIS[1];
		toA = a = AXIS[2];
		toF = f = AXIS[3];
		List<TurnDirection> turns = new List<TurnDirection>();
		foreach (int i in Enumerable.Range(0, 3)) turns.Add(TurnDirection.RIGHT);
		foreach (int i in Enumerable.Range(0, 3)) turns.Add(TurnDirection.UP);
		foreach (int i in Enumerable.Range(0, 3)) turns.Add(TurnDirection.DOWN);
		foreach (int i in Enumerable.Range(0, 3)) turns.Add(TurnDirection.ANA);
		turns.Shuffle();
		foreach (TurnDirection turn in turns) {
			Turn(turn);
			r = toR;
			u = toU;
			a = toA;
			f = toF;
		}
		anim = 1f;
		Debug.LogFormat(
			"[4D Maze #{0}] Initial orientation: (R:{1}; U:{2}; A:{3}; F:{4})",
			moduleId, DIRECTIONS_NAMES[r], DIRECTIONS_NAMES[u], DIRECTIONS_NAMES[a], DIRECTIONS_NAMES[f]
		);
		Selectable.Children = new[] {
			CreateButton(Vector3.zero, "L", () => queue.Enqueue(TurnDirection.LEFT)),
			CreateButton(Vector3.right, "R", () => queue.Enqueue(TurnDirection.RIGHT)),
			CreateButton(Vector3.back, "U", () => queue.Enqueue(TurnDirection.UP)),
			CreateButton(Vector3.back * 2, "D", () => queue.Enqueue(TurnDirection.DOWN)),
			CreateButton(Vector3.back + Vector3.right, "A", () => queue.Enqueue(TurnDirection.ANA)),
			CreateButton(Vector3.back * 2 + Vector3.right, "K", () => queue.Enqueue(TurnDirection.KATA)),
			CreateButton(Vector3.right / 2 + Vector3.back * 4, "F", () => queue.Enqueue(null)),
		}.Select(b => b.Selectable).Concat(new[] { SubmitButton, ModeToggleButton.Selectable, WiggleToggleButton.Selectable }).ToArray();
		Selectable.UpdateChildren();
		containerRotation = Random.Range(0, 2f * Mathf.PI);
		BombModule.OnActivate += Activate;
	}

	private void Update() {
		containerRotation += CONTAINER_ROTATION_SPEED * Time.deltaTime;
		if (WiggleToggleButton.advanced) containerAngle = Mathf.Min(1f, containerAngle + CONTAINER_ANGLE_SPEED * Time.deltaTime);
		else containerAngle = Mathf.Max(0f, containerAngle - CONTAINER_ANGLE_SPEED * Time.deltaTime);
		ViewContainer.transform.localRotation = Quaternion.FromToRotation(
			CONTAINER_ANGLE * Vector3.forward, new Vector3(containerAngle * Mathf.Cos(containerRotation), containerAngle * Mathf.Sin(containerRotation), CONTAINER_ANGLE));
		float animationSpeedTarget = queue.Count + 1;
		float animationAcceleration = ANIMATION_ACCELERATION * Time.deltaTime;
		if (animationSpeedTarget > animationSpeed) animationSpeed = Mathf.Min(animationSpeedTarget, animationSpeed + animationAcceleration);
		else animationSpeed = Mathf.Max(animationSpeedTarget, animationSpeed - animationAcceleration);
		if (!activated) return;
		if (solved) TargetText.text = ((anim < .5f ? pos : toPos).AddMod(Vector4Int.zero, SIZE) + Vector4Int.one).ToString();
		if (anim < 1f) anim = Mathf.Min(1f, anim + animationSpeed * Time.deltaTime);
		if (anim >= 1f) {
			if (!solved && pos != toPos) Debug.LogFormat("[4D Maze #{0}] Moved to: {1}", moduleId, (toPos.AddMod(Vector4Int.zero, SIZE) + Vector4Int.one).ToString());
			pos = toPos;
			r = toR;
			u = toU;
			a = toA;
			f = toF;
			RemoveWalls();
			if (queue.Count > 0) {
				TurnDirection? action = queue.Dequeue();
				if (action == null) MoveForward();
				else Turn(action.Value);
				AddWalls();
			}
			if (solved && solvingAnimationActive && Idle()) {
				animationSpeed = 1f;
				if (solvingAnimationForcedForward) {
					queue.Enqueue(null);
					solvingAnimationForcedForward = false;
				} else {
					TurnDirection[] possibleTurnDirections = AllTurnDirections.Where(td => walls[pos.AddMod(TurnDirectionToDirection(td), SIZE)] == null).ToArray();
					bool wallOnFront = walls[pos.AddMod(f, SIZE)] != null;
					if (wallOnFront || (possibleTurnDirections.Length > 0 && Random.Range(0, 4) == 0)) {
						queue.Enqueue(possibleTurnDirections.Length == 0 ? AllTurnDirections.PickRandom() : possibleTurnDirections.PickRandom());
						solvingAnimationForcedForward = true;
					} else queue.Enqueue(null);
				}
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
		Debug.LogFormat("[4D Maze #{0}] Target position: {1}", moduleId, (target + Vector4Int.one).ToString());
		SubmitButton.OnInteract += () => { Submit(); return false; };
		ModeToggleButton.Selectable.OnInteract += () => { ToggleRenderMode(); return false; };
		WiggleToggleButton.Selectable.OnInteract += () => { WiggleToggleButton.advanced = !WiggleToggleButton.advanced; return false; };
		AddWalls();
		activated = true;
	}

	private void ToggleRenderMode() {
		ModeToggleButton.advanced = !ModeToggleButton.advanced;
		if (ModeToggleButton.advanced) AddWalls();
		else RemoveWalls();
		RenderWalls();
	}

	private void Submit() {
		if (solved) {
			solvingAnimationActive = !solvingAnimationActive;
			return;
		}
		if (!activated) return;
		Vector4Int posToCheck = anim < .5f ? pos : toPos;
		if (posToCheck.AddMod(Vector4Int.zero, SIZE) == target) {
			Debug.LogFormat("[4D Maze #{0}] Submit pressed on valid coordinates. Module solved!", moduleId);
			solved = true;
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
			BombModule.HandlePass();
			if (postSolveAutoAdvancedRenderingMode && !ModeToggleButton.advanced) ToggleRenderMode();
		} else {
			Debug.LogFormat("[4D Maze #{0}] Submit pressed on coordinates {1}. Strike!", moduleId, (posToCheck.AddMod(Vector4Int.zero, SIZE) + Vector4Int.one).ToString());
			BombModule.HandleStrike();
		}
	}

	public IEnumerator ProcessTwitchCommand(string command) {
		command = command.Trim().ToLower();
		if (command == "submit") {
			yield return null;
			yield return new[] { SubmitButton };
			yield break;
		}
		if (command == "mode") {
			yield return null;
			yield return new[] { ModeToggleButton.Selectable };
			yield break;
		}
		if (Regex.IsMatch(command, "^[rludakf]+$")) {
			yield return null;
			foreach (char c in command) {
				if (TwitchShouldCancelCommand) {
					yield return "cancelled";
					break;
				}
				switch (c) {
					case 'r': queue.Enqueue(TurnDirection.RIGHT); break;
					case 'l': queue.Enqueue(TurnDirection.LEFT); break;
					case 'u': queue.Enqueue(TurnDirection.UP); break;
					case 'd': queue.Enqueue(TurnDirection.DOWN); break;
					case 'a': queue.Enqueue(TurnDirection.ANA); break;
					case 'k': queue.Enqueue(TurnDirection.KATA); break;
					case 'f': queue.Enqueue(null); break;
					default: throw new NotImplementedException();
				}
				while (!Idle()) yield return null;
			}
			yield break;
		}
	}

	private void AddWalls() {
		foreach (Vector4Int id in GetPositionsToRender()) {
			if (hypercubes.ContainsKey(id)) {
				HyperCube oldHypercube = hypercubes[id];
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
		positionsToRender.Add(pos + f);
		if (!ModeToggleButton.advanced) return positionsToRender;
		if (walls[pos.AddMod(f, SIZE)] == null) positionsToRender = new HashSet<Vector4Int>(positionsToRender.SelectMany(p => new[] { p, p + f }));
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
		foreach (Vector4Int idToRemove in hypercubes.Keys.Where((k) => !positionsToRender.Contains(k)).ToArray()) {
			Destroy(hypercubes[idToRemove].gameObject);
			hypercubes.Remove(idToRemove);
		}
		foreach (Vector4Int id in hypercubes.Keys) {
			HyperCube hypercube = hypercubes[id];
			hypercube.renderedCubes = new HashSet<Vector4Int>();
			foreach (Vector4Int dir in DIRECTIONS) if (walls[id.AddMod(dir, SIZE)] == null) hypercube.renderedCubes.Add(dir);
		}
	}

	private void RenderWalls() {
		foreach (Vector4Int id in hypercubes.Keys.ToArray()) {
			HyperCube hypercube = hypercubes[id];
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
		if (action != null) button.Selectable.OnInteract += () => {
			if (!solved || !solvingAnimationActive) {
				Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
				action();
			}
			return false;
		};
		return button;
	}

	private void MoveForward() {
		Vector4Int newPos = pos + f;
		if (walls[newPos.AddMod(Vector4Int.zero, SIZE)] != null) return;
		toPos = newPos;
		anim -= 1;
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
		anim -= 1;
	}

	private void Turn(ref Vector4Int a, Vector4Int newA, ref Vector4Int b, Vector4Int newB) {
		a = newA;
		b = newB;
	}

	private Vector4Int TurnDirectionToDirection(TurnDirection dir) {
		switch (dir) {
			case TurnDirection.LEFT: return -r;
			case TurnDirection.RIGHT: return r;
			case TurnDirection.UP: return u;
			case TurnDirection.DOWN: return -u;
			case TurnDirection.ANA: return a;
			case TurnDirection.KATA: return -a;
			default: throw new NotImplementedException();
		}
	}

	private IEnumerator TwitchHandleForcedSolve() {
		yield return null;
		while (!Idle()) yield return null;
		List<Vector4Int> way = FindSolution();
		foreach (Vector4Int adj in way) {
			if (pos.AddMod(f, SIZE) != adj) {
				TurnDirection[] possibleDirections = AllTurnDirections.Where(td => pos.AddMod(TurnDirectionToDirection(td), SIZE) == adj).ToArray();
				if (possibleDirections.Length == 0) {
					TurnDirection d = AllTurnDirections.PickRandom();
					queue.Enqueue(d);
					while (!Idle()) yield return null;
					queue.Enqueue(d);
				} else queue.Enqueue(possibleDirections.PickRandom());
				while (!Idle()) yield return null;
			}
			queue.Enqueue(null);
			while (!Idle()) yield return null;
		}
		Submit();
	}

	private List<Vector4Int> FindSolution() {
		FourDimArray<List<Vector4Int>> temp = new FourDimArray<List<Vector4Int>>(SIZE, null);
		temp[toPos] = new List<Vector4Int>();
		Queue<Vector4Int> q = new Queue<Vector4Int>();
		q.Enqueue(toPos);
		while (temp[target] == null) {
			Vector4Int p = q.Dequeue();
			foreach (Vector4Int d in DIRECTIONS) {
				Vector4Int adjPos = p.AddMod(d, SIZE);
				if (walls[adjPos] != null) continue;
				if (temp[adjPos] != null) continue;
				temp[adjPos] = new List<Vector4Int>(temp[p]);
				temp[adjPos].Add(adjPos);
				q.Enqueue(adjPos);
			}
		}
		return temp[target];
	}

	private static Vector4 Lerp(Vector4Int from, Vector4Int to, float anim) {
		return (((Vector4)from) + (to - from) * anim);
	}

	private static Vector4 LerpNorm(Vector4Int from, Vector4Int to, float anim) {
		return Lerp(from, to, anim).normalized;
	}

	private bool Idle() {
		return anim >= 1f && queue.Count == 0 && pos == toPos && r == toR && u == toU && a == toA && f == toF;
	}
}
