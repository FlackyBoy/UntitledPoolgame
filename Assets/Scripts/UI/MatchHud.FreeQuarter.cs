using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // The quarter of the screen nobody plays in, at three players (split
    // screen in a 2 × 2 grid). While the round is played: the table seen from
    // above, with everyone's total beside it. At the countdown and the end of
    // a round: a big panel with the ranking. Always: the invite to join as J4.
    // The totals and the panel need a mode with points (IMatchScoreboard);
    // without one, only the view from above and the invite.
    public partial class MatchHud
    {
        // Laid out for a 960 × 540 quarter (1920 × 1080 split in four), scaled
        // to the real one.
        private const float FreeWidth = 960f, FreeHeight = 540f;
        private const int MaxPips = 10;

        private VisualElement freeFrame, freeContent, freeTag, freeTotals, freeInvite, freePanel, freeRows, freeChips;
        private Label freeTotalHeader, freeGoal, freeInviteText, freePanelTitle, freePanelSub, freeCount, freeReady;
        private readonly FreeEntry[] freeTotalEntries = new FreeEntry[PoolMatchRules.MaxPlayers];
        private readonly FreeEntry[] freeChipEntries = new FreeEntry[PoolMatchRules.MaxPlayers];
        private readonly FreeRow[] freeRowEntries = new FreeRow[PoolMatchRules.MaxPlayers];
        private readonly int[] freeOrder = new int[PoolMatchRules.MaxPlayers];
        private Camera freeCamera;
        private UniversalAdditionalCameraData freeCameraData;
        // The lights over the table, dimmed while the free camera draws, and
        // their own intensities to give back once it's done.
        private readonly List<Light> freeDimmed = new List<Light>();
        private readonly List<float> freeDimmedIntensity = new List<float>();
        private Vector3 freeDimCenter;
        private bool freeDimFound;
        // The players' camera it renders like (exposure, tone mapping, volumes).
        private Camera freeLookSource;
        private UniversalAdditionalCameraData freeLookSourceData;
        private int freeKey = int.MinValue;
        private float freePanelBlend;

        // A player's chip: colour box with their tag, and their points.
        private class FreeEntry
        {
            public VisualElement box, who;
            public Label whoText, points;
        }

        // A line of the big panel.
        private class FreeRow
        {
            public VisualElement box, who, badge;
            public Label rank, whoText, points;
            public readonly VisualElement[] pips = new VisualElement[MaxPips];
        }

        // ---------- Building ----------

        private void BuildFree()
        {
            freeFrame = Box(halvesLayer, "free-quarter");
            freeFrame.style.position = Position.Absolute;
            freeFrame.style.overflow = Overflow.Hidden;
            freeFrame.style.display = DisplayStyle.None;

            freeContent = Box(freeFrame, "content");
            freeContent.style.position = Position.Absolute;
            freeContent.style.left = freeContent.style.top = 0;
            freeContent.style.width = FreeWidth;
            freeContent.style.height = FreeHeight;
            freeContent.style.transformOrigin = new TransformOrigin(0, 0);

            // Tag, top left.
            freeTag = Box(freeContent, "tag");
            freeTag.style.position = Position.Absolute;
            freeTag.style.left = 24;
            freeTag.style.top = 20;
            freeTag.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
            Radius(freeTag, 8);
            freeTag.style.paddingLeft = freeTag.style.paddingRight = 12;
            freeTag.style.paddingTop = freeTag.style.paddingBottom = 4;
            Text(freeTag, cfg.freeViewText, titleFont, 20, Color.white);

            // Totals, a column on the left.
            freeTotals = Box(freeContent, "totals");
            freeTotals.style.position = Position.Absolute;
            freeTotals.style.left = 26;
            freeTotals.style.top = 90;
            freeTotals.style.width = 130;
            freeTotals.style.alignItems = Align.Stretch;
            freeTotalHeader = Text(freeTotals, cfg.freeTotalText, titleFont, 18, Yellow);
            freeTotalHeader.style.marginBottom = 6;
            for (int i = 0; i < freeTotalEntries.Length; i++)
            {
                freeTotalEntries[i] = BuildChip(freeTotals, 40f, 32f);
                freeTotalEntries[i].box.style.marginBottom = 8;
            }
            freeGoal = Text(freeTotals, "", bodyFont, 15, new Color(1f, 1f, 1f, 0.8f));

            // Invite, top right.
            freeInvite = Box(freeContent, "invite");
            freeInvite.style.position = Position.Absolute;
            freeInvite.style.right = 24;
            freeInvite.style.top = 18;
            freeInvite.style.flexDirection = FlexDirection.Row;
            freeInvite.style.alignItems = Align.Center;
            freeInvite.style.backgroundColor = cfg.PlayerColor(3);
            Radius(freeInvite, 12);
            Border(freeInvite, 3, Ink);
            freeInvite.style.borderBottomWidth = 7;
            freeInvite.style.paddingLeft = 6;
            freeInvite.style.paddingRight = 14;
            freeInvite.style.paddingTop = freeInvite.style.paddingBottom = 5;
            VisualElement play = Box(freeInvite, "play");
            play.style.width = play.style.height = 30;
            play.style.marginRight = 8;
            play.style.backgroundColor = Color.white;
            play.style.justifyContent = Justify.Center;
            Radius(play, 15);
            Border(play, 2, Ink);
            Text(play, "▶", bodyFont, 14, Ink);
            freeInviteText = Text(freeInvite, "", titleFont, 20, Color.white);

            // Panel between rounds, over everything else.
            freePanel = Box(freeContent, "panel");
            Fill(freePanel);
            freePanel.style.backgroundColor = new Color(0.16f, 0.1f, 0.06f, 0.97f);
            freePanel.style.alignItems = Align.Center;
            freePanel.style.justifyContent = Justify.Center;
            freePanel.style.paddingTop = 60;
            freePanel.style.opacity = 0f;

            freeCount = Text(freePanel, "", titleFont, 110, Color.white);
            InkShadow(freeCount, 6);
            freeReady = Text(freePanel, cfg.freeReadyText, bodyFont, 22, Color.white);
            freeReady.style.marginBottom = 8;
            freePanelTitle = Text(freePanel, "", titleFont, 44, Yellow);
            InkShadow(freePanelTitle, 4);
            freePanelTitle.style.rotate = new Rotate(new Angle(-3f, AngleUnit.Degree));
            freePanelSub = Text(freePanel, "", bodyFont, 22, new Color(1f, 1f, 1f, 0.85f));
            freePanelSub.style.marginTop = 4;
            freePanelSub.style.marginBottom = 14;

            freeRows = Box(freePanel, "rows");
            freeRows.style.width = 760;
            for (int i = 0; i < freeRowEntries.Length; i++) freeRowEntries[i] = BuildRow(freeRows);

            freeChips = Box(freePanel, "chips");
            freeChips.style.flexDirection = FlexDirection.Row;
            freeChips.style.marginTop = 10;
            for (int i = 0; i < freeChipEntries.Length; i++)
            {
                freeChipEntries[i] = BuildChip(freeChips, 38f, 30f);
                freeChipEntries[i].box.style.marginLeft = freeChipEntries[i].box.style.marginRight = 6;
            }
        }

        private FreeEntry BuildChip(VisualElement parent, float whoSize, float pointsSize)
        {
            var e = new FreeEntry();
            e.box = Box(parent, "chip");
            e.box.style.flexDirection = FlexDirection.Row;
            e.box.style.alignItems = Align.Center;
            e.box.style.backgroundColor = Color.white;
            Radius(e.box, 10);
            Border(e.box, 3, Ink);
            e.box.style.borderBottomWidth = 6;
            e.box.style.paddingLeft = 4;
            e.box.style.paddingRight = 12;
            e.box.style.paddingTop = e.box.style.paddingBottom = 3;
            e.who = Box(e.box, "who");
            e.who.style.width = e.who.style.height = whoSize;
            e.who.style.justifyContent = Justify.Center;
            Radius(e.who, 8);
            Border(e.who, 2, Ink);
            e.whoText = Text(e.who, "", titleFont, whoSize * 0.42f, Color.white);
            e.points = Text(e.box, "", titleFont, pointsSize, Ink);
            e.points.style.marginLeft = 10;
            e.points.style.flexGrow = 1;
            e.points.style.unityTextAlign = TextAnchor.MiddleRight;
            return e;
        }

        private FreeRow BuildRow(VisualElement parent)
        {
            var r = new FreeRow();
            r.box = PopCard(parent, "row");
            r.box.style.flexDirection = FlexDirection.Row;
            r.box.style.alignItems = Align.Center;
            r.box.style.height = 66;
            r.box.style.marginBottom = 10;
            r.box.style.paddingLeft = 10;
            r.box.style.paddingRight = 20;
            r.rank = Text(r.box, "", titleFont, 32, Ink);
            r.rank.style.width = 44;
            r.who = Box(r.box, "who");
            r.who.style.width = r.who.style.height = 50;
            r.who.style.marginRight = 16;
            r.who.style.justifyContent = Justify.Center;
            Radius(r.who, 10);
            Border(r.who, 3, Ink);
            r.whoText = Text(r.who, "", titleFont, 21, Color.white);
            for (int i = 0; i < MaxPips; i++)
            {
                VisualElement pip = Box(r.box, "pip");
                pip.style.width = pip.style.height = 24;
                pip.style.marginRight = 6;
                Radius(pip, 12);
                Border(pip, 3, Ink);
                r.pips[i] = pip;
            }
            r.badge = Box(r.box, "badge");
            r.badge.style.backgroundColor = Ink;
            Radius(r.badge, 6);
            r.badge.style.paddingLeft = r.badge.style.paddingRight = 8;
            r.badge.style.marginLeft = 8;
            Text(r.badge, "+1 !", bodyFont, 16, Yellow);
            r.points = Text(r.box, "", titleFont, 40, Ink);
            r.points.style.flexGrow = 1;
            r.points.style.unityTextAlign = TextAnchor.MiddleRight;
            return r;
        }

        // ---------- Every frame ----------

        private void UpdateFree(PoolMatchRules rules, Vector2 size, float dt)
        {
            if (!cfg.freeQuarter || float.IsNaN(size.x) || size.x < 1f || !FindFreeRect(out Rect cell))
            {
                HideFree();
                return;
            }

            float left = cell.x * size.x, top = (1f - cell.y - cell.height) * size.y;
            float width = cell.width * size.x, height = cell.height * size.y;
            freeFrame.style.display = DisplayStyle.Flex;
            freeFrame.style.left = left;
            freeFrame.style.top = top;
            freeFrame.style.width = width;
            freeFrame.style.height = height;
            float k = Mathf.Min(width / FreeWidth, height / FreeHeight);
            freeContent.style.scale = new Scale(new Vector3(k, k, 1f));
            freeContent.style.left = (width - FreeWidth * k) * 0.5f;
            freeContent.style.top = (height - FreeHeight * k) * 0.5f;

            IMatchScoreboard board = rules.Scoreboard;
            PlaceFreeCamera(rules, cell, board != null);

            // The panel takes the quarter at the countdown and between rounds.
            bool panel = board != null && board.Moment != ScoreboardMoment.Play;
            freePanelBlend = Mathf.MoveTowards(freePanelBlend, panel ? 1f : 0f, dt * 4f);
            freePanel.style.opacity = freePanelBlend;
            freePanel.style.display = freePanelBlend > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            freePanel.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.06f, 1f, freePanelBlend));
            freeTotals.style.display = board != null ? DisplayStyle.Flex : DisplayStyle.None;
            freeTotals.style.opacity = freeTag.style.opacity = 1f - freePanelBlend;

            // The invite, while a fourth player may still come.
            int joined = PlayerInput.all.Count;
            bool invite = PlayerDeviceRouter.JoinByButtonOpen && joined < PlayerDeviceRouter.JoinLimit(PlayerInputManager.instance);
            freeInvite.style.display = invite ? DisplayStyle.Flex : DisplayStyle.None;
            if (invite)
            {
                float t = Time.unscaledTime;
                freeInvite.style.scale = new Scale(Vector3.one * (1f + 0.05f * Mathf.Sin(t * 4.5f)));
                freeInvite.style.rotate = new Rotate(new Angle(-1.5f * Mathf.Sin(t * 4.5f), AngleUnit.Degree));
            }

            int key = FreeKey(board, joined, invite);
            if (key == freeKey) return;
            freeKey = key;
            TextFx.Set(freeInviteText, Format(cfg.freeInviteFormat, joined + 1));
            if (board != null) FillScores(board);
        }

        // Changes whenever a text or a colour of the quarter would.
        private static int FreeKey(IMatchScoreboard board, int joined, bool invite)
        {
            int key = joined * 2 + (invite ? 1 : 0);
            if (board == null) return key;
            key = key * 8 + (int)board.Moment;
            key = key * 32 + Mathf.Clamp(board.CountdownLeft, 0, 31);
            key = key * 8 + board.LastScorer + 1;
            key = key * 16 + board.PointsToWin;
            for (int p = 0; p < board.ScoreboardPlayers && p < PoolMatchRules.MaxPlayers; p++)
                key = key * 64 + board.Points(p) * 2 + (board.IsOut(p) ? 1 : 0);
            return key;
        }

        private void FillScores(IMatchScoreboard board)
        {
            int count = Mathf.Min(board.ScoreboardPlayers, PoolMatchRules.MaxPlayers);
            // Ranking: points, then player number.
            for (int i = 0; i < count; i++) freeOrder[i] = i;
            for (int i = 1; i < count; i++)
            {
                int p = freeOrder[i], j = i - 1;
                while (j >= 0 && board.Points(freeOrder[j]) < board.Points(p)) { freeOrder[j + 1] = freeOrder[j]; j--; }
                freeOrder[j + 1] = p;
            }
            int best = count > 0 ? board.Points(freeOrder[0]) : 0;
            int goal = board.PointsToWin;

            for (int i = 0; i < freeTotalEntries.Length; i++)
            {
                bool shown = i < count;
                SetChip(freeTotalEntries[i], shown, shown ? freeOrder[i] : 0, board, best, true);
                SetChip(freeChipEntries[i], shown, shown ? freeOrder[i] : 0, board, best, false);
            }
            TextFx.Set(freeGoal, Format(cfg.freeGoalFormat, goal));

            bool countdown = board.Moment == ScoreboardMoment.Countdown;
            freeCount.style.display = freeReady.style.display = countdown ? DisplayStyle.Flex : DisplayStyle.None;
            freeChips.style.display = countdown ? DisplayStyle.Flex : DisplayStyle.None;
            freeRows.style.display = countdown ? DisplayStyle.None : DisplayStyle.Flex;
            if (countdown)
            {
                TextFx.Set(freeCount, Mathf.Max(1, board.CountdownLeft).ToString());
                TextFx.Set(freePanelTitle, cfg.freeCountdownText);
                TextFx.Set(freePanelSub, Format(cfg.freeGoalFormat, goal));
            }
            else
            {
                TextFx.Set(freePanelTitle, cfg.freeRoundEndText);
                TextFx.Set(freePanelSub, board.LastScorer >= 0 ? Format(cfg.freeScorerFormat, board.LastScorer + 1) : "");
            }

            for (int i = 0; i < freeRowEntries.Length; i++)
            {
                FreeRow r = freeRowEntries[i];
                bool shown = i < count;
                r.box.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                int p = freeOrder[i], points = board.Points(p);
                bool lead = i == 0 && points > 0;
                Color color = cfg.PlayerColor(p);
                r.box.style.backgroundColor = lead ? Yellow : Cream;
                r.box.style.rotate = new Rotate(new Angle(lead ? -1.5f : 0f, AngleUnit.Degree));
                TextFx.Set(r.rank, (i + 1).ToString());
                r.who.style.backgroundColor = color;
                TextFx.Set(r.whoText, Format(cfg.playerTagFormat, p + 1));
                for (int s = 0; s < MaxPips; s++)
                {
                    r.pips[s].style.display = s < Mathf.Min(goal, MaxPips) ? DisplayStyle.Flex : DisplayStyle.None;
                    r.pips[s].style.backgroundColor = s < points ? color : new Color(0f, 0f, 0f, 0.1f);
                }
                r.badge.style.display = !countdown && p == board.LastScorer ? DisplayStyle.Flex : DisplayStyle.None;
                TextFx.Set(r.points, points.ToString());
            }
        }

        private void SetChip(FreeEntry e, bool shown, int p, IMatchScoreboard board, int best, bool dimOut)
        {
            e.box.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) return;
            int points = board.Points(p);
            bool lead = points == best && best > 0;
            e.box.style.backgroundColor = lead ? Yellow : Color.white;
            e.box.style.rotate = new Rotate(new Angle(lead ? -2f : 0f, AngleUnit.Degree));
            e.box.style.opacity = dimOut && board.IsOut(p) ? 0.45f : 1f;
            e.who.style.backgroundColor = cfg.PlayerColor(p);
            TextFx.Set(e.whoText, Format(cfg.playerTagFormat, p + 1));
            TextFx.Set(e.points, points.ToString());
        }

        private void HideFree()
        {
            if (freeFrame != null) freeFrame.style.display = DisplayStyle.None;
            if (freeCamera != null) freeCamera.enabled = false;
            freeKey = int.MinValue;
            freePanelBlend = 0f;
        }

        // Three players in a 2 × 2 split: the cell no player's camera is in
        // (viewport rect, origin bottom left). Uses the cameras Layout found.
        private bool FindFreeRect(out Rect free)
        {
            free = default;
            if (PlayerInput.all.Count != 3) return false;
            for (int cell = 0; cell < 4; cell++)
            {
                var r = new Rect(cell % 2 * 0.5f, cell < 2 ? 0.5f : 0f, 0.5f, 0.5f);
                bool taken = false;
                foreach (Half h in halves)
                {
                    if (h.input == null) continue;
                    if (h.cam == null) return false;
                    if (r.Contains(h.cam.rect.center)) { taken = true; break; }
                }
                if (taken) continue;
                free = r;
                return true;
            }
            return false;
        }

        // Rendered like the players' cameras: a bare camera has no
        // post-processing, so no tone mapping, and the lamp above the table
        // burnt the felt white where the players' views were fine. The
        // volumes are picked from the player's camera, not from 20 m up.
        // Copied every frame (a few fields): the quality level may change.
        private void MatchPlayersLook()
        {
            if (freeLookSource == null)
            {
                foreach (Half h in halves)
                {
                    if (h == null || h.cam == null) continue;
                    freeLookSource = h.cam;
                    freeLookSourceData = h.cam.GetUniversalAdditionalCameraData();
                    break;
                }
                if (freeLookSource == null) return;
            }
            freeCamera.allowHDR = freeLookSource.allowHDR;
            freeCameraData.renderPostProcessing = freeLookSourceData.renderPostProcessing;
            freeCameraData.antialiasing = freeLookSourceData.antialiasing;
            freeCameraData.antialiasingQuality = freeLookSourceData.antialiasingQuality;
            freeCameraData.volumeLayerMask = freeLookSourceData.volumeLayerMask;
            freeCameraData.volumeTrigger = freeLookSource.transform;
        }

        // Looking straight down, the felt sends the lamp above it right back
        // into the camera and burns white, a view the players never have:
        // only for this camera, the lights over the table are dimmed
        // (Free View Light Scale), then given back.
        private void FindLightsOver(Vector3 center, Vector2 halfSize)
        {
            freeDimFound = true;
            freeDimCenter = center;
            freeDimmed.Clear();
            float reach = Mathf.Max(halfSize.x, halfSize.y) * 1.5f;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.type == LightType.Directional) continue;
                Vector3 offset = light.transform.position - center;
                if (offset.y <= 0f || new Vector2(offset.x, offset.z).magnitude > reach) continue;
                freeDimmed.Add(light);
            }
        }

        private void DimForFreeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != freeCamera || cfg == null) return;
            freeDimmedIntensity.Clear();
            foreach (Light light in freeDimmed)
            {
                freeDimmedIntensity.Add(light != null ? light.intensity : 0f);
                if (light != null) light.intensity *= cfg.freeViewLightScale;
            }
        }

        private void UndimAfterFreeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != freeCamera) return;
            for (int i = 0; i < freeDimmed.Count && i < freeDimmedIntensity.Count; i++)
                if (freeDimmed[i] != null) freeDimmed[i].intensity = freeDimmedIntensity[i];
        }

        // An orthographic camera straight above the play area, in the free
        // cell. The totals column covers the left of the quarter: the table is
        // framed a little to the right of the middle when there are totals.
        private void PlaceFreeCamera(PoolMatchRules rules, Rect cell, bool totals)
        {
            if (!rules.GetOverview(out Vector3 center, out Vector2 halfSize, out Quaternion rotation))
            {
                if (freeCamera != null) freeCamera.enabled = false;
                return;
            }
            if (freeCamera == null)
            {
                var go = new GameObject("Free Quarter Camera (vue de dessus)");
                go.transform.SetParent(transform, false);
                freeCamera = go.AddComponent<Camera>();
                freeCamera.orthographic = true;
                freeCameraData = freeCamera.GetUniversalAdditionalCameraData();
            }
            // The lights over this table, looked up again when it moves (Sumo
            // Pool's small table, another level).
            if (!freeDimFound || (center - freeDimCenter).sqrMagnitude > 0.01f) FindLightsOver(center, halfSize);
            freeCamera.enabled = true;
            MatchPlayersLook();
            freeCamera.rect = cell;

            const float Height = 20f;
            float aspect = Screen.height > 0 ? cell.width * Screen.width / (cell.height * Screen.height) : 16f / 9f;
            float half = Mathf.Max(halfSize.x, halfSize.y) * Mathf.Max(1f, cfg.freeViewMargin);
            // The width left of the totals column is about 75 % of the quarter.
            float usable = totals ? 0.75f : 1f;
            float size = Mathf.Max(half, half / Mathf.Max(0.1f, aspect * usable));
            freeCamera.orthographicSize = size;
            Vector3 up = rotation * Vector3.forward;
            Quaternion look = Quaternion.LookRotation(Vector3.down, up);
            Vector3 right = look * Vector3.right;
            // Shift the view left so the table sits in the space the column leaves.
            float shift = totals ? size * aspect * (1f - usable) : 0f;
            freeCamera.transform.SetPositionAndRotation(center + Vector3.up * Height - right * shift, look);
            freeCamera.nearClipPlane = Mathf.Max(0.01f, Height - cfg.freeViewClearAbove);
            freeCamera.farClipPlane = Height + 10f;
        }
    }
}
