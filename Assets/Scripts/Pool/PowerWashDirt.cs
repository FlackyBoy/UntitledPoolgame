using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UntitledPoolGame.Pool
{
    // Power Wash Pool: the grime over the felt and the match clock. A grid of
    // cells in the felt's own frame (x along the length, z across), each
    // dirty, washed by P1 or P2, or out of play (around the pockets). Every
    // frame, each rolling group ball washes a capsule from where it was to
    // where it is, for its owner; the 8 muddies its own. Shown by a texture
    // on a quad laid on the felt (one pixel per cell). Counts are kept as
    // cells change, so the percentages cost nothing to read. Created by
    // PowerWashPoolRuleSet.Setup, destroyed by its Cleanup.
    public class PowerWashDirt : MonoBehaviour
    {
        public static PowerWashDirt Instance { get; private set; }

        // (owner, point, radius): a pocket splash starts.
        public static event Action<int, Vector3, float> Splashed;

        private const byte Dirty = 0, OutOfPlay = 255;

        private PowerWashPoolSettings settings;
        private PoolTableSurface surface;
        private Quaternion inverseRotation;
        private float halfLength, halfWidth, cellSize;
        private int cellsX, cellsZ, playableCells;

        // 0 dirty, 1 + player washed, OutOfPlay.
        private byte[] cells;
        private readonly int[] washedCount = new int[2];
        private Color32[] pixels, dirtPixels;
        private readonly Color32[] washedPixel = new Color32[2];
        private readonly Color[] playerColour = new Color[2];
        // Splash being painted: the cells it changes, for their shine.
        private List<int> collecting;
        // Cue balls hidden after going in, shown again once back in play.
        private readonly PoolBall[] hiddenCue = new PoolBall[2];
        private bool pixelsChanged;

        private Texture2D texture;
        private Material material;
        private Mesh mesh;

        // Where each ball was last frame: the trail runs from there.
        private readonly Dictionary<PoolBall, Vector3> lastPosition = new Dictionary<PoolBall, Vector3>();
        // A ball that moved more than this in one frame was put back (respot,
        // Rejouer): no trail across the table.
        private const float TeleportDistance = 0.5f;

        // Splashes still spreading: the match isn't judged before they land.
        private int splashesInFlight;

        public float TimeLeft { get; private set; }
        // Time is up with both players level: the next to lead wins.
        public bool Overtime { get; private set; }

        public static PowerWashDirt Create(PowerWashPoolSettings settings, Color player1, Color player2)
        {
            PoolTableSurface surface = PoolTableSurface.Instance;
            if (surface == null)
            {
                Debug.LogWarning("[PowerWash] Pas de PoolTableSurface dans la scène : pas de crasse.");
                return null;
            }
            var dirt = new GameObject("PowerWash (crasse)").AddComponent<PowerWashDirt>();
            dirt.Build(settings, surface, player1, player2);
            return dirt;
        }

        private void Build(PowerWashPoolSettings s, PoolTableSurface table, Color player1, Color player2)
        {
            Instance = this;
            settings = s;
            surface = table;
            halfLength = table.HalfLength;
            halfWidth = table.HalfWidth;
            inverseRotation = Quaternion.Inverse(table.transform.rotation);
            cellsX = s.resolution;
            cellSize = 2f * halfLength / cellsX;
            cellsZ = Mathf.Max(1, Mathf.RoundToInt(2f * halfWidth / cellSize));

            int count = cellsX * cellsZ;
            cells = new byte[count];
            pixels = new Color32[count];
            dirtPixels = new Color32[count];
            washedPixel[0] = Tint(player1, s.washedTint);
            washedPixel[1] = Tint(player2, s.washedTint);
            playerColour[0] = player1;
            playerColour[1] = player2;

            // Grime with blotches: two octaves of Perlin, a random offset so
            // each match looks different.
            Vector2 seed = new Vector2(UnityEngine.Random.value * 100f, UnityEngine.Random.value * 100f);
            Color dirt = s.dirtColor;
            for (int z = 0; z < cellsZ; z++)
            for (int x = 0; x < cellsX; x++)
            {
                float wx = x * cellSize / s.dirtNoiseScale, wz = z * cellSize / s.dirtNoiseScale;
                float n = Mathf.PerlinNoise(seed.x + wx, seed.y + wz) * 0.7f + Mathf.PerlinNoise(seed.x + wx * 4f, seed.y + wz * 4f) * 0.3f;
                float shade = 1f + (n - 0.5f) * 2f * s.dirtNoise;
                dirtPixels[z * cellsX + x] = new Color(Mathf.Clamp01(dirt.r * shade), Mathf.Clamp01(dirt.g * shade), Mathf.Clamp01(dirt.b * shade),
                                                       Mathf.Clamp01(dirt.a * Mathf.Lerp(1f, shade, 0.3f)));
            }

            // Pockets out of play: their mouths stay visible.
            foreach (PoolPocket pocket in PoolPocket.Active)
                Stamp(ToCell(pocket.transform.position), ToCell(pocket.transform.position), (pocket.Radius + s.pocketClearance) / cellSize, OutOfPlay, force: true);

            Reset();
            BuildQuad(table);
        }

        // Every cell dirty, the clock full.
        private void Reset()
        {
            playableCells = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != OutOfPlay) { cells[i] = Dirty; playableCells++; pixels[i] = dirtPixels[i]; }
                else pixels[i] = new Color32(dirtPixels[i].r, dirtPixels[i].g, dirtPixels[i].b, 0);
            }
            washedCount[0] = washedCount[1] = 0;
            pixelsChanged = true;
            lastPosition.Clear();
            TimeLeft = settings.matchDuration;
            Overtime = false;
        }

        private static Color32 Tint(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);

        private void BuildQuad(PoolTableSurface table)
        {
            texture = new Texture2D(cellsX, cellsZ, TextureFormat.RGBA32, false)
            {
                name = "PowerWashDirt", wrapMode = TextureWrapMode.Clamp,
                filterMode = settings.sharpEdges ? FilterMode.Point : FilterMode.Bilinear,
            };
            // Same material setup as the table holes: drawn over the felt,
            // under the balls (they write depth first).
            material = new Material(Shader.Find("Sprites/Default")) { renderQueue = 2450, mainTexture = texture };

            mesh = new Mesh
            {
                name = "PowerWashDirt",
                vertices = new[]
                {
                    new Vector3(-halfLength, 0f, -halfWidth), new Vector3(halfLength, 0f, -halfWidth),
                    new Vector3(-halfLength, 0f, halfWidth), new Vector3(halfLength, 0f, halfWidth),
                },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Not parented to the surface: its scale is not uniform (the
            // quad is in metres).
            Vector3 origin = table.transform.position;
            origin.y = table.FeltTopY + 0.002f;
            transform.SetPositionAndRotation(origin, table.transform.rotation);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        // Share of the playable felt washed by the player, 0-1.
        public float WashedShare(int player) => playableCells > 0 ? (float)washedCount[player] / playableCells : 0f;

        // The player with more felt washed, -1 when level.
        public int Leader => washedCount[0] == washedCount[1] ? -1 : washedCount[0] > washedCount[1] ? 0 : 1;

        // A potted ball: its trail starts afresh where it comes back.
        public void Forget(PoolBall ball) => lastPosition.Remove(ball);

        // Ends the clock now (the 8 went in); a tie still goes to overtime.
        public void EndNow() => TimeLeft = 0f;

        private void Update()
        {
            PoolMatchRules match = PoolMatchRules.Instance;
            if (match == null || !match.MatchStarted || match.GameOver) return;

            // Back from the pocket (Speed Pool's delay, or the end of the shot).
            for (int p = 0; p < hiddenCue.Length; p++)
            {
                PoolBall cue = hiddenCue[p];
                if (cue == null || cue.Rigidbody.isKinematic) continue;
                cue.SetMeshesVisible(true);
                hiddenCue[p] = null;
            }

            // Indexed: foreach over the IReadOnlyList would allocate each frame.
            IReadOnlyList<PoolBall> balls = PoolBall.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (ball.IsCueBall) continue;
                Vector3 now = ball.transform.position;
                bool known = lastPosition.TryGetValue(ball, out Vector3 before);
                lastPosition[ball] = now;
                if (!known || !ball.IsRolling || ball.Rigidbody.isKinematic) continue;
                if ((now - before).sqrMagnitude > TeleportDistance * TeleportDistance) continue;

                float width;
                byte value;
                if (ball.Group == BallGroup.Eight)
                {
                    if (settings.mudTrailWidth <= 0f) continue;
                    width = settings.mudTrailWidth;
                    value = Dirty;
                }
                else
                {
                    int owner = SpeedPoolRuleSet.OwnerOf(ball);
                    if (owner < 0) continue;
                    width = Mathf.Lerp(settings.slowTrailWidth, settings.fastTrailWidth, ball.HorizontalSpeed / settings.fullSpeed);
                    value = (byte)(owner + 1);
                }
                Stamp(ToCell(before), ToCell(now), width * ball.Radius / cellSize, value, force: false);
            }

            UpdateClock(match);
        }

        private void UpdateClock(PoolMatchRules match)
        {
            if (TimeLeft > 0f)
            {
                TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
                if (TimeLeft > 0f) return;
            }
            // The 8's splash, or one at the buzzer, counts in full.
            if (splashesInFlight > 0) return;
            int leader = Leader;
            if (leader < 0)
            {
                if (!Overtime && settings.debugLogs) Debug.Log("[PowerWash] Temps écoulé, égalité : prolongation, le prochain qui passe devant gagne.", this);
                Overtime = true;
                return;
            }
            if (settings.debugLogs)
                Debug.Log($"[PowerWash] Fin{(Overtime ? " de la prolongation" : "")} : J1 {WashedShare(0):P0}, J2 {WashedShare(1):P0} → J{leader + 1} gagne.", this);
            match.Win(leader);
        }

        private void LateUpdate()
        {
            if (!pixelsChanged) return;
            pixelsChanged = false;
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        // A potted cue ball vanishes until it is back in play.
        public void HideCue(PoolBall cue)
        {
            if (cue.CueOwner < 0 || cue.CueOwner >= hiddenCue.Length) return;
            cue.SetMeshesVisible(false);
            hiddenCue[cue.CueOwner] = cue;
        }

        // A splash washed for the player (a ball potted, the 8): a puddle
        // with a ragged edge thrown towards the middle of the table (the
        // pocket is on the rail, half a disc would land off the felt), there
        // the moment the ball goes in, and drops flung around it on streaks
        // over Splash Duration. Fresh paint shines, then settles; a burst of
        // water at the pocket and a jolt of the player's screen.
        public void Splash(Vector3 point, float radius, int player)
        {
            if (player < 0 || radius <= 0f) return;
            Splashed?.Invoke(player, point, radius);
            float strength = Mathf.Clamp(radius / Mathf.Max(0.01f, settings.pocketSplashRadius), 0.5f, 2f);
            PlayBurst(point, strength, playerColour[player]);
            // Alone at the keyboard, the one screen plays it for either player.
            int screen = UnityEngine.InputSystem.PlayerInput.all.Count <= 1 ? -1 : player;
            LocalPoolPowerEffectReceiver.PlayImpact(screen, settings.shakeMagnitude * strength, settings.shakeDuration,
                playerColour[player], settings.screenFlashAlpha * Mathf.Min(1f, strength), settings.screenFlashDuration);
            StartCoroutine(Spread(ToCell(point), radius / cellSize, (byte)(player + 1)));
        }

        private void PlayBurst(Vector3 point, float strength, Color colour)
        {
            if (settings.splashVfx == null) return;
            point.y = surface.FeltTopY;
            GameObject burst = Instantiate(settings.splashVfx, point, Quaternion.identity);
            burst.transform.localScale *= settings.splashVfxScale * strength;
            ParticleSystem[] systems = burst.GetComponentsInChildren<ParticleSystem>();
            // Stopped first: Play On Awake has queued its time-0 bursts.
            foreach (ParticleSystem system in systems) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            int boost = Mathf.Max(1, settings.splashVfxDropCount);
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                if (settings.tintSplashVfx) main.startColor = colour;
                // Sub-emitters fire once per boosted particle: room for all.
                main.maxParticles *= boost;
                if (!IsBoosted(system.name)) continue;
                main.startSpeedMultiplier *= settings.splashVfxSpeed;
                ParticleSystem.EmissionModule emission = system.emission;
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                for (int i = 0; i < bursts.Length; i++)
                {
                    ParticleSystem.MinMaxCurve count = bursts[i].count;
                    count.constant *= boost;
                    count.constantMin *= boost;
                    count.constantMax *= boost;
                    count.curveMultiplier *= boost;
                    bursts[i].count = count;
                }
                emission.SetBursts(bursts);
                emission.rateOverTimeMultiplier *= boost;
                if (settings.splashVfxSpread > 0f)
                {
                    // A cone around the way it shot out (no shape = straight along its z).
                    ParticleSystem.ShapeModule shape = system.shape;
                    if (!shape.enabled) { shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = 0.0001f; }
                    if (shape.shapeType == ParticleSystemShapeType.Cone) shape.angle = Mathf.Max(shape.angle, settings.splashVfxSpread);
                }
            }
            // From the root: the sub-emitters only fire from their parents.
            if (burst.TryGetComponent(out ParticleSystem root)) root.Play(true);
            else foreach (ParticleSystem system in systems) system.Play(false);
            Destroy(burst, 5f);
        }

        private bool IsBoosted(string systemName)
        {
            if (settings.splashVfxBoosted == null) return false;
            foreach (string prefix in settings.splashVfxBoosted)
                if (!string.IsNullOrEmpty(prefix) && systemName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private IEnumerator Spread(Vector2 pocket, float reach, byte value)
        {
            splashesInFlight++;
            Vector2 inward = new Vector2(cellsX * 0.5f, cellsZ * 0.5f) - pocket;
            inward = inward.sqrMagnitude > 0f ? inward.normalized : Vector2.up;
            float puddle = reach * settings.splashPuddleShare;
            Vector2 centre = pocket + inward * puddle * 0.5f;
            // Ragged edge: three waves around the rim, random phases.
            Vector3 phase = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value) * Mathf.PI * 2f;

            // Drops: direction within Splash Spread of inward, landing time
            // in proportion to how far they fly.
            int dropCount = settings.splashDrops;
            var dropAt = new Vector2[dropCount];
            var dropSize = new float[dropCount];
            var dropLanded = new bool[dropCount];
            float inwardAngle = Mathf.Atan2(inward.y, inward.x);
            for (int i = 0; i < dropCount; i++)
            {
                float angle = inwardAngle + UnityEngine.Random.Range(-0.5f, 0.5f) * settings.splashSpread * Mathf.Deg2Rad;
                float distance = reach * UnityEngine.Random.Range(0.55f, 1f);
                dropAt[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                dropSize[i] = reach * UnityEngine.Random.Range(0.04f, 0.11f);
            }

            // Shine: the splash's own cells, a lighter take on the colour.
            var fresh = new List<int>();
            Color shineColour = Color.Lerp(playerColour[value - 1], Color.white, settings.freshPaintWhiten);
            shineColour.a = 0.95f;
            Color32 shine = shineColour;
            bool shines = settings.freshPaintTime > 0f;

            float duration = Mathf.Max(0.01f, settings.splashDuration);
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                int before = fresh.Count;
                collecting = fresh;
                // The puddle is there at once, full a few frames later.
                StampPuddle(centre, puddle * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(k * 3f)), phase, value);
                for (int i = 0; i < dropCount; i++)
                {
                    if (dropLanded[i] || k < (dropAt[i] - centre).magnitude / reach) continue;
                    dropLanded[i] = true;
                    Vector2 from = centre + (dropAt[i] - centre).normalized * puddle * 0.7f;
                    Stamp(from, dropAt[i], dropSize[i] * 0.45f, value, force: true);
                    Stamp(dropAt[i], dropAt[i], dropSize[i], value, force: true);
                }
                collecting = null;
                if (shines)
                    for (int i = before; i < fresh.Count; i++) pixels[fresh[i]] = shine;
                if (k >= 1f) break;
                yield return null;
            }
            splashesInFlight--;

            // Then the shine settles into the washed tint (cells taken back
            // meanwhile keep what they have).
            if (!shines) yield break;
            Color32 washed = washedPixel[value - 1];
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / settings.freshPaintTime);
                // Ease out: bright for a moment, then fades.
                Color32 colour = Color32.Lerp(shine, washed, k * k);
                foreach (int index in fresh)
                    if (cells[index] == value) pixels[index] = colour;
                pixelsChanged = true;
                if (k >= 1f) yield break;
                yield return null;
            }
        }

        // Cells within the ragged rim around centre (radius in cells).
        private void StampPuddle(Vector2 centre, float radius, Vector3 phase, byte value)
        {
            float outer = radius * 1.3f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(centre.x - outer)), maxX = Mathf.Min(cellsX - 1, Mathf.CeilToInt(centre.x + outer));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(centre.y - outer)), maxZ = Mathf.Min(cellsZ - 1, Mathf.CeilToInt(centre.y + outer));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 d = new Vector2(x + 0.5f, z + 0.5f) - centre;
                float a = Mathf.Atan2(d.y, d.x);
                float rim = radius * (1f + 0.14f * Mathf.Sin(3f * a + phase.x) + 0.09f * Mathf.Sin(5f * a + phase.y) + 0.06f * Mathf.Sin(9f * a + phase.z));
                if (d.sqrMagnitude <= rim * rim) Set(z * cellsX + x, value, true);
            }
        }

        // Felt point to grid coordinates (cell units, cell i spans i..i+1).
        private Vector2 ToCell(Vector3 world)
        {
            Vector3 local = inverseRotation * (world - surface.transform.position);
            return new Vector2((local.x + halfLength) / cellSize, (local.z + halfWidth) / cellSize);
        }

        // Every cell whose centre lies within radius (cells) of segment a-b
        // takes value. force: a splash takes the opponent's cells even when
        // Steal Washed is off.
        private void Stamp(Vector2 a, Vector2 b, float radius, byte value, bool force)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius));
            int maxX = Mathf.Min(cellsX - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius));
            int maxZ = Mathf.Min(cellsZ - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius));
            Vector2 ab = b - a;
            float abSqr = ab.sqrMagnitude, radiusSqr = radius * radius;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, z + 0.5f);
                float t = abSqr > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / abSqr) : 0f;
                if ((a + ab * t - p).sqrMagnitude <= radiusSqr) Set(z * cellsX + x, value, force);
            }
        }

        private void Set(int index, byte value, bool force)
        {
            byte old = cells[index];
            if (old == value) return;
            if (value == OutOfPlay) { cells[index] = OutOfPlay; return; }
            if (old == OutOfPlay) return;
            if (old != Dirty && value != Dirty && !force && !settings.stealWashed) return;

            if (old != Dirty) washedCount[old - 1]--;
            if (value != Dirty) washedCount[value - 1]++;
            cells[index] = value;
            pixels[index] = value == Dirty ? dirtPixels[index] : washedPixel[value - 1];
            pixelsChanged = true;
            collecting?.Add(index);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Destroy(texture);
            Destroy(material);
            Destroy(mesh);
        }
    }
}
